using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace WatermelonFarm
{
    public sealed class Btn
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public Button btn;
        public Text label;

        public void Tint(Color c) { img.color = c; }
        public void Caption(string s) { label.text = s; }
        public void Visible(bool v) { go.SetActive(v); }
    }

    public static class UIKit
    {
        // 全局字号倍率：由 FarmUI 按当前逻辑画布尺寸设定，保证大屏下文字与板块同比放大
        public static float FontScale = 1f;

        public static int FS(int size)
        {
            return Mathf.Max(6, Mathf.RoundToInt(size * FontScale));
        }

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                try
                {
                    _font = Font.CreateDynamicFontFromOSFont(new string[]
                    {
                        "Microsoft YaHei", "Microsoft JhengHei", "PingFang SC",
                        "Noto Sans CJK SC", "SimHei", "WenQuanYi Micro Hei", "Arial Unicode MS", "Arial"
                    }, 22);
                }
                catch (System.Exception) { _font = null; }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (System.Exception) { }
                }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch (System.Exception) { }
                }
                return _font;
            }
        }

        public static Color C(string hex) { return SpriteFactory.C(hex); }

        // 以「左上角为原点、y 向下」的方式放置元素，和网页布局思路一致
        public static RectTransform Place(Transform parent, string name, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(Transform parent, string name, float x, float y, float w, float h,
            Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rt = Place(parent, name, x, y, w, h);
            Image im = rt.gameObject.AddComponent<Image>();
            im.sprite = sprite;
            im.color = color;
            im.raycastTarget = raycast;
            im.preserveAspect = false;
            return im;
        }

        public static Text Txt(Transform parent, string name, float x, float y, float w, float h,
            string content, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
            FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = Place(parent, name, x, y, w, h);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = content;
            t.fontSize = FS(size);
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            t.raycastTarget = false;
            return t;
        }

        public static Btn Button(Transform parent, string name, float x, float y, float w, float h,
            string label, int size, Color bg, Color fg, UnityAction onClick, int radius = 8)
        {
            RectTransform rt = Place(parent, name, x, y, w, h);
            Image im = rt.gameObject.AddComponent<Image>();
            im.sprite = SpriteFactory.Panel(Mathf.Max(2, (int)w), Mathf.Max(2, (int)h), radius);
            im.color = bg;
            im.raycastTarget = true;
            Button b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            b.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
            cb.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            if (onClick != null) b.onClick.AddListener(onClick);

            Btn result = new Btn();
            result.go = rt.gameObject;
            result.rt = rt;
            result.img = im;
            result.btn = b;
            float px = 5f * FontScale, py = 2f * FontScale;
            result.label = Txt(rt, "Label", px, py, w - 2f * px, h - 2f * py, label, size, fg,
                TextAnchor.MiddleCenter);
            return result;
        }

        public static ScrollRect Scroll(Transform parent, string name, float x, float y, float w, float h,
            Color bg, out RectTransform content)
        {
            RectTransform rt = Place(parent, name, x, y, w, h);
            ScrollRect sr = rt.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 26f;

            RectTransform vp = Place(rt, "Viewport", 0, 0, w, h);
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = Vector2.zero;
            vp.offsetMax = Vector2.zero;
            Image vpImg = vp.gameObject.AddComponent<Image>();
            vpImg.color = bg;
            vpImg.raycastTarget = true;
            vp.gameObject.AddComponent<RectMask2D>();
            sr.viewport = vp;

            RectTransform crt = Place(vp, "Content", 0, 0, w, 0);
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = Vector2.zero;
            sr.content = crt;
            content = crt;
            return sr;
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject g = t.GetChild(i).gameObject;
                g.SetActive(false);
                Object.Destroy(g);
            }
        }

        // 粗略估算富文本所需高度，供弹窗手动纵向排版使用
        public static float EstimateHeight(string rich, float width, int fontSize)
        {
            if (string.IsNullOrEmpty(rich)) return 0f;
            bool inTag = false;
            float units = 0f;
            int newLines = 0;
            for (int i = 0; i < rich.Length; i++)
            {
                char ch = rich[i];
                if (ch == '<') { inTag = true; continue; }
                if (ch == '>') { inTag = false; continue; }
                if (inTag) continue;
                if (ch == '\n') { newLines++; continue; }
                units += (ch > 127) ? 1f : 0.55f;
            }
            float fs = FS(fontSize);
            float perLine = Mathf.Max(6f, width / (fs * 1.03f));
            float lines = newLines + Mathf.Ceil(units / perLine) + 1f;
            return lines * fs * 1.62f + 16f * FontScale;
        }
    }
}
