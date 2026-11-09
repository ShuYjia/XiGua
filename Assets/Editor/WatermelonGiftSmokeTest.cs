using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 礼品兑换本地订单自检（纯编辑器、同步执行，不进入 Play Mode）。
/// 菜单：Watermelon Science/Run Gift Smoke Test
/// 覆盖：目录、余额不足、库存为 0、姓名/手机号/地址校验、扣币扣库存、
///       订单号唯一、状态待审核、重启持久化、保存失败回滚、三处入口。
/// 全部使用虚拟收货信息；测试前完整备份、测试后恢复所有相关 PlayerPrefs 与共享 JSON。
/// </summary>
public static class WatermelonGiftSmokeTest
{
    const string VirtualName = "测试虚拟人";
    const string VirtualPhone = "13800000000";
    const string VirtualAddress = "虚拟市科普路 1 号";

    sealed class Snapshot
    {
        public bool hadFarm, hadShard, hadWallet, hadMigrated, hadOrders, hadStock, hadJson;
        public string farm, shard, orders, stock, json;
        public int wallet, migrated;
    }

    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder trace = new StringBuilder();

    static string SharedJsonPath { get { return WatermelonCurrencyWallet.SharedShardFilePath; } }

    static string ResultPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs",
                                            "WatermelonGiftSmokeResult.txt"));
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
            if (s.hadJson) File.WriteAllText(SharedJsonPath, s.json, new UTF8Encoding(false));
            else if (File.Exists(SharedJsonPath)) File.Delete(SharedJsonPath);
        }
        catch (Exception e) { Debug.LogWarning("[礼品自检] 恢复共享 JSON 失败：" + e.Message); }

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
        WatermelonGiftRedemptionService.SaveOrdersOverride = null;
    }

    static void SetBalance(int value)
    {
        PlayerPrefs.SetInt(WatermelonCurrencyWallet.WalletPrefKey, value);
        PlayerPrefs.SetInt(WatermelonCurrencyWallet.MigrationPrefKey, 1);
        PlayerPrefs.Save();
        ResetWalletCache();
    }

    static string Source(string relative)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
    }

    [MenuItem("Watermelon Science/Run Gift Smoke Test")]
    public static void Run()
    {
        Snapshot snapshot = Capture();
        errors.Clear();
        trace.Length = 0;
        trace.AppendLine("礼品兑换 · 本地订单演示自检");
        trace.AppendLine("Unity " + Application.unityVersion);
        trace.AppendLine("时间 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        trace.AppendLine("提示 · " + WatermelonGiftPanel.DemoNotice);
        trace.AppendLine("");

        try
        {
            ClearAll();
            ResetWalletCache();

            // ── A. 目录集中配置 ──
            GiftDefinition[] all = WatermelonGiftCatalog.All;
            Check(all.Length == 4, "礼品目录 4 项");
            Check(WatermelonGiftCatalog.Find("gift_postcard_set") != null &&
                  WatermelonGiftCatalog.Find("gift_postcard_set").price == 30 &&
                  WatermelonGiftCatalog.GetStock("gift_postcard_set") == 100,
                  "明信片套装 30 西瓜币 / 库存 100");
            Check(WatermelonGiftCatalog.Find("gift_canvas_bag") != null &&
                  WatermelonGiftCatalog.Find("gift_canvas_bag").price == 150 &&
                  WatermelonGiftCatalog.GetStock("gift_canvas_bag") == 30,
                  "帆布袋 150 西瓜币 / 库存 30");
            Check(WatermelonGiftCatalog.Find("no_such_gift") == null, "不存在的礼品返回 null");

            // ── B. 各类校验失败 ──
            SetBalance(1000);
            Check(!WatermelonGiftRedemptionService.TryRedeem("no_such_gift",
                VirtualName, VirtualPhone, VirtualAddress).success, "礼品不存在不能兑换");
            Check(!WatermelonGiftRedemptionService.TryRedeem("gift_badge",
                "", VirtualPhone, VirtualAddress).success, "姓名为空不能提交");
            Check(!WatermelonGiftRedemptionService.TryRedeem("gift_badge",
                VirtualName, "12345", VirtualAddress).success, "手机号格式错误不能提交");
            Check(!WatermelonGiftRedemptionService.TryRedeem("gift_badge",
                VirtualName, VirtualPhone, "  ").success, "地址为空不能提交");

            SetBalance(10);
            Check(!WatermelonGiftRedemptionService.TryRedeem("gift_badge",
                VirtualName, VirtualPhone, VirtualAddress).success, "余额不足不能兑换");
            Check(WatermelonCurrencyWallet.GetBalance() == 10, "余额不足时余额不变");

            WatermelonGiftCatalog.SetStock("gift_badge", 0);
            SetBalance(1000);
            Check(!WatermelonGiftRedemptionService.TryRedeem("gift_badge",
                VirtualName, VirtualPhone, VirtualAddress).success, "库存为 0 不能兑换");
            WatermelonGiftCatalog.RestoreStock("");
            WatermelonGiftCatalog.Reload();

            // ── C. 兑换成功 ──
            SetBalance(1000);
            int stockBefore = WatermelonGiftCatalog.GetStock("gift_postcard_set");
            GiftRedeemResult ok = WatermelonGiftRedemptionService.TryRedeem(
                "gift_postcard_set", VirtualName, VirtualPhone, VirtualAddress);
            Check(ok.success && ok.order != null, "兑换成功返回订单");
            Check(WatermelonCurrencyWallet.GetBalance() == 1000 - 30,
                  "兑换成功扣除 30 西瓜币");
            Check(WatermelonGiftCatalog.GetStock("gift_postcard_set") == stockBefore - 1,
                  "兑换成功减少库存 1");
            Check(ok.order.status == "待审核", "订单状态为「待审核」");
            Check(ok.order.orderId != null && ok.order.orderId.StartsWith("WM-"),
                  "订单号格式 WM-YYYYMMDD-XXXX：" + ok.order.orderId);
            Check(ok.order.giftId == "gift_postcard_set" && ok.order.price == 30 &&
                  ok.order.recipientName == VirtualName && ok.order.phone == VirtualPhone &&
                  ok.order.address == VirtualAddress, "订单字段完整");

            GiftRedeemResult ok2 = WatermelonGiftRedemptionService.TryRedeem(
                "gift_postcard_set", VirtualName, VirtualPhone, VirtualAddress);
            Check(ok2.success && ok2.order.orderId != ok.order.orderId,
                  "连续两次兑换订单号唯一：" + ok.order.orderId + " / " + ok2.order.orderId);

            // ── D. 重启持久化 ──
            int countBefore = WatermelonGiftRedemptionService.OrderCount;
            string raw = WatermelonGiftRedemptionService.ExportOrdersRaw();
            WatermelonGiftRedemptionService.RestoreOrdersRaw(raw);
            WatermelonGiftRedemptionService.Reload();
            Check(WatermelonGiftRedemptionService.OrderCount == countBefore &&
                  countBefore == 2, "订单保存到本机 PlayerPrefs，重载后仍存在（" + countBefore + " 笔）");
            List<GiftOrder> orders = WatermelonGiftRedemptionService.GetOrders();
            Check(orders.Count == 2 && orders[0].status == "待审核" && orders[1].status == "待审核",
                  "重载后订单状态仍为「待审核」");

            // ── E. 保存订单失败回滚 ──
            int balanceBefore = WatermelonCurrencyWallet.GetBalance();
            int badgeStockBefore = WatermelonGiftCatalog.GetStock("gift_badge");
            int orderCountBefore = WatermelonGiftRedemptionService.OrderCount;
            WatermelonGiftRedemptionService.SaveOrdersOverride = delegate { return false; };
            GiftRedeemResult failed = WatermelonGiftRedemptionService.TryRedeem(
                "gift_badge", VirtualName, VirtualPhone, VirtualAddress);
            WatermelonGiftRedemptionService.SaveOrdersOverride = null;
            Check(!failed.success, "保存订单失败时返回失败结果");
            Check(WatermelonCurrencyWallet.GetBalance() == balanceBefore,
                  "失败时回滚西瓜币（" + balanceBefore + "）");
            Check(WatermelonGiftCatalog.GetStock("gift_badge") == badgeStockBefore,
                  "失败时回滚礼品库存（" + badgeStockBefore + "）");
            Check(WatermelonGiftRedemptionService.OrderCount == orderCountBefore,
                  "失败时不新增订单");

            // ── F. 三处入口共用同一服务 ──
            Check(typeof(WatermelonGiftPanel) != null && typeof(WatermelonCurrencyHUD) != null,
                  "共享兑换面板与统一 HUD 组件已编译");
            string setup = File.ReadAllText(Source("Editor/WatermelonProjectSetup.cs"));
            string farmUI = File.ReadAllText(Source("Subgames/WatermelonFarm/Scripts/FarmUI.cs"));
            string cutUI = File.ReadAllText(Source("Subgames/WatermelonCut/Scripts/GuiliUI.cs"));
            Check(setup.Contains("GiftRedeemButton") && setup.Contains("WatermelonCurrencyHUD"),
                  "主课堂生成脚本含礼品兑换入口");
            Check(farmUI.Contains("GiftRedeemBtn") && farmUI.Contains("WatermelonGiftPanel.Open"),
                  "Farm 顶栏含礼品兑换入口");
            Check(cutUI.Contains("兑换钮") && cutUI.Contains("WatermelonGiftPanel.Open"),
                  "Cut 顶栏含礼品兑换入口");
        }
        catch (Exception e)
        {
            errors.Add("自检异常：" + e.Message);
            trace.AppendLine("异常 · " + e);
        }
        finally
        {
            WatermelonGiftRedemptionService.SaveOrdersOverride = null;
            Restore(snapshot);
            trace.AppendLine("");
            trace.AppendLine("测试后已恢复：新钱包余额 / 旧 Farm 存档 / 旧碎片 PP / 共享 JSON / 礼品订单 / 礼品库存 / 迁移标记。");
        }

        trace.AppendLine("");
        trace.AppendLine("失败项数: " + errors.Count);
        for (int i = 0; i < errors.Count; i++) trace.AppendLine("  ! " + errors[i]);
        trace.AppendLine("结论: " + (errors.Count == 0 ? "全部通过" : "存在失败项"));

        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath()));
        File.WriteAllText(ResultPath(), trace.ToString(), new UTF8Encoding(false));
        if (errors.Count == 0) Debug.Log("[礼品自检] 全部通过，结果见 Logs/WatermelonGiftSmokeResult.txt");
        else Debug.LogError("[礼品自检] 失败 " + errors.Count + " 项，结果见 Logs/WatermelonGiftSmokeResult.txt");
    }
}
