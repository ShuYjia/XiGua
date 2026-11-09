using System.Collections.Generic;
using UnityEngine;

namespace WatermelonFarm
{
    // 极简像素画布：所有美术资源都在运行时用代码绘制，工程不依赖任何外部图片
    internal sealed class PixelCanvas
    {
        public int w, h;
        public Color32[] px;

        public PixelCanvas(int w, int h) { this.w = w; this.h = h; px = new Color32[w * h]; }

        public void Blend(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || c.a <= 0) return;
            int idx = y * w + x;
            Color d = px[idx];
            float a = Mathf.Clamp01(c.a), da = d.a;
            float oa = a + da * (1 - a);
            if (oa <= 0.0001f) { px[idx] = new Color32(0, 0, 0, 0); return; }
            float r = (c.r * a + d.r * da * (1 - a)) / oa;
            float g = (c.g * a + d.g * da * (1 - a)) / oa;
            float b = (c.b * a + d.b * da * (1 - a)) / oa;
            px[idx] = new Color(r, g, b, oa);
        }

        public float RoundAlpha(int x, int y, int rw, int rh, float r)
        {
            float cx = rw / 2f, cy = rh / 2f, hw = rw / 2f, hh = rh / 2f;
            float qx = Mathf.Max(Mathf.Abs((x + 0.5f) - cx) - (hw - r), 0f);
            float qy = Mathf.Max(Mathf.Abs((y + 0.5f) - cy) - (hh - r), 0f);
            float dist = Mathf.Sqrt(qx * qx + qy * qy) - r;
            return Mathf.Clamp01(0.5f - dist);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            if (rx <= 0 || ry <= 0) return;
            int x0 = Mathf.FloorToInt(cx - rx - 2), x1 = Mathf.CeilToInt(cx + rx + 2);
            int y0 = Mathf.FloorToInt(cy - ry - 2), y1 = Mathf.CeilToInt(cy + ry + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f);
                    if (a > 0) Blend(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
        }

        public void Line(float x0, float y0, float x1, float y1, float thick, Color c)
        {
            float vx = x1 - x0, vy = y1 - y0;
            float len2 = vx * vx + vy * vy;
            int ax = Mathf.FloorToInt(Mathf.Min(x0, x1) - thick - 2);
            int bx = Mathf.CeilToInt(Mathf.Max(x0, x1) + thick + 2);
            int ay = Mathf.FloorToInt(Mathf.Min(y0, y1) - thick - 2);
            int by = Mathf.CeilToInt(Mathf.Max(y0, y1) + thick + 2);
            for (int y = ay; y <= by; y++)
                for (int x = ax; x <= bx; x++)
                {
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float t = len2 <= 0 ? 0 : Mathf.Clamp01(((px0 - x0) * vx + (py0 - y0) * vy) / len2);
                    float dx = px0 - (x0 + t * vx), dy = py0 - (y0 + t * vy);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(thick / 2f - d + 0.5f);
                    if (a > 0) Blend(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
        }

        public Texture2D ToTexture()
        {
            // 画布用「y=0 在顶部」的屏幕坐标，写入纹理时翻转成 Unity 的「y=0 在底部」
            Color32[] flipped = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) flipped[y * w + x] = px[(h - 1 - y) * w + x];
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(flipped);
            tex.Apply(false, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }
    }

    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Color C(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        static Sprite Make(string key, int w, int h, System.Action<PixelCanvas> draw)
        {
            Sprite s;
            if (cache.TryGetValue(key, out s)) return s;
            PixelCanvas p = new PixelCanvas(w, h);
            draw(p);
            Texture2D tex = p.ToTexture();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            s.name = key;
            cache[key] = s;
            return s;
        }

        // 白色圆角面板，用 Image.color 染色
        public static Sprite Panel(int w, int h, int r)
        {
            if (w < 2) w = 2; if (h < 2) h = 2;
            if (r * 2 > Mathf.Min(w, h)) r = Mathf.Min(w, h) / 2;
            string key = "panel_" + w + "_" + h + "_" + r;
            return Make(key, w, h, delegate (PixelCanvas p)
            {
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float a = p.RoundAlpha(x, y, w, h, r);
                        if (a > 0) p.Blend(x, y, new Color(1, 1, 1, a));
                    }
            });
        }

        // 地块土壤：竖条纹 + 边框 + 顶部内阴影
        public static Sprite Soil(int w, int h, bool locked, bool upgraded)
        {
            Sprite art = ArtBank.Soil(locked);
            if (art != null)
            {
                // 手绘土壤原图是 2.4:1 宽幅，宽屏下地块接近 1.6:1。
                // 直接拉伸会把圆角拉成鼓包，这里按目标尺寸重采样并重新生成圆角。
                string akey = "soil_art_" + w + "_" + h + "_" + locked;
                Sprite ready;
                if (cache.TryGetValue(akey, out ready)) return ready;
                try
                {
                    Texture2D tex = art.texture;
                    Color32[] src = tex.GetPixels32();
                    int sw = tex.width, sh = tex.height;
                    float radius = Mathf.Clamp(Mathf.Min(w, h) * 0.055f, 8f, 26f);
                    return Make(akey, w, h, delegate (PixelCanvas p)
                    {
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                float a = p.RoundAlpha(x, y, w, h, radius);
                                if (a <= 0) continue;
                                int sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / w * sw), 0, sw - 1);
                                int sy = Mathf.Clamp(Mathf.FloorToInt((1f - (y + 0.5f) / h) * sh), 0, sh - 1);
                                Color32 c = src[sy * sw + sx];
                                p.Blend(x, y, new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f * a));
                            }
                    });
                }
                catch (System.Exception) { return art; }   // 纹理不可读时退回原图
            }
            string key = "soil_" + w + "_" + h + "_" + locked + "_" + upgraded;
            return Make(key, w, h, delegate (PixelCanvas p)
            {
                Color a = C("#8A5A33"), b = C("#7A4C28");
                Color border = C(locked ? "#6B746F" : upgraded ? "#B45309" : "#5E3A1C");
                Color flat = C("#9AA5A1");
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float alpha = p.RoundAlpha(x, y, w, h, 12f);
                        if (alpha <= 0) continue;
                        Color c;
                        if (locked) c = flat;
                        else c = ((x / 16) % 2 == 0) ? a : b;
                        if (x < 3 || y < 3 || x > w - 4 || y > h - 4) c = border;
                        else if (!locked && y < 18)
                        {
                            float k = 1f - (18 - y) / 18f * 0.32f;
                            c = new Color(c.r * k, c.g * k, c.b * k, 1f);
                        }
                        if (upgraded && !locked && (x < 6 || x > w - 7 || y > h - 7))
                            c = Color.Lerp(c, C("#FDE047"), 0.35f);
                        p.Blend(x, y, new Color(c.r, c.g, c.b, alpha));
                    }
            });
        }

        // 天空渐变
        public static Sprite Sky(int w, int h)
        {
            string key = "sky_" + w + "_" + h;
            return Make(key, w, h, delegate (PixelCanvas p)
            {
                Color top = C("#8ED8F8"), bottom = C("#C9EEFF");
                for (int y = 0; y < h; y++)
                {
                    Color c = Color.Lerp(top, bottom, y / (float)Mathf.Max(1, h - 1));
                    for (int x = 0; x < w; x++) p.Blend(x, y, c);
                }
            });
        }

        public static Sprite Sun()
        {
            return Make("sun", 72, 72, delegate (PixelCanvas p)
            {
                Color glow = new Color(1f, 0.83f, 0.28f, 0.28f);
                p.Ellipse(36, 36, 34, 34, glow);
                for (int i = 0; i < 12; i++)
                {
                    float ang = i * Mathf.PI / 6f;
                    p.Line(36 + Mathf.Cos(ang) * 20, 36 + Mathf.Sin(ang) * 20,
                           36 + Mathf.Cos(ang) * 28, 36 + Mathf.Sin(ang) * 28, 3.2f, C("#FDE047"));
                }
                p.Ellipse(36, 36, 19, 19, C("#FDE047"));
                p.Ellipse(30, 30, 7, 7, new Color(1, 1, 1, 0.35f));
            });
        }

        public static Sprite Cloud(int w, int h)
        {
            string key = "cloud_" + w + "_" + h;
            return Make(key, w, h, delegate (PixelCanvas p)
            {
                Color c = new Color(1, 1, 1, 0.92f);
                p.Ellipse(w * 0.30f, h * 0.62f, w * 0.26f, h * 0.34f, c);
                p.Ellipse(w * 0.55f, h * 0.45f, w * 0.28f, h * 0.45f, c);
                p.Ellipse(w * 0.78f, h * 0.64f, w * 0.22f, h * 0.30f, c);
                p.Ellipse(w * 0.5f, h * 0.75f, w * 0.46f, h * 0.22f, c);
            });
        }

        static void Leaf(PixelCanvas p, float cx, float cy, float rx, float ry, Color c, float angle)
        {
            // 用两段线 + 椭圆近似一片叶子
            p.Ellipse(cx, cy, rx, ry, c);
            float dx = Mathf.Cos(angle) * rx, dy = Mathf.Sin(angle) * ry;
            p.Line(cx - dx, cy - dy, cx + dx, cy + dy, 1.2f, new Color(0.06f, 0.32f, 0.16f, 0.7f));
        }

        static void Melon(PixelCanvas p, float cx, float cy, float rx, float ry, bool ripe)
        {
            Color baseCol = ripe ? C("#16A34A") : C("#4ADE80");
            Color stripe = C("#14532D");
            p.Ellipse(cx, cy, rx, ry, baseCol);
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            {
                float ny = (y + 0.5f - cy) / ry;
                if (Mathf.Abs(ny) > 0.99f) continue;
                float half = rx * Mathf.Sqrt(1 - ny * ny);
                for (int k = -3; k <= 3; k++)
                {
                    float x = cx + (k / 3.6f) * half + Mathf.Sin(ny * 6.28f) * 1.6f;
                    Color sc = new Color(stripe.r, stripe.g, stripe.b, 0.55f);
                    p.Blend(Mathf.RoundToInt(x), y, sc);
                    p.Blend(Mathf.RoundToInt(x) + 1, y, new Color(stripe.r, stripe.g, stripe.b, 0.3f));
                }
            }
            p.Ellipse(cx - rx * 0.38f, cy - ry * 0.42f, rx * 0.20f, ry * 0.14f, new Color(1, 1, 1, 0.28f));
            p.Line(cx, cy - ry, cx + 2, cy - ry - 5, 2f, C("#15803D"));
        }

        // 成熟期按品种取专属插画；其余阶段（以及缺图时）沿用同一套生长素材
        public static Sprite Plant(int stage, bool dead, string vid)
        {
            if (!dead && stage >= 7)
            {
                Sprite ripe = ArtBank.Ripe(vid);
                if (ripe != null) return ripe;
            }
            return Plant(stage, dead);
        }

        // 8 个生长阶段 + 枯萎，全部程序化绘制
        public static Sprite Plant(int stage, bool dead)
        {
            Sprite art = ArtBank.Plant(stage, dead);
            if (art != null) return art;
            string key = "plant_" + (dead ? "dead" : stage.ToString());
            return Make(key, 96, 96, delegate (PixelCanvas p)
            {
                Color dark = C("#15803D"), mid = C("#22C55E"), light = C("#86EFAC");
                if (dead)
                {
                    Color brown = C("#92400E");
                    p.Line(48, 84, 46, 60, 2.4f, brown);
                    p.Line(46, 60, 32, 68, 2f, brown);
                    p.Line(46, 60, 62, 70, 2f, brown);
                    p.Line(48, 76, 34, 82, 1.6f, C("#A16207"));
                    p.Line(48, 76, 64, 84, 1.6f, C("#A16207"));
                    return;
                }
                if (stage == 0)
                {
                    p.Ellipse(48, 74, 9, 6, C("#8A5A33"));
                    p.Ellipse(45, 72, 3, 2, new Color(1, 1, 1, 0.25f));
                    return;
                }
                if (stage == 1)
                {
                    p.Line(48, 82, 48, 62, 2.5f, mid);
                    Leaf(p, 39, 60, 9, 4.5f, light, 0.4f);
                    Leaf(p, 57, 60, 9, 4.5f, light, -0.4f);
                    return;
                }
                if (stage == 2)
                {
                    p.Line(48, 84, 48, 50, 3f, dark);
                    Leaf(p, 37, 68, 12, 5.5f, mid, 0.4f);
                    Leaf(p, 59, 62, 12, 5.5f, light, -0.4f);
                    Leaf(p, 40, 54, 10, 5f, light, 0.3f);
                    Leaf(p, 57, 48, 10, 5f, mid, -0.3f);
                    return;
                }
                if (stage == 3)
                {
                    p.Line(48, 84, 44, 62, 3f, dark);
                    p.Line(44, 62, 26, 74, 2.2f, mid);
                    p.Line(44, 62, 68, 72, 2.2f, mid);
                    p.Line(44, 62, 50, 44, 2.2f, mid);
                    Leaf(p, 28, 70, 12, 6f, mid, 0.5f);
                    Leaf(p, 68, 68, 12, 6f, light, -0.5f);
                    Leaf(p, 50, 44, 11, 5.5f, light, 0.2f);
                    p.Line(70, 72, 78, 62, 1.4f, C("#65A30D"));
                    return;
                }
                if (stage == 4)
                {
                    p.Line(48, 84, 46, 58, 3f, dark);
                    p.Line(46, 70, 28, 78, 2.2f, mid);
                    p.Line(46, 66, 68, 76, 2.2f, mid);
                    Leaf(p, 28, 76, 12, 6f, mid, 0.4f);
                    Leaf(p, 68, 74, 12, 6f, light, -0.4f);
                    Color petal = C("#FDE047"), core = C("#F59E0B");
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * Mathf.PI * 2 / 5f;
                        p.Ellipse(48 + Mathf.Cos(ang) * 9, 44 + Mathf.Sin(ang) * 9, 7, 7, petal);
                    }
                    p.Ellipse(48, 44, 6, 6, core);
                    return;
                }
                if (stage == 5)
                {
                    p.Line(48, 84, 40, 76, 2.4f, dark);
                    p.Line(48, 84, 72, 80, 2f, mid);
                    Leaf(p, 26, 76, 12, 6f, mid, 0.4f);
                    Leaf(p, 72, 78, 12, 6f, light, -0.4f);
                    Melon(p, 48, 62, 18, 16, false);
                    return;
                }
                if (stage == 6)
                {
                    p.Line(48, 86, 34, 80, 2.4f, dark);
                    p.Line(48, 86, 74, 82, 2.2f, mid);
                    Leaf(p, 24, 78, 12, 6f, mid, 0.4f);
                    Leaf(p, 76, 80, 12, 6f, light, -0.4f);
                    Melon(p, 48, 58, 27, 23, false);
                    return;
                }
                // 成熟
                p.Line(48, 88, 28, 82, 2.6f, dark);
                p.Line(48, 88, 76, 84, 2.4f, mid);
                Leaf(p, 22, 80, 13, 6.5f, mid, 0.4f);
                Leaf(p, 78, 82, 13, 6.5f, light, -0.4f);
                Melon(p, 48, 54, 34, 28, true);
            });
        }
    }
}
