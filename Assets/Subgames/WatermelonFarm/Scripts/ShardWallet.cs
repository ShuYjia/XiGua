using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WatermelonFarm
{
    // ══════════ 西瓜碎片钱包（兼容层） ══════════
    // 统一货币后，本项目只允许存在一份余额：WatermelonCurrencyWallet（西瓜币）。
    // 本类保留原有类名与全部公开 API，供旧编辑器工具 / Farm 冒烟测试继续调用，
    // 但底层读写全部转发到统一钱包，不再维护独立的 PlayerPrefs 与共享 JSON 余额。
    //
    // 旧 JSON 格式仍然兼容（读写迁移时按 id = "watermelon" 解析）：
    //   { "version": 1, "items": [ { "id": "watermelon", "count": 100 } ] }
    // Export() 也继续导出这个格式，保证外部工具无需改动。
    [Serializable]
    public sealed class ShardEntry
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class ShardWalletData
    {
        public int version = 1;
        public List<ShardEntry> items = new List<ShardEntry>();
    }

    public static class ShardWallet
    {
        public const string PREF_KEY = "pgz_shard_wallet_v1";
        public const string WATERMELON = "watermelon";
        public const int PER_MELON = 2;          // 每卖出 1 个西瓜额外获得的西瓜币数

        // 跨游戏共享的明文文件路径（保留供迁移读取与旧自检工具备份/恢复）
        public static string SharedFilePath
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

        static bool IsWatermelon(string id)
        {
            return string.IsNullOrEmpty(id) || id == WATERMELON;
        }

        static ShardWalletData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                ShardWalletData d = JsonUtility.FromJson<ShardWalletData>(json);
                if (d == null) return null;
                if (d.items == null) d.items = new List<ShardEntry>();
                return d;
            }
            catch (Exception) { return null; }
        }

        public static void Reload()
        {
            WatermelonCurrencyWallet.Reload();
        }

        public static int Get(string id)
        {
            if (!IsWatermelon(id)) return 0;
            return WatermelonCurrencyWallet.GetBalance();
        }

        public static int Count
        {
            get { return 1; }
        }

        /// <summary>返回变动后的西瓜币余额（按差额转发，绝不绕过统一钱包）。</summary>
        public static int Set(string id, int count)
        {
            if (!IsWatermelon(id)) return 0;
            int target = Mathf.Max(0, count);
            int current = WatermelonCurrencyWallet.GetBalance();
            int diff = target - current;
            if (diff > 0) return WatermelonCurrencyWallet.Credit(diff, "shard_wallet_set");
            if (diff < 0)
            {
                WatermelonCurrencyWallet.TrySpend(-diff, "shard_wallet_set");
                return WatermelonCurrencyWallet.GetBalance();
            }
            return current;
        }

        public static int Add(string id, int delta)
        {
            if (!IsWatermelon(id) || delta == 0) return Get(id);
            if (delta > 0) return WatermelonCurrencyWallet.Credit(delta, "shard_wallet_add");
            WatermelonCurrencyWallet.TrySpend(-delta, "shard_wallet_add");
            return Get(id);
        }

        public static void Remove(string id)
        {
            if (IsWatermelon(id)) Set(id, 0);
        }

        /// <summary>兼容 JSON 导出：格式与旧碎片钱包完全一致，余额来自统一钱包。</summary>
        public static string Export()
        {
            ShardWalletData d = new ShardWalletData();
            ShardEntry e = new ShardEntry();
            e.id = WATERMELON;
            e.count = WatermelonCurrencyWallet.GetBalance();
            d.items.Add(e);
            return JsonUtility.ToJson(d);
        }

        /// <summary>从其他游戏导入：只取较大值，绝不覆盖本地已获得的余额。</summary>
        public static bool ImportFrom(string json)
        {
            ShardWalletData other = Parse(json);
            if (other == null) return false;
            for (int i = 0; i < other.items.Count; i++)
            {
                ShardEntry e = other.items[i];
                if (e == null || !IsWatermelon(e.id)) continue;
                if (e.count > WatermelonCurrencyWallet.GetBalance()) Set(WATERMELON, e.count);
            }
            return true;
        }
    }
}
