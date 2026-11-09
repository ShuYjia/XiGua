using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 全项目唯一的「西瓜币」钱包。Farm / Cut / 礼品兑换 / 主课堂都只读写这里。
///
/// 迁移规则（只执行一次）：
///   oldFarmCoins = pgz_farm_unity_v1 里的 coins
///   legacyShard  = max(pgz_shard_wallet_v1 的 watermelon 数量, 共享 JSON 的 watermelon 数量)
///   newBalance   = oldFarmCoins + legacyShard
///   三者都不存在时 newBalance = 100
///   迁移成功后写 pgz_watermelon_currency_migrated_v1 = 1，之后永不重复迁移。
///
/// 旧 PlayerPrefs 与旧 JSON 是同一碎片钱包的镜像，只取较大值一次，不重复相加。
/// </summary>
public static class WatermelonCurrencyWallet
{
    public const string CurrencyName = "西瓜币";
    public const string WalletPrefKey = "pgz_watermelon_currency_v2";
    public const string MigrationPrefKey = "pgz_watermelon_currency_migrated_v1";

    public const string FarmSaveKey = "pgz_farm_unity_v1";
    public const string ShardPrefKey = "pgz_shard_wallet_v1";
    public const string ShardItemId = "watermelon";
    public const int DefaultBalance = 100;

    public static event Action<int> BalanceChanged;

    static bool initialized;
    static readonly List<string> reasonLog = new List<string>();
    const int MaxReasonLog = 64;

    [Serializable]
    class LegacyFarmSave { public int coins; }

    [Serializable]
    class LegacyShardEntry { public string id; public int count; }

    [Serializable]
    class LegacyShardWallet { public int version; public List<LegacyShardEntry> items; }

    [Serializable]
    class WalletSnapshot
    {
        public string currency;
        public int balance;
        public bool migrated;
        public List<string> reasons;
    }

    /// <summary>共享碎片 JSON 路径（与 ShardWallet 完全一致）。</summary>
    public static string SharedShardFilePath
    {
        get
        {
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local))
                    return Path.Combine(Path.Combine(local, "PanggezhuangGames"), "shard_wallet.json");
            }
            catch (Exception) { }
            return Path.Combine(Application.persistentDataPath, "shard_wallet.json");
        }
    }

    /// <summary>确保钱包已初始化；未迁移过则执行一次迁移。</summary>
    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        if (PlayerPrefs.GetInt(MigrationPrefKey, 0) == 1)
        {
            AddReason("already_migrated");
            return;
        }

        bool hasLegacy = false;
        int oldFarmCoins = 0;
        int prefShard = 0;
        int jsonShard = 0;

        string farmJson = PlayerPrefs.GetString(FarmSaveKey, "");
        if (!string.IsNullOrEmpty(farmJson))
        {
            hasLegacy = true;
            oldFarmCoins = ParseFarmCoins(farmJson);
        }

        string shardPrefJson = PlayerPrefs.GetString(ShardPrefKey, "");
        if (!string.IsNullOrEmpty(shardPrefJson))
        {
            hasLegacy = true;
            prefShard = ParseShardCount(shardPrefJson);
        }

        try
        {
            string path = SharedShardFilePath;
            if (File.Exists(path))
            {
                hasLegacy = true;
                jsonShard = ParseShardCount(File.ReadAllText(path));
            }
        }
        catch (Exception e) { Debug.LogWarning("[西瓜币] 共享碎片文件读取失败：" + e.Message); }

        int legacyShard = Mathf.Max(prefShard, jsonShard);
        int balance = hasLegacy ? oldFarmCoins + legacyShard : DefaultBalance;
        balance = Mathf.Max(0, balance);

        PlayerPrefs.SetInt(WalletPrefKey, balance);
        PlayerPrefs.SetInt(MigrationPrefKey, 1);
        PlayerPrefs.Save();
        PersistLegacyMirror(balance);
        AddReason("migration coins=" + oldFarmCoins + " shards=" + legacyShard + " balance=" + balance);
    }

    static int ParseFarmCoins(string json)
    {
        try
        {
            LegacyFarmSave d = JsonUtility.FromJson<LegacyFarmSave>(json);
            return d == null ? 0 : Mathf.Max(0, d.coins);
        }
        catch (Exception) { return 0; }
    }

    static int ParseShardCount(string json)
    {
        try
        {
            LegacyShardWallet d = JsonUtility.FromJson<LegacyShardWallet>(json);
            if (d == null || d.items == null) return 0;
            for (int i = 0; i < d.items.Count; i++)
            {
                LegacyShardEntry e = d.items[i];
                if (e != null && e.id == ShardItemId) return Mathf.Max(0, e.count);
            }
        }
        catch (Exception) { }
        return 0;
    }

    public static int GetBalance()
    {
        EnsureInitialized();
        return Mathf.Max(0, PlayerPrefs.GetInt(WalletPrefKey, 0));
    }

    /// <summary>消费：先检查余额，成功立即落盘，金额不能为负。</summary>
    public static bool TrySpend(int amount, string reason)
    {
        if (amount < 0) return false;
        EnsureInitialized();
        int balance = GetBalance();
        if (balance < amount) return false;
        if (amount == 0) return true;

        int next = Mathf.Max(0, balance - amount);
        PlayerPrefs.SetInt(WalletPrefKey, next);
        PlayerPrefs.Save();
        PersistLegacyMirror(next);
        AddReason(reason + " -" + amount + " -> " + next);
        Raise(next);
        return true;
    }

    /// <summary>收入：成功立即落盘，金额不能为负，返回变动后的余额。</summary>
    public static int Credit(int amount, string reason)
    {
        if (amount < 0) return GetBalance();
        EnsureInitialized();
        int balance = GetBalance();
        if (amount == 0) return balance;

        int next = Mathf.Max(0, balance + amount);
        PlayerPrefs.SetInt(WalletPrefKey, next);
        PlayerPrefs.Save();
        PersistLegacyMirror(next);
        AddReason(reason + " +" + amount + " -> " + next);
        Raise(next);
        return next;
    }

    /// <summary>清缓存并从磁盘重读（自检 / 外部改动后同步）。</summary>
    public static void Reload()
    {
        initialized = false;
        EnsureInitialized();
    }

    /// <summary>导出钱包快照，包含余额、迁移状态与 reason 记录。</summary>
    public static string Export()
    {
        EnsureInitialized();
        WalletSnapshot s = new WalletSnapshot();
        s.currency = CurrencyName;
        s.balance = GetBalance();
        s.migrated = PlayerPrefs.GetInt(MigrationPrefKey, 0) == 1;
        s.reasons = new List<string>(reasonLog);
        return JsonUtility.ToJson(s);
    }

    public static string GetLastReason()
    {
        return reasonLog.Count == 0 ? "" : reasonLog[reasonLog.Count - 1];
    }

    static void AddReason(string line)
    {
        reasonLog.Add(line);
        if (reasonLog.Count > MaxReasonLog) reasonLog.RemoveAt(0);
    }

    static void Raise(int balance)
    {
        Action<int> handler = BalanceChanged;
        if (handler != null) handler(balance);
    }

    static void PersistLegacyMirror(int balance)
    {
        try
        {
            string dir = Path.GetDirectoryName(SharedShardFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string json = "{\"version\":1,\"items\":[{\"id\":\"watermelon\",\"count\":" + Mathf.Max(0, balance) + "}]}";
            File.WriteAllText(SharedShardFilePath, json, new System.Text.UTF8Encoding(false));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[西瓜币] 兼容 JSON 镜像写入失败：" + e.Message);
        }
    }
}
