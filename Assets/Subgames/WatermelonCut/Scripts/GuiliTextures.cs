using System;
using System.Collections.Generic;
using UnityEngine;

/* ============================================================
 * GuiliTextures.cs —— Unity 端程序化「生图」
 * 对应网页 js/watermelon.js 的 makeLegacySkinTexture / makeLegacyCrossSectionTexture
 *       + js/rind-textures.js 的 makeSkinTexture / makeCrossSectionTexture
 *       + js/atelier-scene.js 的桌面木纹与阴影
 *       + index.html 的 SVG symbol 图标
 * 以网页真实瓜皮照片为底图，按种子叠加着地斑、雀斑、擦痕、损伤与裂纹；
 * 照片不可读时自动回退到纯程序化条纹瓜皮。全部在 Unity 运行时生成。
 * ============================================================ */

/* 像素画布：坐标系与网页 canvas 一致，(0,0) 在左上角 */
public class PixelCanvas
{
    public int W, H;
    public Color[] p;
    public PixelCanvas(int w, int h, Color fill)
    {
        W = w; H = h; p = new Color[w * h];
        for (int i = 0; i < p.Length; i++) p[i] = fill;
    }
    public Color Get(int x, int y)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return new Color(0, 0, 0, 0);
        return p[y * W + x];
    }
    public void Set(int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        p[y * W + x] = c;
    }
    /* source-over 混合，供半透明笔触使用 */
    public void Blend(int x, int y, Color c)
    {
        if (c.a <= 0 || x < 0 || y < 0 || x >= W || y >= H) return;
        Color a = p[y * W + x];
        float sa = Mathf.Clamp01(c.a);
        float oa = sa + a.a * (1 - sa);
        if (oa <= 0) { p[y * W + x] = new Color(0, 0, 0, 0); return; }
        float dr = (c.r * sa + a.r * a.a * (1 - sa)) / oa;
        float dg = (c.g * sa + a.g * a.a * (1 - sa)) / oa;
        float db = (c.b * sa + a.b * a.a * (1 - sa)) / oa;
        p[y * W + x] = new Color(dr, dg, db, oa);
    }
    public void Rect(int x, int y, int w, int h, Color c)
    {
        for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c);
    }
    public void Dot(float x, float y, float radius, Color c)
    {
        int x0 = Mathf.FloorToInt(x - radius), x1 = Mathf.CeilToInt(x + radius);
        int y0 = Mathf.FloorToInt(y - radius), y1 = Mathf.CeilToInt(y + radius);
        for (int j = y0; j <= y1; j++) for (int i = x0; i <= x1; i++)
        {
            float d = Mathf.Sqrt((i - x) * (i - x) + (j - y) * (j - y));
            if (d > radius + .5f) continue;
            Color cc = c; cc.a *= Mathf.Clamp01(radius + .5f - d);
            Blend(i, j, cc);
        }
    }
    public void Line(float x0, float y0, float x1, float y1, Color c, float width)
    {
        float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        int steps = Mathf.Max(1, Mathf.CeilToInt(len * 2));
        for (int s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            Dot(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, width * .5f, c);
        }
    }
    public void Path(Vector2[] pts, Color c, float width)
    {
        for (int i = 0; i < pts.Length - 1; i++) Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, c, width);
    }
    public void EllipseOutline(float cx, float cy, float rx, float ry, Color c, float width)
    {
        Vector2[] pts = new Vector2[181];
        for (int i = 0; i <= 180; i++)
        {
            float a = (float)i / 180 * Mathf.PI * 2f;
            pts[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
        }
        Path(pts, c, width);
    }
    public void EllipseFill(float cx, float cy, float rx, float ry, Color c)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                float d = (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry);
                if (d <= 1) Blend(x, y, c);
            }
    }
    /* 径向渐变椭圆斑 */
    public void RadialSpot(float cx, float cy, float rx, float ry, Color center, Color edge, float power)
    {
        for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
            for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry));
                if (d > 1) continue;
                float t = Mathf.Pow(Mathf.Clamp01(d), power);
                Color cc = Color.Lerp(center, edge, t);
                cc.a *= Mathf.Clamp01((1 - d) * 3f);
                Blend(x, y, cc);
            }
    }
    /* 输出为 Unity Texture2D：canvas 顶部 -> 纹理顶部(v=1) */
    public Texture2D ToTexture(bool mipmap = true)
    {
        Color[] flipped = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                flipped[(H - 1 - y) * W + x] = p[y * W + x];
        Texture2D t = new Texture2D(W, H, TextureFormat.RGBA32, mipmap);
        t.SetPixels(flipped);
        t.wrapMode = TextureWrapMode.Repeat;
        t.anisoLevel = 4;
        t.Apply(true, false);
        return t;
    }
}

public static class GuiliTextures
{
    static Texture2D photo;
    static bool photoTried;
    static readonly Dictionary<string, Texture2D> artCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

    public static Texture2D Photo()
    {
        if (!photoTried)
        {
            photoTried = true;
            try { photo = Resources.Load<Texture2D>("Art/rind-photographic"); }
            catch (Exception) { photo = null; }
        }
        return photo;
    }

    public static Texture2D Art(string name)
    {
        if (artCache.ContainsKey(name)) return artCache[name];
        Texture2D t = Resources.Load<Texture2D>("Art/" + name);
        artCache[name] = t;
        return t;
    }

    public static Sprite ArtSprite(string name)
    {
        string key = "art:" + name;
        if (spriteCache.ContainsKey(key)) return spriteCache[key];
        Texture2D t = Art(name);
        Sprite s = t == null ? null : Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
        spriteCache[key] = s;
        return s;
    }

    /* ------------------------------------------------------------
     * 网页用 CSS「mix-blend-mode: multiply」把工笔插画的近白底融进纸色
     * （.home-illustration>img / .modal-art / .about-art / .vine-border）。
     * uGUI 没有该混合模式，直接贴会露出一块白底方块——这正是 Unity 端
     * 「底色变了」的原因之一。这里在导入时按正片叠底把白色精确映射到
     * --paper #f4efdc，做到与网页一致且不留白框。
     * ------------------------------------------------------------ */
    public static Texture2D ArtOnPaper(string name)
    {
        string key = "onpaper:" + name;
        if (artCache.ContainsKey(key)) return artCache[key];
        Texture2D src = Art(name);
        if (src == null) { artCache[key] = null; return null; }
        Color[] px;
        try { px = src.GetPixels(); }
        catch (Exception) { artCache[key] = src; return src; }

        int w = src.width, h = src.height;
        Color paper = GuiliData.Paper;

        /* ① 估计原图自身的「纸白」。网页 multiply 的前提是插画为白底；
              但 botanical.jpg / vine-border.jpg 的白其实带一点灰绿，直接乘纸色
              仍会留下一个比纸面略暗的矩形——这就是「一看就是垫了一张图」。 */
        int s = Mathf.Max(4, Mathf.Min(w, h) / 24);
        int[,] regions = {
            { 0, 0 }, { w - s, 0 }, { 0, h - s }, { w - s, h - s },
            { (w - s) / 2, 0 }, { (w - s) / 2, h - s }
        };
        Color bg = Color.white;
        float bestLum = -1f;
        for (int k = 0; k < regions.GetLength(0); k++)
        {
            float r = 0, g = 0, b = 0; int n = 0;
            int x0 = Mathf.Clamp(regions[k, 0], 0, w - 1), y0 = Mathf.Clamp(regions[k, 1], 0, h - 1);
            for (int y = y0; y < Mathf.Min(h, y0 + s); y++)
                for (int x = x0; x < Mathf.Min(w, x0 + s); x++)
                {
                    Color p = px[y * w + x];
                    r += p.r; g += p.g; b += p.b; n++;
                }
            if (n == 0) continue;
            Color avg = new Color(r / n, g / n, b / n, 1);
            float lum = avg.r * .299f + avg.g * .587f + avg.b * .114f;
            if (lum > bestLum) { bestLum = lum; bg = avg; }
        }
        bool lightGround = bestLum > .78f;

        /* ② 逐像素：底色归一化到纯白 → 正片叠底到纸色 → 按墨浓度生成 alpha
              → 四周羽化。于是白底彻底透明、只留工笔线条与淡彩，边界消失。 */
        float br = Mathf.Max(.04f, bg.r), bgc = Mathf.Max(.04f, bg.g), bb = Mathf.Max(.04f, bg.b);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                Color c = px[i];
                Color nrm = lightGround
                    ? new Color(Mathf.Clamp01(c.r / br), Mathf.Clamp01(c.g / bgc), Mathf.Clamp01(c.b / bb), 1)
                    : c;
                Color mul = new Color(nrm.r * paper.r, nrm.g * paper.g, nrm.b * paper.b, 1);
                if (!lightGround) { px[i] = new Color(mul.r, mul.g, mul.b, c.a); continue; }
                float ink = 1f - Mathf.Min(nrm.r, Mathf.Min(nrm.g, nrm.b));
                /* 阈值放宽一点，保住极淡的水彩层次，避免插画看起来「缺了一半」 */
                float alpha = Fade(0f, 1f, Mathf.Clamp01((ink - .008f) / .15f));
                float fx = Mathf.Min(x, w - 1 - x) / (float)w;
                float fy = Mathf.Min(y, h - 1 - y) / (float)h;
                float feather = Fade(0f, 1f, Mathf.Clamp01(Mathf.Min(fx, fy) / .085f));
                px[i] = new Color(mul.r, mul.g, mul.b, alpha * feather * c.a);
            }
        Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        t.filterMode = FilterMode.Bilinear;
        t.wrapMode = TextureWrapMode.Clamp;
        t.Apply(false, false);
        artCache[key] = t;
        return t;
    }

    /* 首页插画背后极淡的暖色水彩晕：让插画与纸面之间有一层柔和过渡，
       不产生任何可见边界（对应网页纸张的轻微色温变化）。 */
    public static Texture2D MakeWash()
    {
        int S = 256;
        PixelCanvas c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
        c.RadialSpot(S * .5f, S * .46f, S * .48f, S * .50f,
                     C("#e7dcbd", .13f), C("#e7dcbd", 0), 1.1f);
        return c.ToTexture(false);
    }

    public static Sprite ArtSpriteOnPaper(string name)
    {
        string key = "art-onpaper:" + name;
        if (spriteCache.ContainsKey(key)) return spriteCache[key];
        Texture2D t = ArtOnPaper(name);
        Sprite s = t == null ? null :
            Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
        spriteCache[key] = s;
        return s;
    }

    public static Sprite FromTexture(Texture2D t, string key)
    {
        if (t == null) return null;
        string k = "tex:" + key;
        if (spriteCache.ContainsKey(k)) return spriteCache[k];
        Sprite s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
        spriteCache[k] = s;
        return s;
    }

    static float R(System.Random r) { return (float)r.NextDouble(); }
    /* 0→1 的 smoothstep 渐隐。注意 Mathf.SmoothStep(a,b,t) 返回的是 a..b 之间的
       插值，中心处等于 a 而不是 0；地斑曾用它导致中心不透明度被压到 .43，
       三档颜色几乎分不出来，这里用正确的归一化版本。 */
    static float Fade(float lo, float hi, float v)
    {
        float t = Mathf.Clamp01((v - lo) / Mathf.Max(.001f, hi - lo));
        return t * t * (3f - 2f * t);
    }
    static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }
    static Color C(string hex, float a) { Color c = C(hex); c.a = a; return c; }
    static Color Saturate(Color c, float s)
    {
        float g = c.r * .299f + c.g * .587f + c.b * .114f;
        return new Color(Mathf.Lerp(g, c.r, s), Mathf.Lerp(g, c.g, s), Mathf.Lerp(g, c.b, s), c.a);
    }
    static Color Brighten(Color c, float b)
    {
        return new Color(Mathf.Clamp01(c.r * b), Mathf.Clamp01(c.g * b), Mathf.Clamp01(c.b * b), c.a);
    }

    /* ------------------------------------------------------------
     * 瓜皮贴图（1024x512，U 方向环绕）
     * ------------------------------------------------------------ */
    public static Texture2D MakeSkin(MelonInfo m)
    {
        int W = 1024, H = 512;
        System.Random rnd = new System.Random(m.textureSeed);
        PixelCanvas c = new PixelCanvas(W, H, C("#8a9c61"));
        Texture2D ph = Photo();
        Color[] sampled = null;
        if (ph != null)
        {
            try { sampled = Resample(ph, W, H); } catch (Exception) { sampled = null; }
        }
        if (sampled != null)
        {
            float sat = .65f + R(rnd) * .18f;
            float bright = .79f + R(rnd) * .16f;
            int offset = rnd.Next(W);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color col = sampled[y * W + ((x + offset) % W)];
                    col = Brighten(Saturate(col, sat), bright);
                    col.a = 1;
                    c.Set(x, y, col);
                }
            /* 消除 UV 接缝 */
            for (int y = 0; y < H; y++)
                for (int x = 0; x < 24; x++)
                {
                    float t = (24 - x) / 24f * .5f;
                    Color a = c.Get(x, y), b = c.Get(W - 1 - x, y);
                    c.Set(x, y, Color.Lerp(a, b, t));
                    c.Set(W - 1 - x, y, Color.Lerp(b, a, t));
                }
        }
        else LegacySkin(c, rnd, m, m.spotGrade);

        /* 着地斑：与网页 js/rind-textures.js 完全一致，直接画在球面贴图
           UV 底部（sy = 0.89H，靠近南极）——就是最初版那块圆圆的白斑。
           不再额外叠加球冠网格或整幅放射状瓜底视图。
           三色（白 / 奶油黄 / 橙黄）由 spotGrade 决定，并与切瓜结果对应。 */
        float sx = W * (.25f + R(rnd) * .5f), sy = H * .89f;
        Color spotIn, spotOut;
        SpotPalette(m.spotGrade, out spotIn, out spotOut);
        DrawFieldSpot(c, sx, sy, spotIn, spotOut, rnd);
        /* 正常的木栓小雀斑 */
        if (m.freckles)
        {
            float cx = R(rnd) * W, cy = H * (.25f + R(rnd) * .45f);
            for (int n = 0; n < 70; n++)
            {
                float x = cx + (R(rnd) - .5f) * 130f, y = cy + (R(rnd) - .5f) * 75f;
                c.Blend(Mathf.RoundToInt(x), Mathf.RoundToInt(y), C("#b5a566", .15f + R(rnd) * .35f));
            }
        }
        /* 轻微擦痕 */
        if (m.scuff)
        {
            float x = W * R(rnd), y = H * (.3f + R(rnd) * .35f);
            for (int n = 0; n < 8; n++)
            {
                Vector2[] pts = new Vector2[8];
                for (int k = 0; k < 8; k++)
                    pts[k] = new Vector2(x + n * 2f + k * 5.7f, y + R(rnd) * 3f + Mathf.Sin(k * .6f) * 3f + k * 2.6f);
                c.Path(pts, C("#bfb384", .48f), .8f);
            }
        }
        /* 损伤：局部瘀斑、裂纹、皱缩 */
        if (m.damaged)
        {
            float x = W * (.12f + R(rnd) * .73f), y = H * (.3f + R(rnd) * .35f), r = 32f + R(rnd) * 23f;
            c.RadialSpot(x, y, r, r * .75f, C("#312415", .86f), C("#5d592c", 0), .55f);
            for (int n = 0; n < 320; n++)
            {
                float dx = (R(rnd) - .5f) * r * 1.6f, dy = (R(rnd) - .5f) * r;
                if (dx * dx + dy * dy > r * r * .65f) continue;
                Color sp = new Color(.27f + R(rnd) * .23f, .25f, .11f, R(rnd) * .35f);
                c.Blend(Mathf.RoundToInt(x + dx), Mathf.RoundToInt(y + dy), sp);
            }
            Vector2[] crack = {
                new Vector2(x - r * .6f, y + 5), new Vector2(x - 8, y - 3),
                new Vector2(x + 5, y + 2), new Vector2(x + r * .6f, y - 7)
            };
            c.Path(crack, C("#251d10", .8f), 1.7f);
            if (m.wrinkles)
            {
                for (int n = 0; n < 13; n++)
                {
                    float wx = x - r * .7f + n * 4f;
                    Vector2[] w = new Vector2[10];
                    for (int k = 0; k < 10; k++)
                    {
                        float t = k / 9f;
                        w[k] = new Vector2(wx + Mathf.Sin(t * 3f) * 3f, y - r * .7f + t * r * 1.35f);
                    }
                    c.Path(w, C("#3a391c", .36f), .7f);
                }
            }
        }
        return c.ToTexture();
    }

    /* 纯程序化条纹瓜皮（照片不可用时的等价实现） */
    static void LegacySkin(PixelCanvas c, System.Random rnd, MelonInfo m, SpotGrade grade)
    {
        int W = c.W, H = c.H;
        string[] tones = { "#a4ae72", "#8a9c61", "#aeb67c" };
        Color baseTone = C(tones[Mathf.Abs(m.textureSeed) % 3]);
        c.Rect(0, 0, W, H, baseTone);
        for (int i = 0; i < 2600; i++)
        {
            int x = rnd.Next(W), y = rnd.Next(H);
            c.Blend(x, y, C("#ffffff", .025f));
            c.Blend(rnd.Next(W), rnd.Next(H), C("#002800", .04f));
        }
        int stripes = 9 + Mathf.Abs(m.textureSeed) % 3;
        Color deep = C("#405b39");
        for (int s = 0; s < stripes; s++)
        {
            float cu = (s + .5f) / stripes * W;
            float halfBase = W / (float)stripes * .16f;
            for (int v = 0; v < H; v += 1)
            {
                float wob = Mathf.Sin(v * .05f + s * 2.1f) * 10f + Mathf.Sin(v * .013f + s) * 16f;
                float nearPole = v > H * .86f ? (1 - (v - H * .86f) / (H * .14f)) : (v < H * .14f ? v / (H * .14f) : 1);
                nearPole = Mathf.Clamp01(nearPole);
                int xl = Mathf.RoundToInt(cu + wob - halfBase * nearPole);
                int xr = Mathf.RoundToInt(cu + wob + halfBase * nearPole);
                for (int x = xl; x <= xr; x++) c.Set((x % W + W) % W, v, deep);
            }
        }
        /* 极点附近的条纹已收拢；与网页最初版一样，在底部补一块三色圆斑 */
        Color inside, outside;
        SpotPalette(grade, out inside, out outside);
        c.RadialSpot(W * .5f, H * .90f, 95f * 1.6f, 95f * .6f, inside, outside, .8f);
        if (m.damaged)
        {
            float[,] spots = { { W * .28f, H * .34f, 74 }, { W * .33f, H * .30f, 46 }, { W * .24f, H * .38f, 34 } };
            for (int i = 0; i < 3; i++)
            {
                float x = spots[i, 0], y = spots[i, 1], r = spots[i, 2];
                c.RadialSpot(x, y, r, r * .8f, C("#402614", .96f), C("#5a371c", 0), .6f);
                c.Line(x - r * .7f, y, x + r * .8f, y - r * .2f, C("#1e1208", .9f), 3f);
            }
        }
    }

    /* ------------------------------------------------------------
     * 着地斑三色（网页 js/rind-textures.js 最初版形态）
     *
     * 网页 makeSkinTexture 在瓜皮贴图底部画一块不规则圆形着地斑：
     *   translate(sx, sy) + scale(1.65, .58) + 径向渐变 + 高频起伏边缘
     *   + 220 个细蜡质斑点。最初只有「奶黄 / 偏白」两色，本轮按用户
     *   要求扩成三色，共用同一块圆斑、只换颜色：
     *     白色   White  —— 发白无黄晕 → ordinary / raw / damaged
     *     奶油黄 Cream  —— 蜡质奶黄   → ripe
     *     橙黄   Orange —— 暖橙黄     → ripe
     * 斑形与方位由 spotVariant 决定；颜色 ↔ 切瓜结果由
     * GuiliData.GradeMatchesCut 严格对应。
     * ------------------------------------------------------------ */
    public static void SpotPalette(SpotGrade g, out Color inside, out Color outside)
    {
        switch (g)
        {
            case SpotGrade.Cream:
                inside = C("#f3dc91", 1f); outside = C("#e0bd63", 0f); break;   /* 奶油黄 */
            case SpotGrade.Orange:
                inside = C("#eeae55", 1f); outside = C("#d78934", 0f); break;   /* 橙黄 */
            default:
                inside = C("#f2f0e2", 1f); outside = C("#dcddc2", 0f); break;   /* 白色 */
        }
    }

    /* 网页 rind-textures.js 的不规则圆形着地斑 */
    public static void DrawFieldSpot(PixelCanvas c, float sx, float sy,
                                     Color inside, Color outside, System.Random rnd)
    {
        float rx = 85f * 1.65f, ry = 85f * .58f;
        for (int y = Mathf.FloorToInt(sy - ry); y <= Mathf.CeilToInt(sy + ry); y++)
            for (int x = Mathf.FloorToInt(sx - rx); x <= Mathf.CeilToInt(sx + rx); x++)
            {
                float nx = (x - sx) / rx, ny = (y - sy) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float ang = Mathf.Atan2(ny, nx);
                float edge = 81f / 85f + Mathf.Sin(ang * 7f) * .05f + Mathf.Cos(ang * 11f) * .035f;
                if (d > edge) continue;
                float t = Mathf.Clamp01(d / Mathf.Max(.001f, edge));
                Color cc = Color.Lerp(inside, outside, Mathf.Lerp(.65f, 1f, t));
                cc.a = 1f - Mathf.Clamp01((t - .65f) / .35f);
                c.Blend(x, y, cc);
            }
        /* 斑面上的细小蜡质斑点，与网页一致 */
        for (int i = 0; i < 220; i++)
        {
            float x = (R(rnd) - .5f) * 115f, y = (R(rnd) - .5f) * 115f;
            if (x * x + y * y > 3300f) continue;
            c.Blend(Mathf.RoundToInt(sx + x * 1.65f), Mathf.RoundToInt(sy + y * .58f), C("#5b552e", .13f));
        }
    }

    /* 三色圆斑预览图：把球面贴图底部那块斑单独画出来，供导出与人工验收。
       底色是网页同色调的条纹瓜皮，中心就是玩家翻底时看到的圆斑。 */
    public static Texture2D MakeFieldSpotPreview(SpotGrade g, int seed)
    {
        const int W = 512, H = 256;
        System.Random rnd = new System.Random(seed);
        PixelCanvas c = new PixelCanvas(W, H, C("#8a9c61"));
        for (int i = 0; i < 2600; i++)
        {
            c.Blend(rnd.Next(W), rnd.Next(H), C("#ffffff", .025f));
            c.Blend(rnd.Next(W), rnd.Next(H), C("#002800", .04f));
        }
        int stripes = 10;
        for (int s = 0; s < stripes; s++)
        {
            float cu = (s + .5f) / stripes * W;
            for (int v = 0; v < H; v++)
            {
                float wob = Mathf.Sin(v * .05f + s * 2.1f) * 10f + Mathf.Sin(v * .013f + s) * 16f;
                int xl = Mathf.RoundToInt(cu + wob - 7f), xr = Mathf.RoundToInt(cu + wob + 7f);
                for (int x = xl; x <= xr; x++) c.Set((x % W + W) % W, v, C("#405b39"));
            }
        }
        Color inside, outside;
        SpotPalette(g, out inside, out outside);
        DrawFieldSpot(c, W * .5f, H * .5f, inside, outside, rnd);
        return c.ToTexture(false);
    }

    /* 从任意尺寸照片双线性重采样为 W x H（canvas 坐标，y=0 为图片顶部） */
    static Color[] Resample(Texture2D src, int W, int H)
    {
        int sw = src.width, sh = src.height;
        Color[] srcPix = src.GetPixels();
        Color[] dst = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float fy = (1f - (y + .5f) / H) * (sh - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(fy), 0, sh - 1);
            int y1 = Mathf.Min(sh - 1, y0 + 1);
            float ty = fy - y0;
            for (int x = 0; x < W; x++)
            {
                float fx = (x + .5f) / W * (sw - 1);
                int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, sw - 1);
                int x1 = Mathf.Min(sw - 1, x0 + 1);
                float tx = fx - x0;
                Color a = Color.Lerp(srcPix[y0 * sw + x0], srcPix[y0 * sw + x1], tx);
                Color b = Color.Lerp(srcPix[y1 * sw + x0], srcPix[y1 * sw + x1], tx);
                dst[y * W + x] = Color.Lerp(a, b, ty);
            }
        }
        return dst;
    }

    /* ------------------------------------------------------------
     * 切面贴图（512x512）：瓜皮环 / 白层 / 瓜瓤 / 放射纹 / 瓜籽 / 损伤区
     * ------------------------------------------------------------ */
    public static Texture2D MakeCrossSection(string cutType)
    {
        int S = 512;
        float c0 = S / 2f, rad = S / 2f;
        PixelCanvas c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
        c.EllipseFill(c0, c0, rad, rad, C("#2f7040"));
        c.EllipseFill(c0, c0, rad * .94f, rad * .94f, C("#5ba86b"));
        c.EllipseFill(c0, c0, rad * .85f, rad * .85f, C("#e9f2dd"));
        Color flesh = cutType == "raw" ? C("#dba298") : cutType == "ordinary" ? C("#cb8272") : C("#bd6358");
        Color fleshIn = cutType == "raw" ? C("#eac5ac") : cutType == "ordinary" ? C("#d99780") : C("#d57e6e");
        c.RadialSpot(c0, c0, rad * .80f, rad * .80f, fleshIn, flesh, .45f);
        System.Random rnd = new System.Random(cutType == "raw" ? 42 : cutType == "damaged" ? 79 : cutType == "ordinary" ? 16 : 93);
        for (int i = 0; i < 26; i++)
        {
            float a = R(rnd) * Mathf.PI * 2f;
            float len = rad * (.6f + R(rnd) * .2f);
            c.Line(c0 + Mathf.Cos(a) * rad * .15f, c0 + Mathf.Sin(a) * rad * .15f,
                   c0 + Mathf.Cos(a) * len, c0 + Mathf.Sin(a) * len, C("#ffffff", .10f), 2f);
        }
        Color seed = cutType == "raw" ? C("#e8e0c8") : C("#2b1f14");
        Color border = cutType == "raw" ? C("#c9bd9a") : C("#150e08");
        int count = cutType == "raw" ? 9 : 16;
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f + R(rnd) * .4f;
            float rr = rad * (.28f + R(rnd) * .38f);
            float x = c0 + Mathf.Cos(a) * rr, y = c0 + Mathf.Sin(a) * rr;
            DrawSeed(c, x, y, a + Mathf.PI / 2f, seed, border);
        }
        if (cutType == "damaged")
        {
            float dx = c0 - rad * .42f, dy = c0 + rad * .3f;
            c.RadialSpot(dx, dy, rad * .34f, rad * .34f, C("#462819", .9f), C("#462819", 0), .5f);
            c.EllipseFill(dx, dy, rad * .12f, rad * .12f, C("#3c2314", .55f));
        }
        /* 瓤质细颗粒 */
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c0) * (x - c0) + (y - c0) * (y - c0)) / rad;
                if (d > .79f) continue;
                Color px = c.Get(x, y);
                if (px.r * 255f > 105f)
                {
                    float grain = (R(rnd) - .5f) * 19f / 255f;
                    c.Set(x, y, new Color(px.r + grain, px.g + grain * .75f, px.b + grain * .65f, 1));
                }
            }
        return c.ToTexture();
    }

    static void DrawSeed(PixelCanvas c, float x, float y, float angle, Color fill, Color border)
    {
        Vector2[] outline = new Vector2[25];
        for (int i = 0; i <= 24; i++)
        {
            float a = i / 24f * Mathf.PI * 2f;
            float px = Mathf.Cos(a) * 5f, py = Mathf.Sin(a) * 9f;
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            outline[i] = new Vector2(x + px * ca - py * sa, y + px * sa + py * ca);
        }
        /* 填充椭圆（旋转） */
        for (int j = -10; j <= 10; j++) for (int i = -6; i <= 6; i++)
        {
            float u = i / 5f, v = j / 9f;
            if (u * u + v * v > 1) continue;
            float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
            float px = i * ca - j * sa, py = i * sa + j * ca;
            c.Blend(Mathf.RoundToInt(x + px), Mathf.RoundToInt(y + py), fill);
        }
        c.Path(outline, border, 2f);
    }

    /* 桌面木纹（atelier-scene.js） */
    public static Texture2D MakeWood()
    {
        int S = 512;
        PixelCanvas c = new PixelCanvas(S, S, C("#c3b189"));
        System.Random rnd = new System.Random(20261021);
        for (int i = 0; i < 140; i++)
        {
            float y = i * 4f;
            Color col = new Color(83 / 255f, 73 / 255f, 46 / 255f, .025f + R(rnd) * .07f);
            Vector2[] pts = new Vector2[17];
            for (int k = 0; k <= 16; k++)
                pts[k] = new Vector2(k * 32f, y + Mathf.Sin(k * .42f + i * .17f) * 5f);
            c.Path(pts, col, 1f);
        }
        return c.ToTexture();
    }

    /* 瓜底接触阴影 */
    public static Texture2D MakeShadow()
    {
        int S = 128;
        PixelCanvas c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
        c.RadialSpot(64, 64, 64, 64, C("#2f2b19", .28f), C("#2f2b19", 0), .8f);
        return c.ToTexture();
    }

    /* 九瓜图录缩略图（160x110，椭圆裁切 + 描边 + 瓜柄） */
    public static Texture2D MakeThumbnail(Texture2D skin)
    {
        int W = 160, H = 110;
        PixelCanvas c = new PixelCanvas(W, H, new Color(0, 0, 0, 0));
        if (skin != null)
        {
            Color[] src = null;
            try { src = skin.GetPixels(); } catch (Exception) { src = null; }
            if (src != null)
            {
                int sw = skin.width, sh = skin.height;
                for (int y = 20; y < 105; y++)
                    for (int x = 25; x < 135; x++)
                    {
                        float nx = (x - 80) / 49f, ny = (y - 62) / 40f;
                        if (nx * nx + ny * ny > 1) continue;
                        float fx = (x - 25) / 110f * (sw - 1);
                        float fy = (1f - (65 + (y - 20) / 85f * 360f) / 512f) * (sh - 1);
                        int sx = Mathf.Clamp(Mathf.RoundToInt(fx), 0, sw - 1);
                        int sy = Mathf.Clamp(Mathf.RoundToInt(fy), 0, sh - 1);
                        c.Set(x, y, src[sy * sw + sx]);
                    }
            }
        }
        c.EllipseOutline(80, 62, 49, 40, C("#536346"), 1.5f);
        Vector2[] stem = new Vector2[10];
        for (int k = 0; k < 10; k++)
        {
            float t = k / 9f;
            stem[k] = new Vector2(80 - 5 * Mathf.Sin(t * Mathf.PI) + 12 * t * t, 23 - 12 * t + 3 * t * t);
        }
        c.Path(stem, C("#536346"), 1.5f);
        return c.ToTexture();
    }

    /* ------------------------------------------------------------
     * SVG symbol 图标的等价线稿（白色，UI 用 Image.color 着色）
     * ------------------------------------------------------------ */
    public static Texture2D MakeIcon(int index)
    {
        int S = 64;
        PixelCanvas c = new PixelCanvas(S, S, new Color(0, 0, 0, 0));
        Color ink = new Color(1, 1, 1, 1);
        float w = 2.2f;
        if (index == 0) /* 观瓜皮 */
        {
            c.EllipseOutline(32, 36, 22, 20, ink, w);
            for (int k = -1; k <= 1; k++)
            {
                Vector2[] pts = new Vector2[16];
                for (int i = 0; i < 16; i++)
                {
                    float t = i / 15f;
                    pts[i] = new Vector2(32 + k * 11 + Mathf.Sin(t * 3f + k * 2f) * 3f, 17 + t * 38);
                }
                c.Path(pts, ink, w);
            }
            c.Line(32, 16, 32, 9, ink, w); c.Line(32, 9, 40, 7, ink, w);
        }
        else if (index == 1) /* 看底部 */
        {
            c.EllipseOutline(32, 34, 20, 20, ink, w);
            c.EllipseOutline(32, 38, 10, 7, ink, w);
            Vector2[] a1 = { new Vector2(10, 15), new Vector2(18, 7), new Vector2(30, 5), new Vector2(40, 7), new Vector2(53, 15) };
            c.Path(a1, ink, w);
            c.Line(53, 15, 53, 7, ink, w); c.Line(53, 7, 45, 7, ink, w);
            Vector2[] a2 = { new Vector2(54, 53), new Vector2(44, 60), new Vector2(32, 62), new Vector2(22, 60), new Vector2(11, 51) };
            c.Path(a2, ink, w);
            c.Line(11, 51, 11, 59, ink, w); c.Line(11, 59, 19, 59, ink, w);
        }
        else if (index == 2) /* 辨瓜柄 */
        {
            Vector2[] stem = { new Vector2(15, 53), new Vector2(22, 40), new Vector2(28, 30), new Vector2(36, 22), new Vector2(44, 20) };
            c.Path(stem, ink, w);
            Vector2[] leaf1 = { new Vector2(28, 34), new Vector2(10, 32), new Vector2(12, 12), new Vector2(28, 20), new Vector2(28, 34) };
            c.Path(leaf1, ink, w);
            Vector2[] leaf2 = { new Vector2(35, 23), new Vector2(35, 3), new Vector2(56, 8), new Vector2(50, 24), new Vector2(35, 23) };
            c.Path(leaf2, ink, w);
        }
        else if (index == 3) /* 轻叩听音 */
        {
            float[] hs = { 9, 18, 27, 36, 27, 18, 9 };
            for (int i = 0; i < hs.Length; i++) c.Line(12 + i * 7, 32 - hs[i] / 2, 12 + i * 7, 32 + hs[i] / 2, ink, w);
        }
        else if (index == 4) /* 查档案 */
        {
            Vector2[] rect = { new Vector2(12, 12), new Vector2(48, 12), new Vector2(48, 54), new Vector2(12, 54), new Vector2(12, 12) };
            c.Path(rect, ink, w);
            for (int k = 0; k < 4; k++) c.Line(7, 12 + k * 11, 16, 12 + k * 11, ink, w);
            c.Line(24, 23, 40, 23, ink, w); c.Line(24, 32, 40, 32, ink, w); c.Line(24, 41, 34, 41, ink, w);
        }
        else if (index == 5) /* 返回箭头 */
        {
            c.Line(9, 32, 54, 32, ink, w);
            c.Line(39, 17, 54, 32, ink, w); c.Line(54, 32, 39, 47, ink, w);
        }
        else /* 深纹一定更甜 */
        {
            c.EllipseOutline(32, 34, 20, 20, ink, w);
            for (int k = -2; k <= 2; k++)
            {
                Vector2[] pts = new Vector2[12];
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11f;
                    pts[i] = new Vector2(32 + k * 7 + Mathf.Sin(t * 4f) * 2f, 15 + t * 38);
                }
                c.Path(pts, ink, w * .8f);
            }
        }
        return c.ToTexture(false);
    }
}
