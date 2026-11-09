using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>本地订单（全部字段按方案定义，只存本机 PlayerPrefs）。</summary>
[Serializable]
public class GiftOrder
{
    public string orderId;
    public string giftId;
    public string giftTitle;
    public int price;
    public string recipientName;
    public string phone;
    public string address;
    public string createdAt;
    public string status;
}

[Serializable]
class GiftOrderBook
{
    public List<GiftOrder> orders = new List<GiftOrder>();
}

public sealed class GiftRedeemResult
{
    public bool success;
    public string message;
    public GiftOrder order;
}

/// <summary>
/// 本地订单演示服务：无在线支付、无物流接口、无真实发货、无云端后台、无网络 API、无账号系统。
/// 订单只保存到本机 PlayerPrefs，状态固定「待审核」。
///
/// 创建顺序（严格）：礼品存在 -> 启用 -> 库存 -> 姓名 -> 手机号 -> 地址 -> 余额
///                -> 扣币 -> 扣库存 -> 生成订单号 -> 保存订单 -> 返回结果。
/// 保存订单失败时回滚西瓜币与礼品库存。
/// </summary>
public static class WatermelonGiftRedemptionService
{
    public const string OrdersPrefKey = "pgz_watermelon_gift_orders_v1";
    public const string StatusPending = "待审核";

    /// <summary>
    /// 仅供自检注入：返回 false 时模拟「保存订单失败」，用于验证回滚。
    /// 正常运行保持 null。
    /// </summary>
    public static Func<bool> SaveOrdersOverride;

    static GiftOrderBook book;

    public static GiftRedeemResult TryRedeem(string giftId, string recipientName, string phone, string address)
    {
        // 1) 礼品存在
        GiftDefinition gift = WatermelonGiftCatalog.Find(giftId);
        if (gift == null) return Fail("礼品不存在");

        // 2) 礼品启用
        if (!gift.enabled) return Fail("该礼品暂未开放兑换");

        // 3) 库存
        int stockBefore = WatermelonGiftCatalog.GetStock(giftId);
        if (stockBefore <= 0) return Fail("库存不足，无法兑换");

        // 4) 姓名
        string name = recipientName == null ? "" : recipientName.Trim();
        if (name.Length == 0) return Fail("请填写收货人姓名");

        // 5) 手机号
        string tel = phone == null ? "" : phone.Trim();
        if (!IsValidPhone(tel)) return Fail("手机号格式不正确");

        // 6) 地址
        string addr = address == null ? "" : address.Trim();
        if (addr.Length == 0) return Fail("请填写收货地址");

        // 7) 余额
        WatermelonCurrencyWallet.EnsureInitialized();
        if (WatermelonCurrencyWallet.GetBalance() < gift.price)
            return Fail("西瓜币不足，无法兑换");

        // 8) 扣币
        if (!WatermelonCurrencyWallet.TrySpend(gift.price, "gift_redeem:" + giftId))
            return Fail("西瓜币不足，无法兑换");

        // 9) 扣库存
        if (!WatermelonGiftCatalog.TryReserve(giftId, 1))
        {
            WatermelonCurrencyWallet.Credit(gift.price, "gift_rollback:" + giftId);
            return Fail("库存不足，无法兑换（已退回西瓜币）");
        }

        // 10) 生成订单号
        EnsureBook();
        string orderId = NextOrderId();

        GiftOrder order = new GiftOrder();
        order.orderId = orderId;
        order.giftId = giftId;
        order.giftTitle = gift.title;
        order.price = gift.price;
        order.recipientName = name;
        order.phone = tel;
        order.address = addr;
        order.createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        order.status = StatusPending;

        List<GiftOrder> orders = new List<GiftOrder>(book.orders);
        orders.Add(order);

        // 11) 保存订单；失败必须回滚币与库存
        if (!SaveOrders(orders))
        {
            WatermelonCurrencyWallet.Credit(gift.price, "gift_rollback:" + giftId);
            WatermelonGiftCatalog.SetStock(giftId, stockBefore);
            return Fail("订单保存失败，已回滚西瓜币与库存");
        }

        book.orders = orders;

        // 12) 成功
        GiftRedeemResult ok = new GiftRedeemResult();
        ok.success = true;
        ok.order = order;
        ok.message = "兑换成功，订单 " + orderId + " 已保存到本机（待审核）";
        return ok;
    }

    static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrEmpty(phone)) return false;
        return Regex.IsMatch(phone, "^1[3-9][0-9]{9}$");
    }

    static string NextOrderId()
    {
        string date = DateTime.Now.ToString("yyyyMMdd");
        HashSet<string> used = new HashSet<string>();
        for (int i = 0; i < book.orders.Count; i++)
            if (book.orders[i] != null && book.orders[i].orderId != null)
                used.Add(book.orders[i].orderId);

        int seq = 1;
        for (int i = 0; i < book.orders.Count; i++)
        {
            GiftOrder o = book.orders[i];
            if (o != null && !string.IsNullOrEmpty(o.orderId) && o.orderId.Contains(date)) seq++;
        }
        string id = "WM-" + date + "-" + seq.ToString("D4");
        int guard = 0;
        while (used.Contains(id) && guard < 10000)
        {
            seq++;
            id = "WM-" + date + "-" + seq.ToString("D4");
            guard++;
        }
        return id;
    }

    static bool SaveOrders(List<GiftOrder> orders)
    {
        if (SaveOrdersOverride != null && !SaveOrdersOverride()) return false;
        try
        {
            GiftOrderBook next = new GiftOrderBook();
            next.orders = orders;
            PlayerPrefs.SetString(OrdersPrefKey, JsonUtility.ToJson(next));
            PlayerPrefs.Save();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[礼品] 订单保存失败：" + e.Message);
            return false;
        }
    }

    static void EnsureBook()
    {
        if (book != null) return;
        book = new GiftOrderBook();
        string json = PlayerPrefs.GetString(OrdersPrefKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                GiftOrderBook loaded = JsonUtility.FromJson<GiftOrderBook>(json);
                if (loaded != null && loaded.orders != null) book = loaded;
            }
            catch (Exception) { book = new GiftOrderBook(); }
        }
    }

    public static List<GiftOrder> GetOrders()
    {
        EnsureBook();
        return new List<GiftOrder>(book.orders);
    }

    public static int OrderCount { get { EnsureBook(); return book.orders.Count; } }

    public static string ExportOrdersRaw()
    {
        EnsureBook();
        return JsonUtility.ToJson(book);
    }

    /// <summary>恢复整个订单账本（自检用，传空字符串表示删除该键）。</summary>
    public static void RestoreOrdersRaw(string json)
    {
        if (string.IsNullOrEmpty(json)) PlayerPrefs.DeleteKey(OrdersPrefKey);
        else PlayerPrefs.SetString(OrdersPrefKey, json);
        PlayerPrefs.Save();
        book = null;
        EnsureBook();
    }

    public static void Reload()
    {
        book = null;
        EnsureBook();
    }

    static GiftRedeemResult Fail(string message)
    {
        GiftRedeemResult r = new GiftRedeemResult();
        r.success = false;
        r.message = message;
        return r;
    }
}
