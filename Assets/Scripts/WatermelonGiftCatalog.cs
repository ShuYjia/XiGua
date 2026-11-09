using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>礼品定义。价格与库存集中配置在 WatermelonGiftCatalog.Defaults，不得散落在 UI 文件里。</summary>
[Serializable]
public class GiftDefinition
{
    public string id;
    public string title;
    public string description;
    public int price;
    public int stock;
    public string imageResource;
    public bool requiresShipping;
    public bool enabled;
}

[Serializable]
class GiftStockEntry
{
    public string id;
    public int stock;
}

[Serializable]
class GiftStockBook
{
    public List<GiftStockEntry> items = new List<GiftStockEntry>();
}

/// <summary>
/// 礼品目录 + 本机库存。库存以 PlayerPrefs 持久化，缺省时使用目录里的初始库存，
/// 首次兑换才落盘，避免污染用户真实数据。
/// </summary>
public static class WatermelonGiftCatalog
{
    public const string StockPrefKey = "pgz_watermelon_gift_stock_v1";

    static readonly GiftDefinition[] Defaults = new GiftDefinition[]
    {
        new GiftDefinition {
            id = "gift_postcard_set", title = "西瓜科普明信片套装",
            description = "六张手绘西瓜科普明信片，认识西瓜从外形到剖面。",
            price = 30, stock = 100, imageResource = "", requiresShipping = true, enabled = true },
        new GiftDefinition {
            id = "gift_sticker_pack", title = "西瓜主题贴纸包",
            description = "西瓜主题贴纸一包，适合贴在笔记本与水杯上。",
            price = 50, stock = 100, imageResource = "", requiresShipping = true, enabled = true },
        new GiftDefinition {
            id = "gift_badge", title = "西瓜科普纪念徽章",
            description = "金属纪念徽章一枚，西瓜小课堂限定图案。",
            price = 80, stock = 50, imageResource = "", requiresShipping = true, enabled = true },
        new GiftDefinition {
            id = "gift_canvas_bag", title = "西瓜主题帆布袋",
            description = "加厚帆布袋一只，可装下课堂讲义与一整个夏天。",
            price = 150, stock = 30, imageResource = "", requiresShipping = true, enabled = true },
    };

    static GiftStockBook stockBook;

    /// <summary>返回目录副本，避免外部改动默认配置。</summary>
    public static GiftDefinition[] All
    {
        get
        {
            GiftDefinition[] copy = new GiftDefinition[Defaults.Length];
            for (int i = 0; i < Defaults.Length; i++)
            {
                GiftDefinition src = Defaults[i];
                copy[i] = new GiftDefinition {
                    id = src.id, title = src.title, description = src.description,
                    price = src.price, stock = src.stock, imageResource = src.imageResource,
                    requiresShipping = src.requiresShipping, enabled = src.enabled };
            }
            return copy;
        }
    }

    public static int Count { get { return Defaults.Length; } }

    public static GiftDefinition Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < Defaults.Length; i++)
            if (Defaults[i].id == id) return Defaults[i];
        return null;
    }

    static void EnsureBook()
    {
        if (stockBook != null) return;
        stockBook = new GiftStockBook();
        string json = PlayerPrefs.GetString(StockPrefKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                GiftStockBook loaded = JsonUtility.FromJson<GiftStockBook>(json);
                if (loaded != null && loaded.items != null) stockBook = loaded;
            }
            catch (Exception) { stockBook = new GiftStockBook(); }
        }
    }

    static GiftStockEntry Entry(string id)
    {
        EnsureBook();
        for (int i = 0; i < stockBook.items.Count; i++)
        {
            GiftStockEntry e = stockBook.items[i];
            if (e != null && e.id == id) return e;
        }
        return null;
    }

    public static int GetStock(string id)
    {
        GiftDefinition def = Find(id);
        if (def == null) return 0;
        GiftStockEntry e = Entry(id);
        return e == null ? def.stock : Mathf.Max(0, e.stock);
    }

    /// <summary>直接设置库存（供回滚与自检恢复使用）。</summary>
    public static void SetStock(string id, int value)
    {
        if (Find(id) == null) return;
        EnsureBook();
        GiftStockEntry e = Entry(id);
        if (e == null)
        {
            e = new GiftStockEntry();
            e.id = id;
            stockBook.items.Add(e);
        }
        e.stock = Mathf.Max(0, value);
        Persist();
    }

    /// <summary>扣减库存；库存不足返回 false。</summary>
    public static bool TryReserve(string id, int count)
    {
        if (count <= 0) return false;
        int current = GetStock(id);
        if (current < count) return false;
        SetStock(id, current - count);
        return true;
    }

    public static string ExportStock()
    {
        EnsureBook();
        return JsonUtility.ToJson(stockBook);
    }

    /// <summary>恢复整个库存账本（自检用，传空字符串表示删除该键）。</summary>
    public static void RestoreStock(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            PlayerPrefs.DeleteKey(StockPrefKey);
        }
        else
        {
            PlayerPrefs.SetString(StockPrefKey, json);
        }
        PlayerPrefs.Save();
        stockBook = null;
        EnsureBook();
    }

    public static void Reload()
    {
        stockBook = null;
        EnsureBook();
    }

    static void Persist()
    {
        try
        {
            PlayerPrefs.SetString(StockPrefKey, JsonUtility.ToJson(stockBook));
            PlayerPrefs.Save();
        }
        catch (Exception e) { Debug.LogWarning("[礼品] 库存写入失败：" + e.Message); }
    }
}
