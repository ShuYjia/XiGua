using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WatermelonFarm;

/* ============================================================
 * FarmSmokeTest.cs —— 导入主工程后的农场运行时冒烟测试（仅 Editor）
 * 菜单：Tools/瓜田农场/运行时冒烟测试
 * 自动打开 Assets/Scenes/Subgames/Farm.unity 并进入 Play Mode：
 *   新档 → 买种 → 八阶段生长 → 收获 → 存档往返 → 仓库出售（碎片 +2）
 *   → 离线推进（告警计时不累计）→ 场景内只有一个 FarmGame
 * 测试前备份、测试后恢复真实农场存档与共享碎片钱包，不污染用户数据。
 * ============================================================ */

[InitializeOnLoad]
public static class FarmSmokeTest
{
    const string FarmScenePath = "Assets/Scenes/Subgames/Farm.unity";
    const string BackupPathSuffix = "Logs/FarmSmokeBackup.json";
    const string MarkerSuffix = "Logs/FarmSmokeMarker";
    const string ResultSuffix = "Logs/FarmSmokeResult.txt";

    [Serializable]
    class Backup
    {
        public bool hadSave;
        public string save;
        public bool hadShard;
        public string shard;
        public bool hadWallet;
        public string wallet;
        public bool hadCurrency;
        public int currency;
        public bool hadMigrated;
        public int migrated;
        public bool hadOrders;
        public string orders;
        public bool hadStock;
        public string stock;
    }

    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder trace = new StringBuilder();
    static FarmGame game;
    static int stage, wait, frames, timeout;
    static bool finished;
    static bool needRestore;

    static string ProjectPath(string suffix)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", suffix));
    }

    static string MarkerPath() { return ProjectPath(MarkerSuffix); }
    static string ResultPath() { return ProjectPath(ResultSuffix); }
    static string BackupPath() { return ProjectPath(BackupPathSuffix); }

    static FarmSmokeTest()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            if (!msg.Contains("Licensing") && !msg.Contains("abort_threads")) errors.Add(msg);
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode && needRestore)
        {
            needRestore = false;
            RestoreBackups();
        }
    }

    [MenuItem("Tools/WatermelonFarm/Run Runtime Smoke Test")]
    public static void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath()));
        BackupData();
        if (File.Exists(ResultPath())) File.Delete(ResultPath());
        File.WriteAllText(MarkerPath(), "run", new UTF8Encoding(false));
        errors.Clear();
        EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void BackupData()
    {
        Backup b = new Backup();
        b.hadSave = PlayerPrefs.HasKey(FarmModel.SAVE_KEY);
        b.save = b.hadSave ? PlayerPrefs.GetString(FarmModel.SAVE_KEY, "") : "";
        b.hadShard = PlayerPrefs.HasKey(ShardWallet.PREF_KEY);
        b.shard = b.hadShard ? PlayerPrefs.GetString(ShardWallet.PREF_KEY, "") : "";
        try
        {
            b.hadWallet = File.Exists(ShardWallet.SharedFilePath);
            b.wallet = b.hadWallet ? File.ReadAllText(ShardWallet.SharedFilePath, Encoding.UTF8) : "";
        }
        catch (Exception) { b.hadWallet = false; b.wallet = ""; }
        b.hadCurrency = PlayerPrefs.HasKey(WatermelonCurrencyWallet.WalletPrefKey);
        b.currency = PlayerPrefs.GetInt(WatermelonCurrencyWallet.WalletPrefKey, 0);
        b.hadMigrated = PlayerPrefs.HasKey(WatermelonCurrencyWallet.MigrationPrefKey);
        b.migrated = PlayerPrefs.GetInt(WatermelonCurrencyWallet.MigrationPrefKey, 0);
        b.hadOrders = PlayerPrefs.HasKey(WatermelonGiftRedemptionService.OrdersPrefKey);
        b.orders = b.hadOrders ? PlayerPrefs.GetString(WatermelonGiftRedemptionService.OrdersPrefKey, "") : "";
        b.hadStock = PlayerPrefs.HasKey(WatermelonGiftCatalog.StockPrefKey);
        b.stock = b.hadStock ? PlayerPrefs.GetString(WatermelonGiftCatalog.StockPrefKey, "") : "";
        File.WriteAllText(BackupPath(), JsonUtility.ToJson(b), new UTF8Encoding(false));
    }

    static void RestoreBackups()
    {
        try
        {
            if (!File.Exists(BackupPath())) return;
            Backup b = JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath(), Encoding.UTF8));
            if (b == null) return;
            if (b.hadSave) PlayerPrefs.SetString(FarmModel.SAVE_KEY, b.save);
            else PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
            if (b.hadShard) PlayerPrefs.SetString(ShardWallet.PREF_KEY, b.shard);
            else PlayerPrefs.DeleteKey(ShardWallet.PREF_KEY);
            if (b.hadCurrency) PlayerPrefs.SetInt(WatermelonCurrencyWallet.WalletPrefKey, b.currency);
            else PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.WalletPrefKey);
            if (b.hadMigrated) PlayerPrefs.SetInt(WatermelonCurrencyWallet.MigrationPrefKey, b.migrated);
            else PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.MigrationPrefKey);
            if (b.hadOrders) PlayerPrefs.SetString(WatermelonGiftRedemptionService.OrdersPrefKey, b.orders);
            else PlayerPrefs.DeleteKey(WatermelonGiftRedemptionService.OrdersPrefKey);
            if (b.hadStock) PlayerPrefs.SetString(WatermelonGiftCatalog.StockPrefKey, b.stock);
            else PlayerPrefs.DeleteKey(WatermelonGiftCatalog.StockPrefKey);
            PlayerPrefs.Save();
            WatermelonGiftCatalog.Reload();
            WatermelonGiftRedemptionService.Reload();
            try
            {
                if (b.hadWallet) File.WriteAllText(ShardWallet.SharedFilePath, b.wallet, new UTF8Encoding(false));
                else if (File.Exists(ShardWallet.SharedFilePath)) File.Delete(ShardWallet.SharedFilePath);
            }
            catch (Exception e) { Debug.LogWarning("农场冒烟测试：恢复共享碎片文件失败 " + e.Message); }
            // 不调用 ShardWallet.Reload()：它转发到钱包 Reload，
            // 在「原本无钱包键」时会重新执行迁移、污染真实数据。
            File.Delete(BackupPath());
            Debug.Log("[瓜田农场] 冒烟测试已恢复真实存档、西瓜币与礼品订单。");
        }
        catch (Exception e) { Debug.LogError("农场冒烟测试恢复失败：" + e.Message); }
    }

    static void Say(string s) { trace.AppendLine("  " + s); }

    static void Check(bool ok, string what)
    {
        Say((ok ? "通过 · " : "失败 · ") + what);
        if (!ok) errors.Add(what);
    }

    static void Tick()
    {
        if (finished) return;
        if (!File.Exists(MarkerPath())) return;
        if (!EditorApplication.isPlaying) return;
        if (frames == 0)
        {
            trace.Length = 0;
            trace.AppendLine("《瓜田农场》Unity 运行时冒烟测试");
            trace.AppendLine("Unity " + Application.unityVersion);
        }
        frames++;
        if (++timeout > 20000) { Finish("超时"); return; }
        if (game == null)
        {
            game = UnityEngine.Object.FindObjectOfType<FarmGame>();
            if (game == null) return;
        }
        if (game.Model == null || game.Model.P == null) return;
        if (wait > 0) { wait--; return; }

        switch (stage)
        {
            case 0:
                RunChecks();
                stage = 1; wait = 5; break;
            case 1:
                Check(PlayerPrefs.HasKey(FarmModel.SAVE_KEY), "自动保存写入 Farm 存档键");
                stage = 2; wait = 5; break;
            case 2:
                Check(UnityEngine.Object.FindObjectsOfType<FarmGame>().Length == 1, "Farm 场景只有一个 FarmGame 总控");
                Check(UnityEngine.Object.FindObjectsOfType<GuiliGame>().Length == 0, "Farm 场景没有误建切瓜总控");
                Check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length <= 1, "Farm 场景没有重复 EventSystem");
                Check(Camera.main != null, "Farm 场景存在主相机");
                Finish(null);
                break;
        }
    }

    static void RunChecks()
    {
        game.ResetSave();
        FarmModel m = game.Model;
        SaveData P = m.P;

        Check(P.plotsOwned == 4 && P.plots.Count == 9,
            "新档初始状态：4 块地 / 共 9 块（余额由统一钱包管理）");
        Check(FarmData.V.Length == 3 && FarmData.ST.Length == 8,
            "品种 3 种 / 生长阶段 8 档");
        Check(FarmData.SPEEDS.Length == 2 && Mathf.Abs(FarmData.SPEEDS[1] - 2f) < 0.01f,
            "倍速仅 1x / 2x");
        Check(FarmData.V[0].id == "jx" && FarmData.V[1].id == "hx" && FarmData.V[2].id == "jxiu",
            "京欣1号 / 航兴1号 / 京秀 品种齐全");

        // 买种：京欣1号，消耗统一钱包
        int balanceBeforeSeed = WatermelonCurrencyWallet.GetBalance();
        m.BuySeed("jx", 0);
        PlotData pl = P.plots[0];
        Check(WatermelonCurrencyWallet.GetBalance() == balanceBeforeSeed - FarmData.V[0].seed &&
              pl.v == "jx" && pl.st == 0, "买种并种下京欣1号（消耗西瓜币）");

        // 预置农事决策，避免冒烟流程被疏果/控水弹窗打断
        pl.pol = true;
        pl.askThin = true;
        pl.thin = 1;
        pl.askDry = true;
        pl.dry = true;
        pl.care = 100;

        int ticks = 0;
        while (pl.st < FarmData.RIPE_ST && ticks < 500)
        {
            pl.water = 80f;
            pl.weed = false;
            pl.pest = "";
            m.TickOnce();
            ticks++;
        }
        Check(pl.st == FarmData.RIPE_ST, "八阶段生长至成熟（" + ticks + " tick）");
        Check(pl.water > 0 && pl.danger == 0f, "生长期间无告警计时残留");

        int beforeCoins = WatermelonCurrencyWallet.GetBalance();
        m.Harvest(pl);
        Check(P.wh.Count == 1, "成熟收获进入仓库 1 个瓜");
        Check(P.GetCard("jx") != null, "收获解锁京欣1号品种收藏卡");
        WarehouseItem item = P.wh[0];
        Check(item.coins > 0 && item.q >= 0 && item.brix > 0 && item.wt > 0,
            "结算品质 / 糖度 / 重量 / 售价：" + item.q + " / " + item.brix + "° / " + item.wt + "kg / " + item.coins + "金");

        // 存档往返
        m.Save(true);
        FarmModel loaded = new FarmModel();
        loaded.view = new NullFarmView();
        loaded.Init();
        Check(loaded.P.wh.Count == 1 && loaded.P.GetCard("jx") != null,
            "存档往返：仓库 / 图鉴完整（余额由统一钱包保存）");

        // 仓库出售：售价 + 每瓜 2 西瓜币，只入账一次
        int balanceBeforeSell = WatermelonCurrencyWallet.GetBalance();
        int sellCoins = loaded.P.wh[0].coins;
        int expectedGain = sellCoins + ShardWallet.PER_MELON;
        loaded.Sell(0);
        Check(WatermelonCurrencyWallet.GetBalance() == balanceBeforeSell + expectedGain,
            "出售西瓜币到账（售价 " + sellCoins + " + 奖励 " + ShardWallet.PER_MELON +
            " = " + expectedGain + "，单次入账）");
        Check(loaded.P.wh.Count == 0, "出售完成后仓库清空");

        loaded.Save(true);
        FarmModel reloaded = new FarmModel();
        reloaded.view = new NullFarmView();
        reloaded.Init();
        Check(reloaded.P.wh.Count == 0 &&
              WatermelonCurrencyWallet.GetBalance() == balanceBeforeSell + expectedGain,
            "出售后的西瓜币余额 / 仓库持久化");

        // 离线推进：2 小时前保存、地块处于发芽阶段
        SaveData off = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(reloaded.P));
        PlotData offPlot = off.plots[1];
        offPlot.v = "jx";
        offPlot.st = 1f;
        offPlot.prog = 0f;
        offPlot.water = 100f;
        offPlot.pol = true;
        offPlot.askThin = true;
        offPlot.thin = 1;
        offPlot.askDry = true;
        offPlot.care = 100;
        offPlot.danger = 5f;
        offPlot.dangerCause = "有草";
        off.t = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds
                 - 2L * 3600L * 1000L).ToString();
        PlayerPrefs.SetString(FarmModel.SAVE_KEY, JsonUtility.ToJson(off));
        PlayerPrefs.Save();
        FarmModel offline = new FarmModel();
        offline.view = new NullFarmView();
        offline.Init();
        Check(offline.P.plots[1].st >= 2f, "离线 2 小时推进生长（st=" + offline.P.plots[1].st + "）");
        Check(offline.P.plots[1].danger == 0f, "离线期间不累计告警计时");
    }

    static void Finish(string failure)
    {
        if (finished) return;
        finished = true;
        trace.AppendLine("");
        if (failure != null) trace.AppendLine("失败: " + failure);
        trace.AppendLine("运行时错误数: " + errors.Count);
        for (int i = 0; i < errors.Count && i < 30; i++) trace.AppendLine("  ! " + errors[i]);
        trace.AppendLine("总帧数: " + frames);
        trace.AppendLine("结论: " + (failure == null && errors.Count == 0 ? "全流程通过" : "存在异常"));
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath()));
        File.WriteAllText(ResultPath(), trace.ToString(), new UTF8Encoding(false));
        if (File.Exists(MarkerPath())) File.Delete(MarkerPath());
        Debug.Log("[瓜田农场] 冒烟测试结束: " + (failure == null && errors.Count == 0 ? "通过" : "有异常"));
        needRestore = true;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode)
            EditorApplication.Exit(failure == null && errors.Count == 0 ? 0 : 1);
    }

    sealed class NullFarmView : IFarmView
    {
        public void Render() { }
        public void AddLog(string type, string text) { }
        public void ShowModal(ModalSpec spec) { }
        public void CloseTopModal() { }
        public void ShowShardToast(int gained, int total) { }
        public void ShowWiltToast(string variety, string cause) { }
    }
}
