using System.Collections.Generic;
using UnityEngine;

namespace WatermelonFarm
{
    // 工具光标：选中「水壶 / 施肥 / 锄头·除草 / 除虫」后，把系统箭头替换成对应农具插画。
    //
    // 主路径是 FarmUI 里的 UGUI 软光标（大图、清晰、随分辨率缩放，系统箭头隐藏）；
    // 本类同时准备一份 64×64 的 OS 自定义光标纹理，用于：
    //   1) 工程只启用新输入系统、拿不到 Input.mousePosition 时的安全回退；
    //   2) 任何情况下都保证「鼠标指针」本身已被换成农具，而不是默认箭头。
    // 四张插画位于 Assets/Resources/Art/tool_*.png，缺图时自动退回系统箭头。
    public static class ToolCursor
    {
        public const int OS_SIZE = 64;
        public static readonly string[] Tools = { "water", "fert", "hoe", "spray" };

        static readonly Dictionary<string, Texture2D> osCache = new Dictionary<string, Texture2D>();

        public static bool IsTool(string t)
        {
            for (int i = 0; i < Tools.Length; i++) if (Tools[i] == t) return true;
            return false;
        }

        public static Sprite Sprite(string tool)
        {
            return ArtBank.Get("tool_" + tool);
        }

        // 热点：以「图片左上角为原点」的归一化坐标，指向工具的作用尖端。
        // 与 UGUI 的 pivot（y 自下而上）通过 Pivot() 换算；OS 光标直接乘纹理尺寸。
        public static Vector2 Hotspot(string tool)
        {
            switch (tool)
            {
                case "water": return new Vector2(0.16f, 0.16f);  // 壶嘴 / 洒水头
                case "fert": return new Vector2(0.30f, 0.12f);  // 袋口嫩芽
                case "hoe": return new Vector2(0.24f, 0.74f);   // 锄刃
                case "spray": return new Vector2(0.16f, 0.16f);  // 喷嘴
                default: return new Vector2(0.12f, 0.12f);
            }
        }

        // RectTransform.pivot：y 自下而上
        public static Vector2 Pivot(string tool)
        {
            Vector2 h = Hotspot(tool);
            return new Vector2(h.x, 1f - h.y);
        }

        // 把 256×256 的插画按 alpha 加权降采样到 64×64，作为系统光标纹理。
        // 用与源纹理一致的「自下而上」像素顺序写回，保证显示方向不翻转。
        public static Texture2D OSTexture(string tool)
        {
            Texture2D cached;
            if (osCache.TryGetValue(tool, out cached)) return cached;

            Sprite s = Sprite(tool);
            Texture2D src = s != null ? s.texture : null;
            if (src == null) { osCache[tool] = null; return null; }

            Texture2D small = null;
            try
            {
                int sw = src.width, sh = src.height;
                Color32[] sp = src.GetPixels32();
                Color32[] dp = new Color32[OS_SIZE * OS_SIZE];
                for (int y = 0; y < OS_SIZE; y++)
                {
                    int y0 = y * sh / OS_SIZE, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / OS_SIZE);
                    for (int x = 0; x < OS_SIZE; x++)
                    {
                        int x0 = x * sw / OS_SIZE, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / OS_SIZE);
                        long r = 0, g = 0, b = 0, a = 0;
                        int n = 0;
                        for (int yy = y0; yy < y1 && yy < sh; yy++)
                            for (int xx = x0; xx < x1 && xx < sw; xx++)
                            {
                                Color32 c = sp[yy * sw + xx];
                                long aa = c.a;
                                r += c.r * aa; g += c.g * aa; b += c.b * aa;
                                a += c.a; n++;
                            }
                        dp[y * OS_SIZE + x] = (a > 0)
                            ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / n))
                            : new Color32(0, 0, 0, 0);
                    }
                }
                small = new Texture2D(OS_SIZE, OS_SIZE, TextureFormat.RGBA32, false);
                small.name = "cursor_" + tool;
                small.SetPixels32(dp);
                small.Apply(false, false);   // 保持可读，Cursor.SetCursor 需要可读写纹理
                small.wrapMode = TextureWrapMode.Clamp;
                small.filterMode = FilterMode.Bilinear;
            }
            catch (System.Exception)
            {
                // 少数平台不允许读像素时，直接用原始纹理（系统会自动缩放到光标尺寸）
                small = src;
            }
            osCache[tool] = small;
            return small;
        }

        public static void ApplyOS(string tool)
        {
            Texture2D t = OSTexture(tool);
            if (t == null) { ResetOS(); return; }
            Vector2 h = Hotspot(tool);
            Cursor.SetCursor(t, new Vector2(h.x * t.width, h.y * t.height), CursorMode.Auto);
        }

        public static void ResetOS()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
        }
    }
}
