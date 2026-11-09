using System;
using UnityEngine;
using UnityEngine.UI;

/* ============================================================
 * GuiliUIKit.cs —— uGUI 构建工具
 * 视觉参数取自 css/atelier.css：
 *   --paper #f4efdc  --ink #333b2c  --green #405b42
 *   --muted #8a8b72  --line #bab398  --red #9f4b3b
 *   标题楷体 STKaiti/KaiTi，正文 Microsoft YaHei/SimHei
 *   纸纹平铺、直角细边框、朱红印章、双线装帧
 * ============================================================ */

public static class GuiliUIKit
{
    public static Font Kai;
    public static Font Hei;
    static Texture2D[] icons;

    // ══════════ 1440x900 网页设计稿 -> 1920x1080 统一基准 ══════════
    // 所有 Legacy* 方法集中在此，调用处一律写设计稿原始数值，不得散落手工缩放数字。
    // 字号按高度系数等比放大，保证文字不因容器变高而被截断。
    public const float LegacyDesignWidth = 1440f;
    public const float LegacyDesignHeight = 900f;
    public const float LegacyScaleX = 1920f / 1440f;
    public const float LegacyScaleY = 1080f / 900f;
    public const float LegacyFontScale = 1080f / 900f;

    public static float LegacyX(float value) { return value * LegacyScaleX; }
    public static float LegacyY(float value) { return value * LegacyScaleY; }
    public static float LegacyWidth(float value) { return value * LegacyScaleX; }
    public static float LegacyHeight(float value) { return value * LegacyScaleY; }
    public static Vector2 LegacyPos(Vector2 v) { return new Vector2(LegacyX(v.x), LegacyY(v.y)); }
    public static Vector2 LegacySize(Vector2 v) { return new Vector2(LegacyWidth(v.x), LegacyHeight(v.y)); }
    public static Vector2 LegacyOffset(Vector2 v) { return new Vector2(LegacyX(v.x), LegacyY(v.y)); }
    public static int LegacyFont(int value) { return Mathf.RoundToInt(value * LegacyFontScale); }

    public static void LoadFonts()
    {
        Kai = Resources.Load<Font>("Fonts/KaiTi");
        Hei = Resources.Load<Font>("Fonts/HeiTi");
        if (Kai == null)
        {
            try { Kai = Font.CreateDynamicFontFromOSFont(new[] { "STKaiti", "KaiTi", "SimSun" }, 28); }
            catch (Exception) { Kai = null; }
        }
        if (Hei == null)
        {
            try { Hei = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei" }, 24); }
            catch (Exception) { Hei = null; }
        }
        if (Kai == null || Hei == null)
        {
            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtin == null) builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (Kai == null) Kai = builtin;
                if (Hei == null) Hei = builtin;
            }
            catch (Exception) { }
        }
    }

    public static RectTransform R(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        GameObject g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)g.transform;
        r.anchorMin = aMin; r.anchorMax = aMax;
        r.offsetMin = LegacyOffset(oMin); r.offsetMax = LegacyOffset(oMax);
        return r;
    }
    public static RectTransform Stretch(string name, Transform parent)
    {
        return R(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }
    public static RectTransform Fixed(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        RectTransform r = R(name, parent, anchor, anchor, Vector2.zero, Vector2.zero);
        r.sizeDelta = LegacySize(size);
        r.anchoredPosition = LegacyPos(pos);
        return r;
    }
    public static Image Img(string name, Transform parent, Color color, bool raycast = false)
    {
        RectTransform r = Stretch(name, parent);
        Image im = r.gameObject.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = raycast;
        return im;
    }
    public static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color color, bool raycast = false)
    {
        RectTransform r = Fixed(name, parent, anchor, pos, size);
        Image im = r.gameObject.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = raycast;
        return im;
    }
    public static void Border(Graphic g, Color color, float distance = 1f)
    {
        Outline o = g.gameObject.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(distance, -distance);
        o.useGraphicAlpha = true;
    }
    public static Text T(string name, Transform parent, string value, int size, Color color, TextAnchor anchor, bool kai = false, bool raycast = false)
    {
        RectTransform r = Stretch(name, parent);
        Text t = r.gameObject.AddComponent<Text>();
        t.font = kai ? Kai : Hei;
        t.fontSize = LegacyFont(size);
        t.color = color;
        t.alignment = anchor;
        t.text = value;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        /* 网页是流式排版，Unity 里若允许 Overflow，长文案会压到下一行元素上，
           造成「字体排版跑了」；但单行大字（首页 100px 标题、30px 任务名）若
           也用 1.55 行高 + Truncate，整行会因为「行高 > 容器高」被完全裁掉，
           这正是首页大标题消失的原因。按文本内容自动区分两套规则。 */
        bool single = value != null && !value.Contains("\n") && value.Length <= 8;
        t.lineSpacing = single ? 1.0f : 1.55f;
        t.verticalOverflow = (single && size >= 26)
            ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        t.raycastTarget = raycast;
        return t;
    }
    public static Text TFixed(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string value, int size2, Color color, TextAnchor anchor2, bool kai = false)
    {
        RectTransform r = Fixed(name, parent, anchor, pos, size);
        Text t = r.gameObject.AddComponent<Text>();
        t.font = kai ? Kai : Hei;
        t.fontSize = LegacyFont(size2);
        t.color = color;
        t.alignment = anchor2;
        t.text = value;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        bool single = value != null && !value.Contains("\n") && value.Length <= 8;
        t.lineSpacing = single ? 1.0f : 1.55f;
        t.verticalOverflow = (single && size2 >= 26)
            ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    public static Button Btn(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                             string label, Action onClick, bool primary, int fontSize = 16, bool kai = true)
    {
        RectTransform r = Fixed(name, parent, anchor, pos, size);
        Image im = r.gameObject.AddComponent<Image>();
        im.color = primary ? GuiliData.Green : new Color(0, 0, 0, 0);
        im.raycastTarget = true;
        if (!primary) Border(im, GuiliData.Green, 1f);
        Button b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = im;
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(.92f, .95f, .86f, 1);
        cb.pressedColor = new Color(.82f, .86f, .76f, 1);
        cb.selectedColor = Color.white;
        cb.fadeDuration = .15f;
        b.colors = cb;
        Text t = T("字", r, label, fontSize, primary ? GuiliData.Paper : GuiliData.Green, TextAnchor.MiddleCenter, kai);
        t.rectTransform.offsetMin = new Vector2(4, 2);
        t.rectTransform.offsetMax = new Vector2(-4, -2);
        if (onClick != null) b.onClick.AddListener(() => onClick());
        return b;
    }

    /* 纯文字按钮（quiet / text-link） */
    public static Button Quiet(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size,
                               string label, Action onClick, int fontSize = 13, Color? color = null)
    {
        RectTransform r = Fixed(name, parent, anchor, pos, size);
        Image im = r.gameObject.AddComponent<Image>();
        im.color = new Color(0, 0, 0, 0);
        im.raycastTarget = true;
        Button b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = im;
        b.onClick.AddListener(() => onClick());
        TFixed("字", r, new Vector2(.5f, .5f), Vector2.zero, size, label, fontSize,
               color ?? GuiliData.Green, TextAnchor.MiddleCenter, true);
        return b;
    }

    public static Image Icon(string name, Transform parent, int index, Color color)
    {
        if (icons == null)
        {
            icons = new Texture2D[7];
            for (int i = 0; i < 7; i++) icons[i] = GuiliTextures.MakeIcon(i);
        }
        RectTransform r = Stretch(name, parent);
        Image im = r.gameObject.AddComponent<Image>();
        im.sprite = GuiliTextures.FromTexture(icons[index], "icon" + index);
        im.color = color;
        im.raycastTarget = false;
        im.preserveAspect = true;
        return im;
    }

    public static Image Pic(string name, Transform parent, string art, Color tint)
    {
        RectTransform r = Stretch(name, parent);
        Image im = r.gameObject.AddComponent<Image>();
        /* 网页插画均为 mix-blend-mode:multiply；此处用纸色融合版，白底不再露白框 */
        im.sprite = GuiliTextures.ArtSpriteOnPaper(art);
        im.color = tint;
        im.raycastTarget = false;
        im.preserveAspect = true;
        return im;
    }

    public static RawImage Paper(string name, Transform parent)
    {
        RectTransform r = Stretch(name, parent);
        RawImage ri = r.gameObject.AddComponent<RawImage>();
        Texture2D t = GuiliTextures.Art("paper");
        ri.texture = t;
        /* 网页 .paper-grain 只有 opacity:.06（且是 multiply），
           旧版用 .55 全屏叠加会把整个画面（含 3D 视口）染成灰黄。 */
        ri.color = new Color(1, 1, 1, .06f);
        ri.raycastTarget = false;
        if (t != null) { t.wrapMode = TextureWrapMode.Repeat; ri.uvRect = new Rect(0, 0, 5, 4); }
        return ri;
    }

    /* 全屏纸面（首页 / 挑战表 / 成绩单 / 弹窗）：纸色 + 极淡纸纹。
       瓜摊与切瓜阶段不使用，让 3D 视口保持通透。 */
    public static void PaperPanel(string name, Transform parent)
    {
        Img(name + "纸色", parent, GuiliData.Paper);
        Paper(name + "纸纹", parent);
    }

    /* 细横线（装帧分隔线） */
    public static void HLine(string name, Transform parent, Vector2 anchor, Vector2 pos, float width, Color color)
    {
        Box(name, parent, anchor, pos, new Vector2(width, 1), color);
    }

    /* 分数进度条 */
    public static Image Bar(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, out Image fill)
    {
        Image bg = Box(name, parent, anchor, pos, size, new Color(.85f, .84f, .76f, 1));
        RectTransform fr = Fixed("填充", bg.transform, new Vector2(0, .5f), Vector2.zero, size);
        fill = fr.gameObject.AddComponent<Image>();
        fill.color = GuiliData.Green;
        fill.raycastTarget = false;
        RectTransform ftr = fill.rectTransform;
        ftr.anchorMin = new Vector2(0, 0); ftr.anchorMax = new Vector2(0, 1);
        ftr.pivot = new Vector2(0, .5f);
        ftr.offsetMin = new Vector2(0, 1); ftr.offsetMax = new Vector2(0, -1);
        ftr.sizeDelta = new Vector2(0, -2);
        return bg;
    }

    public static void SetBar(Image fill, float ratio, float width)
    {
        RectTransform r = fill.rectTransform;
        r.sizeDelta = new Vector2(Mathf.Max(0, width * Mathf.Clamp01(ratio)), -2);
    }

    /* 音量滑杆 */
    public static Slider VolumeSlider(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float value, Action<float> onValue)
    {
        RectTransform root = Fixed(name, parent, anchor, pos, size);
        Image track = Box("轨道", root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size.x, 6), GuiliData.Line);
        RectTransform fillArea = Fixed("填充区", root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size.x, 6));
        Image fill = Img("填充", fillArea, GuiliData.Green);
        RectTransform handleArea = Fixed("滑块区", root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(size.x, 20));
        Image handle = Box("滑块", handleArea, new Vector2(0, .5f), Vector2.zero, new Vector2(14, 20), GuiliData.Green);
        Slider s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0; s.maxValue = 1; s.value = value;
        s.onValueChanged.AddListener(v => onValue(v));
        return s;
    }

    public static void SetActive(GameObject go, bool on) { if (go != null) go.SetActive(on); }
}
