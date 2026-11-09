using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using WatermelonFarm;

/// <summary>
/// 统一「西瓜币」钱包自检（纯编辑器、同步执行，不进入 Play Mode）。
/// 菜单：Watermelon Science/Run Currency Smoke Test
/// 覆盖：旧金币/碎片一次性迁移、无旧存档初值、重置农场不重置钱包、
///       消费/收入边界、Farm 卖瓜 +2、Cut 评分等额发币、ShardWallet 兼容层。
/// 测试前完整备份所有相关 PlayerPrefs 与共享 JSON，测试后一律恢复（含删除迁移标记）。
/// </summary>
public static class WatermelonCurrencySmokeTest
{
    sealed class Snapshot
    {
        public bool hadFarm, hadShard, hadWallet, hadMigrated, hadOrders, hadStock, hadJson;
        public string farm, shard, orders, stock, json;
        public int wallet, migrated;
    }

    sealed class NullView : IFarmView
    {
        public void Render() { }
        public void AddLog(string type, string text) { }
        public void ShowModal(ModalSpec spec) { }
        public void CloseTopModal() { }
        public void ShowShardToast(int gained, int total) { }
        public void ShowWiltToast(string variety, string cause) { }
    }

    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder trace = new StringBuilder();

    static string SharedJsonPath { get { return WatermelonCurrencyWallet.SharedShardFilePath; } }

    static string ResultPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs",
                                            "WatermelonCurrencySmokeResult.txt"));
    }

    static void Check(bool ok, string what)
    {
        trace.AppendLine((ok ? "通过 · " : "失败 · ") + what);
        if (!ok) errors.Add(what);
    }

    static void ResetWalletCache()
    {
        FieldInfo f = typeof(WatermelonCurrencyWallet).GetField(
            "initialized", BindingFlags.Static | BindingFlags.NonPublic);
        if (f != null) f.SetValue(null, false);
        WatermelonCurrencyWallet.EnsureInitialized();
    }

    static Snapshot Capture()
    {
        Snapshot s = new Snapshot();
        s.hadFarm = PlayerPrefs.HasKey(WatermelonCurrencyWallet.FarmSaveKey);
        s.farm = s.hadFarm ? PlayerPrefs.GetString(WatermelonCurrencyWallet.FarmSaveKey, "") : "";
        s.hadShard = PlayerPrefs.HasKey(WatermelonCurrencyWallet.ShardPrefKey);
        s.shard = s.hadShard ? PlayerPrefs.GetString(WatermelonCurrencyWallet.ShardPrefKey, "") : "";
        s.hadWallet = PlayerPrefs.HasKey(WatermelonCurrencyWallet.WalletPrefKey);
        s.wallet = PlayerPrefs.GetInt(WatermelonCurrencyWallet.WalletPrefKey, 0);
        s.hadMigrated = PlayerPrefs.HasKey(WatermelonCurrencyWallet.MigrationPrefKey);
        s.migrated = PlayerPrefs.GetInt(WatermelonCurrencyWallet.MigrationPrefKey, 0);
        s.hadOrders = PlayerPrefs.HasKey(WatermelonGiftRedemptionService.OrdersPrefKey);
        s.orders = s.hadOrders ? PlayerPrefs.GetString(WatermelonGiftRedemptionService.OrdersPrefKey, "") : "";
        s.hadStock = PlayerPrefs.HasKey(WatermelonGiftCatalog.StockPrefKey);
        s.stock = s.hadStock ? PlayerPrefs.GetString(WatermelonGiftCatalog.StockPrefKey, "") : "";
        try
        {
            s.hadJson = File.Exists(SharedJsonPath);
            s.json = s.hadJson ? File.ReadAllText(SharedJsonPath, Encoding.UTF8) : "";
        }
        catch (Exception) { s.hadJson = false; s.json = ""; }
        return s;
    }

    static void Restore(Snapshot s)
    {
        PutString(WatermelonCurrencyWallet.FarmSaveKey, s.hadFarm, s.farm);
        PutString(WatermelonCurrencyWallet.ShardPrefKey, s.hadShard, s.shard);
        PutInt(WatermelonCurrencyWallet.WalletPrefKey, s.hadWallet, s.wallet);
        PutInt(WatermelonCurrencyWallet.MigrationPrefKey, s.hadMigrated, s.migrated);
        PutString(WatermelonGiftRedemptionService.OrdersPrefKey, s.hadOrders, s.orders);
        PutString(WatermelonGiftCatalog.StockPrefKey, s.hadStock, s.stock);
        PlayerPrefs.Save();

        try
        {
            if (s.hadJson)
            {
                string dir = Path.GetDirectoryName(SharedJsonPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SharedJsonPath, s.json, new UTF8Encoding(false));
            }
            else if (File.Exists(SharedJsonPath)) File.Delete(SharedJsonPath);
        }
        catch (Exception e) { Debug.LogWarning("[西瓜币自检] 恢复共享 JSON 失败：" + e.Message); }

        // 让静态缓存与恢复后的磁盘状态一致；不调用 Reload()，避免在键原本不存在时重复迁移
        FieldInfo f = typeof(WatermelonCurrencyWallet).GetField(
            "initialized", BindingFlags.Static | BindingFlags.NonPublic);
        if (f != null) f.SetValue(null, false);
        WatermelonGiftCatalog.Reload();
        WatermelonGiftRedemptionService.Reload();
    }

    static void PutString(string key, bool had, string value)
    {
        if (had) PlayerPrefs.SetString(key, value);
        else PlayerPrefs.DeleteKey(key);
    }

    static void PutInt(string key, bool had, int value)
    {
        if (had) PlayerPrefs.SetInt(key, value);
        else PlayerPrefs.DeleteKey(key);
    }

    static void ClearAll()
    {
        PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.FarmSaveKey);
        PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.ShardPrefKey);
        PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.WalletPrefKey);
        PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.MigrationPrefKey);
        PlayerPrefs.DeleteKey(WatermelonGiftRedemptionService.OrdersPrefKey);
        PlayerPrefs.DeleteKey(WatermelonGiftCatalog.StockPrefKey);
        PlayerPrefs.Save();
        try { if (File.Exists(SharedJsonPath)) File.Delete(SharedJsonPath); }
        catch (Exception) { }
        WatermelonGiftCatalog.Reload();
        WatermelonGiftRedemptionService.Reload();
    }

    static void WriteSharedJson(string json)
    {
        string dir = Path.GetDirectoryName(SharedJsonPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(SharedJsonPath, json, new UTF8Encoding(false));
    }

    static FarmModel MakeFarmWithMelons(int count, int price)
    {
        FarmModel m = new FarmModel();
        m.view = new NullView();
        m.P = SaveData.NewGame();
        for (int i = 0; i < count; i++)
        {
            WarehouseItem w = new WarehouseItem();
            w.v = "jx"; w.q = 1; w.wt = 5f; w.brix = 10f; w.coins = price;
            m.P.wh.Add(w);
        }
        return m;
    }

    [MenuItem("Watermelon Science/Run Currency Smoke Test")]
    public static void Run()
    {
        Snapshot snapshot = Capture();
        errors.Clear();
        trace.Length = 0;
        trace.AppendLine("西瓜币统一钱包 · 编辑器自检");
        trace.AppendLine("Unity " + Application.unityVersion);
        trace.AppendLine("时间 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        trace.AppendLine("");

        try
        {
            // ── A. 迁移：80 金币 + max(PP碎片20, JSON碎片25) = 105 ──
            ClearAll();
            PlayerPrefs.SetString(WatermelonCurrencyWallet.FarmSaveKey, "{\"coins\":80}");
            PlayerPrefs.SetString(WatermelonCurrencyWallet.ShardPrefKey,
                "{\"version\":1,\"items\":[{\"id\":\"watermelon\",\"count\":20}]}");
            WriteSharedJson("{\"version\":1,\"items\":[{\"id\":\"watermelon\",\"count\":25}]}");
            PlayerPrefs.Save();
            ResetWalletCache();
            Check(WatermelonCurrencyWallet.GetBalance() == 105,
                  "迁移：旧金币 80 + max(PP碎片 20, JSON碎片 25) = 105");
            Check(PlayerPrefs.GetInt(WatermelonCurrencyWallet.MigrationPrefKey, 0) == 1,
                  "迁移标记写入 pgz_watermelon_currency_migrated_v1 = 1");

            ResetWalletCache();
            Check(WatermelonCurrencyWallet.GetBalance() == 105, "再次初始化仍为 105（不重复迁移）");

            // ── B. 无旧存档 ──
            ClearAll();
            ResetWalletCache();
            Check(WatermelonCurrencyWallet.GetBalance() == 100, "无任何旧存档：初始余额 100");

            // ── C. 重置 Farm 进度不影响钱包 ──
            int beforeReset = WatermelonCurrencyWallet.GetBalance();
            PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.FarmSaveKey);
            PlayerPrefs.Save();
            ResetWalletCache();
            Check(WatermelonCurrencyWallet.GetBalance() == beforeReset,
                  "重置 Farm 进度后西瓜币不重置（" + beforeReset + "）");

            // ── D. 消费 / 收入边界 ──
            int b = WatermelonCurrencyWallet.GetBalance();
            Check(!WatermelonCurrencyWallet.TrySpend(b + 1, "test_insufficient"),
                  "余额不足时 TrySpend 返回 false");
            Check(WatermelonCurrencyWallet.GetBalance() == b, "余额不足时余额不变");
            Check(WatermelonCurrencyWallet.TrySpend(10, "test_spend"), "余额足够时消费成功");
            Check(WatermelonCurrencyWallet.GetBalance() == b - 10, "消费后余额 = " + (b - 10));
            WatermelonCurrencyWallet.Credit(-5, "test_negative");
            Check(WatermelonCurrencyWallet.GetBalance() == b - 10, "负数收入不改变余额");
            Check(WatermelonCurrencyWallet.Credit(5, "test_credit") == b - 5, "收入后立即保存并返回新余额");
            Check(WatermelonCurrencyWallet.GetBalance() >= 0, "余额不会为负");
            Check(WatermelonCurrencyWallet.Export().Contains(WatermelonCurrencyWallet.CurrencyName),
                  "Export 包含货币名称「西瓜币」");

            // ── E. Farm 卖瓜：售价 + 每瓜 2 西瓜币 ──
            int beforeSell = WatermelonCurrencyWallet.GetBalance();
            FarmModel one = MakeFarmWithMelons(1, 80);
            one.Sell(0);
            Check(WatermelonCurrencyWallet.GetBalance() == beforeSell + 82,
                  "卖售价 80 的西瓜 → 余额 +82（售价 80 + 奖励 2）");

            int beforeBatch = WatermelonCurrencyWallet.GetBalance();
            FarmModel batch = MakeFarmWithMelons(3, 80);
            batch.Sell(0); batch.Sell(0); batch.Sell(0);
            Check(WatermelonCurrencyWallet.GetBalance() == beforeBatch + 246,
                  "批量 3 个（售价合计 240）→ 余额 +246（奖励 3×2=6）");

            // ── F. Farm 主要消费走统一钱包 ──
            int beforeBuy = WatermelonCurrencyWallet.GetBalance();
            FarmModel shopper = MakeFarmWithMelons(0, 0);
            shopper.BuyFert();
            Check(WatermelonCurrencyWallet.GetBalance() == beforeBuy - FarmData.FERT_PRICE,
                  "买化肥从统一钱包扣 " + FarmData.FERT_PRICE);

            // ── G. Cut 评分等额发币口径 ──
            int[] scores = { 0, 60, 85, 100 };
            for (int i = 0; i < scores.Length; i++)
            {
                int before = WatermelonCurrencyWallet.GetBalance();
                WatermelonCurrencyWallet.Credit(scores[i], "cut_score_reward");
                Check(WatermelonCurrencyWallet.GetBalance() == before + scores[i],
                      "Cut " + scores[i] + " 分 → +" + scores[i] + " 西瓜币");
            }
            trace.AppendLine("说明 · 同局不重复发币由 CutSmokeTest 的结算按钮重复点击断言覆盖。");

            // ── H. ShardWallet 兼容层 ──
            Check(ShardWallet.Get(ShardWallet.WATERMELON) == WatermelonCurrencyWallet.GetBalance(),
                  "ShardWallet.Get 转发统一钱包");
            string exported = ShardWallet.Export();
            Check(exported.Contains("\"watermelon\"") && exported.Contains("\"version\":1"),
                  "兼容 JSON 导出保留 id=watermelon 与 version=1");
        }
        catch (Exception e)
        {
            errors.Add("自检异常：" + e.Message);
            trace.AppendLine("异常 · " + e);
        }
        finally
        {
            Restore(snapshot);
            trace.AppendLine("");
            trace.AppendLine("测试后已恢复：旧 Farm 存档 / 旧碎片 PP / 共享 JSON / 新钱包余额 / 迁移标记 / 订单 / 库存。");
        }

        trace.AppendLine("");
        trace.AppendLine("失败项数: " + errors.Count);
        for (int i = 0; i < errors.Count; i++) trace.AppendLine("  ! " + errors[i]);
        trace.AppendLine("结论: " + (errors.Count == 0 ? "全部通过" : "存在失败项"));

        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath()));
        File.WriteAllText(ResultPath(), trace.ToString(), new UTF8Encoding(false));
        if (errors.Count == 0) Debug.Log("[西瓜币自检] 全部通过，结果见 Logs/WatermelonCurrencySmokeResult.txt");
        else Debug.LogError("[西瓜币自检] 失败 " + errors.Count + " 项，结果见 Logs/WatermelonCurrencySmokeResult.txt");
    }
}
