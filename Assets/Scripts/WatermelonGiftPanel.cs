using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 常驻余额 HUD：主课堂（编辑器期生成）与 Farm / Cut（运行期生成）共用同一个组件。
/// 订阅统一钱包的 BalanceChanged，保证三处余额始终一致。
/// </summary>
public class WatermelonCurrencyHUD : MonoBehaviour
{
    public Text balanceText;
    public Button giftButton;
    public Transform panelParent;
    bool bound;

    void Awake()
    {
        WatermelonCurrencyWallet.EnsureInitialized();
        Refresh();
    }

    void OnEnable()
    {
        WatermelonCurrencyWallet.BalanceChanged += OnBalanceChanged;
        Refresh();
    }

    void OnDisable()
    {
        WatermelonCurrencyWallet.BalanceChanged -= OnBalanceChanged;
    }

    void OnDestroy()
    {
        WatermelonCurrencyWallet.BalanceChanged -= OnBalanceChanged;
        if (giftButton != null) giftButton.onClick.RemoveListener(OpenGiftPanel);
    }

    void OnBalanceChanged(int balance) { Refresh(); }

    public void Refresh()
    {
        // 运行期构建的 UI 可能在 AddComponent 之后才接线，这里做幂等绑定
        if (!bound && giftButton != null)
        {
            giftButton.onClick.RemoveListener(OpenGiftPanel);
            giftButton.onClick.AddListener(OpenGiftPanel);
            bound = true;
        }
        if (balanceText != null)
            balanceText.text = WatermelonCurrencyWallet.CurrencyName + " " + WatermelonCurrencyWallet.GetBalance();
    }

    public void OpenGiftPanel()
    {
        WatermelonGiftPanel.Open(panelParent != null ? panelParent : transform);
    }
}

/// <summary>
/// 礼品兑换共享面板（纯本地订单演示）。
/// 三处入口都调用 WatermelonGiftPanel.Open，共用钱包 / 目录 / 订单服务。
/// </summary>
public class WatermelonGiftPanel : MonoBehaviour
{
    public const string DemoNotice =
        "当前为本地订单演示。订单会保存到本机并标记为「待审核」，不会自动连接支付、物流或真实发货系统。";

    static WatermelonGiftPanel current;
    public static bool IsOpen { get { return current != null; } }

    Font font;
    Text balanceText, statusText, orderListText;
    readonly Text[] cardStock = new Text[4];
    readonly Button[] cardButtons = new Button[4];
    GiftDefinition[] defs;
    InputField nameField, phoneField, addressField;
    string selectedGiftId = "";

    public static void Open(Transform parent)
    {
        if (current != null) { current.Refresh(); return; }
        if (parent == null) return;
        GameObject go = new GameObject("WatermelonGiftPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetAsLastSibling();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        current = go.AddComponent<WatermelonGiftPanel>();
        current.Build();
    }

    public static void CloseAny()
    {
        if (current != null) current.Close();
    }

    public void Close()
    {
        if (current == this) current = null;
        Destroy(gameObject);
    }

    static Font CreateFont()
    {
        try
        {
            Font f = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 20);
            if (f != null) return f;
        }
        catch (Exception) { }
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch (Exception) { return null; }
    }

    static readonly Color Paper = new Color32(255, 248, 232, 255);
    static readonly Color DeepGreen = new Color32(24, 91, 62, 255);
    static readonly Color Ink = new Color32(37, 54, 45, 255);
    static readonly Color CardBg = new Color32(246, 240, 220, 255);
    static readonly Color Accent = new Color32(100, 183, 91, 255);
    static readonly Color Red = new Color32(159, 75, 59, 255);

    RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        Image im = go.GetComponent<Image>();
        im.color = color;
        im.raycastTarget = raycast;
        return r;
    }

    Text Label(Transform parent, string name, float x, float y, float w, float h,
               string value, int size, Color color, TextAnchor align, bool bold = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(x, y);
        r.sizeDelta = new Vector2(w, h);
        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.text = value;
        t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    Button MakeButton(Transform parent, string name, float x, float y, float w, float h,
                      string text, Color bg, Action onClick)
    {
        RectTransform r = Box(parent, name, x, y, w, h, bg, true);
        Button b = r.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        b.colors = cb;
        Label(r, "Label", 0, 0, w - 8, h - 6, text, 19, Color.white, TextAnchor.MiddleCenter, true);
        if (onClick != null) b.onClick.AddListener(delegate { onClick(); });
        return b;
    }

    InputField MakeInput(Transform parent, string name, float x, float y, float w, float h, string placeholder)
    {
        RectTransform r = Box(parent, name, x, y, w, h, Color.white, true);
        r.GetComponent<Image>().color = new Color32(255, 255, 255, 255);
        InputField f = r.gameObject.AddComponent<InputField>();
        f.targetGraphic = r.GetComponent<Image>();

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(r, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12, 4); tr.offsetMax = new Vector2(-12, -4);
        Text t = textGo.GetComponent<Text>();
        t.font = font; t.fontSize = 19; t.color = Ink;
        t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        f.textComponent = t;

        GameObject phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phGo.transform.SetParent(r, false);
        RectTransform pr = phGo.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = new Vector2(12, 4); pr.offsetMax = new Vector2(-12, -4);
        Text ph = phGo.GetComponent<Text>();
        ph.font = font; ph.fontSize = 18; ph.color = new Color(0.55f, 0.58f, 0.55f, 1f);
        ph.alignment = TextAnchor.MiddleLeft; ph.raycastTarget = false;
        ph.text = placeholder;
        f.placeholder = ph;
        f.lineType = InputField.LineType.SingleLine;
        f.characterLimit = 120;
        return f;
    }

    void Build()
    {
        font = CreateFont();
        defs = WatermelonGiftCatalog.All;

        // 全屏遮罩（点击关闭）
        RectTransform mask = Box(transform, "Mask", 0, 0, 3000, 2000,
                                 new Color(0.08f, 0.12f, 0.10f, 0.62f), true);
        mask.anchorMin = Vector2.zero; mask.anchorMax = Vector2.one;
        mask.offsetMin = Vector2.zero; mask.offsetMax = Vector2.zero;
        Button maskBtn = mask.gameObject.AddComponent<Button>();
        maskBtn.onClick.AddListener(delegate { Close(); });

        RectTransform card = Box(transform, "GiftCard", 0, 0, 1400, 960, Paper, true);
        Label(card, "Title", 0, 420, 1200, 58, "礼品兑换", 40, DeepGreen, TextAnchor.MiddleCenter, true);
        balanceText = Label(card, "Balance", 0, 370, 1200, 44, "", 26, DeepGreen, TextAnchor.MiddleCenter, true);
        // 演示说明放在卡片底部作脚注：原先 y=315 会与左右两栏栏目标题（y=320）处在同一行而重叠
        Label(card, "DemoNotice", 0, -370, 1280, 36, DemoNotice, 16, Red, TextAnchor.MiddleCenter);

        BuildCatalog(card);
        BuildForm(card);

        MakeButton(card, "CloseButton", 600, -430, 160, 48, "关闭", DeepGreen, Close);
        Refresh();
    }

    void BuildCatalog(RectTransform card)
    {
        RectTransform left = Box(card, "CatalogArea", -340, 0, 640, 700,
                                 new Color(0f, 0f, 0f, 0f), false);
        Label(left, "CatalogTitle", 0, 320, 600, 36, "礼品目录", 22, DeepGreen, TextAnchor.MiddleLeft, true);

        int n = Mathf.Min(4, defs.Length);
        float[] ys = { 225f, 95f, -35f, -165f };
        for (int i = 0; i < n; i++)
        {
            GiftDefinition d = defs[i];
            RectTransform item = Box(left, "Gift_" + d.id, 0, ys[i], 620, 118, CardBg, false);
            Label(item, "GiftTitle", -70, 34, 430, 30, d.title, 22, DeepGreen, TextAnchor.MiddleLeft, true);
            Label(item, "GiftDesc", -70, 2, 430, 34, d.description, 15, Ink, TextAnchor.MiddleLeft);
            Label(item, "GiftPrice", -190, -36, 250, 28,
                  "需要 " + d.price + " 西瓜币", 18, DeepGreen, TextAnchor.MiddleLeft, true);
            cardStock[i] = Label(item, "GiftStock", 40, -36, 170, 28,
                                 "", 17, Ink, TextAnchor.MiddleLeft);
            string id = d.id;
            cardButtons[i] = MakeButton(item, "Redeem", 235, 0, 110, 62, "兑换", Accent,
                                        delegate { SelectGift(id); });
        }
    }

    void BuildForm(RectTransform card)
    {
        RectTransform right = Box(card, "FormArea", 340, 0, 640, 700,
                                  new Color(0f, 0f, 0f, 0f), false);
        Label(right, "FormTitle", 0, 320, 600, 36, "兑换信息", 22, DeepGreen, TextAnchor.MiddleLeft, true);
        Label(right, "SelectedGift", 0, 283, 600, 30, "", 17, Red, TextAnchor.MiddleLeft);
        statusText = right.Find("SelectedGift").GetComponent<Text>();

        Label(right, "NameLabel", -200, 230, 170, 36, "收货人姓名", 18, Ink, TextAnchor.MiddleLeft);
        nameField = MakeInput(right, "NameInput", 95, 230, 370, 44, "请输入收货人姓名");

        Label(right, "PhoneLabel", -200, 175, 170, 36, "手机号", 18, Ink, TextAnchor.MiddleLeft);
        phoneField = MakeInput(right, "PhoneInput", 95, 175, 370, 44, "请输入手机号");

        Label(right, "AddressLabel", -200, 118, 170, 36, "收货地址", 18, Ink, TextAnchor.MiddleLeft);
        addressField = MakeInput(right, "AddressInput", 95, 105, 370, 62, "请输入收货地址");

        MakeButton(right, "Confirm", -120, 30, 190, 52, "确认兑换", Accent, Confirm);
        MakeButton(right, "Cancel", 120, 30, 190, 52, "取消", Red, ClearForm);

        Label(right, "OrdersTitle", 0, -40, 600, 32, "我的订单", 22, DeepGreen, TextAnchor.MiddleLeft, true);
        orderListText = Label(right, "Orders", 0, -190, 600, 260, "", 16, Ink, TextAnchor.UpperLeft);
    }

    void SelectGift(string id)
    {
        GiftDefinition d = WatermelonGiftCatalog.Find(id);
        selectedGiftId = id;
        if (d != null && statusText != null)
            statusText.text = "已选择：" + d.title + "（" + d.price + " 西瓜币），请填写收货信息后确认兑换";
    }

    void Confirm()
    {
        if (string.IsNullOrEmpty(selectedGiftId))
        {
            if (statusText != null) statusText.text = "请先点击一张礼品卡上的「兑换」";
            return;
        }
        GiftRedeemResult r = WatermelonGiftRedemptionService.TryRedeem(
            selectedGiftId,
            nameField != null ? nameField.text : "",
            phoneField != null ? phoneField.text : "",
            addressField != null ? addressField.text : "");
        if (statusText != null) statusText.text = r.message;
        if (r.success) ClearFields();
        Refresh();
    }

    void ClearFields()
    {
        if (nameField != null) nameField.text = "";
        if (phoneField != null) phoneField.text = "";
        if (addressField != null) addressField.text = "";
        selectedGiftId = "";
    }

    void ClearForm()
    {
        ClearFields();
        if (statusText != null) statusText.text = "已取消，未选择礼品";
    }

    public void Refresh()
    {
        if (balanceText != null)
            balanceText.text = "当前" + WatermelonCurrencyWallet.CurrencyName + "余额 " +
                               WatermelonCurrencyWallet.GetBalance();

        int n = Mathf.Min(4, defs == null ? 0 : defs.Length);
        for (int i = 0; i < n; i++)
        {
            int stock = WatermelonGiftCatalog.GetStock(defs[i].id);
            if (cardStock[i] != null) cardStock[i].text = "剩余库存 " + stock;
            if (cardButtons[i] != null) cardButtons[i].interactable = defs[i].enabled && stock > 0;
        }

        if (orderListText != null)
        {
            List<GiftOrder> orders = WatermelonGiftRedemptionService.GetOrders();
            if (orders.Count == 0)
            {
                orderListText.text = "暂无订单";
            }
            else
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                int from = Mathf.Max(0, orders.Count - 4);
                for (int i = orders.Count - 1; i >= from; i--)
                {
                    GiftOrder o = orders[i];
                    if (o == null) continue;
                    sb.AppendLine(o.orderId + "  ·  " + o.giftTitle);
                    sb.AppendLine("    " + o.createdAt + "    状态：" + o.status);
                }
                if (from > 0) sb.AppendLine("… 共 " + orders.Count + " 笔，仅显示最近 4 笔");
                orderListText.text = sb.ToString();
            }
        }
    }
}
