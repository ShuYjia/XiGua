using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace WatermelonFarm
{
    internal sealed class PlotView
    {
        public RectTransform root;
        public Image bg;
        public Button click;
        public Text name;
        public Text center;
        public Text overWet;
        public Image plant;
        public RectTransform plantRt;
        public RectTransform waterFill;
        public RectTransform progFill;
        public Image waterBG;
        public Image progBG;
        public Btn upgrade;
        public RectTransform badgeHost;
        public string badgeSig = "";
        public Vector2 plantBase;
        public bool ripe;
    }

    // ── 响应式布局 ────────────────────────────────────────────────────────
    // 设计基准为 16:9。CanvasScaler(Expand) 下，凡是 16:9 的窗口，
    // 逻辑画布恒等于 1920×1080；非 16:9 时只会多出宽或高，绝不裁切内容。
    // 宽屏把日志从底部移到右侧栏，让 3×3 地块可以放大到接近正方形，
    // 顶栏/工具栏则横向铺满，充分利用整个屏幕。
    public sealed class FarmLayout
    {
        public float W, H, S, M;
        public float topH, toolY, toolH, bodyY, bodyBottom, bodyH;
        public bool side;
        public float farmX, farmY, farmW, farmH, skyH, groundH;
        public float plotW, plotH, gap, pad, gridPadX;
        public float tickerX, tickerY, tickerW, tickerH;
        public float logX, logY, logW, logH, logRowH;
        public float overviewY, overviewH;
        public int logMax;
        public float modalScale;

        public static FarmLayout Compute(float W, float H)
        {
            FarmLayout L = new FarmLayout();
            L.W = W; L.H = H;
            // 控件与字号倍率：1920×1080 → 1.2，窄高窗口仍稳定
            L.S = Mathf.Clamp(Mathf.Min(W / 1600f, H / 900f), 0.85f, 1.5f);
            float S = L.S;
            L.M = Mathf.Max(20f, W * 0.021f);

            L.topH = 56f * S;
            L.toolY = 62f * S;
            L.toolH = 46f * S;
            L.bodyY = 116f * S;
            L.bodyBottom = H - 10f * S;
            L.bodyH = L.bodyBottom - L.bodyY;

            // 宽高比够宽时启用右侧信息栏
            L.side = W / Mathf.Max(1f, H) >= 1.60f;
            float sideW = L.side ? Mathf.Clamp(W * 0.27f, 400f * S, 580f * S) : 0f;
            float gutter = 18f * S;
            float leftW = W - 2f * L.M - (L.side ? sideW + gutter : 0f);

            L.skyH = 76f * S;
            L.farmH = L.side ? L.bodyH : Mathf.Min(520f * S, L.bodyH - 250f * S);
            L.farmY = L.bodyY;
            L.groundH = L.farmH - L.skyH;
            L.pad = 14f * S;
            L.gap = 10f * S;

            // 高度决定地块行高；宽度按 2.6:1 封顶，避免宽屏下地块被拉得过扁
            L.plotH = Mathf.Max(60f, (L.groundH - 2f * L.pad - 2f * L.gap) / 3f);
            float availW = (leftW - 2f * L.pad - 2f * L.gap) / 3f;
            L.plotW = Mathf.Max(80f, Mathf.Min(availW, L.plotH * 2.6f));
            float gridW = 3f * L.plotW + 2f * L.gap + 2f * L.pad;
            L.farmW = Mathf.Min(leftW, gridW);
            L.gridPadX = (L.farmW - (3f * L.plotW + 2f * L.gap)) / 2f;

            if (L.side)
            {
                L.farmX = L.M + Mathf.Max(0f, (leftW - L.farmW) / 2f);
                L.tickerX = L.M + leftW + gutter;
                L.tickerW = Mathf.Max(160f, W - L.M - L.tickerX);
                L.tickerY = L.bodyY;
                L.tickerH = 60f * S;                 // 侧栏较窄，允许小知识换两行
                L.logX = L.tickerX;
                L.logY = L.bodyY + L.tickerH + 34f * S;   // 为「农场动态」标题留出一行
                L.logW = L.tickerW;
                // 侧栏底部放「农场概览」，避免宽屏右下方出现大片空白
                L.overviewH = 184f * S;
                L.overviewY = L.bodyBottom - L.overviewH;
                L.logH = Mathf.Max(120f, L.overviewY - 10f * S - L.logY);
            }
            else
            {
                L.farmX = (W - L.farmW) / 2f;
                L.tickerX = L.farmX;
                L.tickerW = L.farmW;
                L.tickerY = L.farmY + L.farmH + 6f * S;
                L.tickerH = 34f * S;
                L.logX = L.farmX;
                L.logY = L.tickerY + L.tickerH + 8f * S;
                L.logW = L.farmW;
                L.logH = Mathf.Max(100f, L.bodyBottom - L.logY);
                L.overviewH = 0f;
                L.overviewY = L.bodyBottom;
            }

            L.logRowH = 32f * S;
            L.logMax = Mathf.Clamp(Mathf.FloorToInt(L.logH / L.logRowH), 4, 20);
            L.modalScale = Mathf.Clamp(Mathf.Min(W / 1280f, H / 900f), 0.9f, 1.5f);
            return L;
        }
    }

    public sealed class FarmUI : IFarmView
    {
        static Color C(string hex) { return UIKit.C(hex); }

        public FarmModel M;
        // 由 FarmGame 注入的「返回课堂」导航回调；保持农场原有 UI 风格只新增一个顶栏入口
        public UnityAction onReturnClassroom;

        FarmLayout L;
        Transform canvasRef;
        RectTransform root;
        RectTransform modalLayer;
        RectTransform toastLayer;          // 永远置顶，盖住仓库弹窗
        readonly List<GameObject> modals = new List<GameObject>();
        readonly List<PlotView> plots = new List<PlotView>();
        readonly List<string[]> logs = new List<string[]>();
        int logVersion = 0, lastLogVersion = -1;
        RectTransform logHost;

        // 顶栏 / 工具栏引用
        Text coinsText, lvText, plotsText, expLabel;
        // 供 FarmGame 挂载统一 HUD，订阅 BalanceChanged 即时刷新余额
        public Text BalanceText { get { return coinsText; } }
        RectTransform expFill;
        Btn fertBtn;
        Btn allWaterBtn, allCleanBtn;   // 未购买自动化系统时置灰，点击给出购买提示
        readonly Dictionary<string, Btn> toolBtns = new Dictionary<string, Btn>();
        readonly Dictionary<float, Btn> speedBtns = new Dictionary<float, Btn>();
        Text tickerText, overviewLeft, overviewRight;
        RectTransform sunRt, cloud1, cloud2;
        float animTime = 0, factTimer = 0;
        int factIndex = 0;
        bool hasArt;   // 是否存在 AI 生成的手绘素材；缺图时全程回退程序化绘制

        // 轻量 Toast：西瓜币到账（2 秒）/ 瓜苗枯萎遗憾提示（4 秒），共用一套卡片
        GameObject toastGO;
        Text toastTitle, toastSub, toastTag, toastRule, toastHint;
        Image toastBorder, toastCardBG, toastMelon;
        float toastTimer = 0f;
        int toastGained = 0;

        // 工具光标：选中农具后用它替换系统箭头，始终位于最顶层且不拦截点击
        RectTransform cursorLayer, cursorRT;
        Image cursorImg;
        string cursorTool = "";
        bool softCursor = true;        // 旧输入可用时走 UGUI 软光标

        Text Pill(Transform parent, string name, float x, float y, float w, float h, string text, int size)
        {
            UIKit.Img(parent, name + "BG", x, y, w, h, new Color(1, 1, 1, 0.16f),
                SpriteFactory.Panel((int)w, (int)h, (int)(h / 2f)));
            return UIKit.Txt(parent, name, x, y, w, h, text, size, Color.white, TextAnchor.MiddleCenter);
        }

        // ── 构建 ──────────────────────────────────────────────────────────
        public void Build(Transform canvasRoot, float W, float H)
        {
            canvasRef = canvasRoot;
            BuildBackdrop(canvasRoot);
            modalLayer = UIKit.Stretch(canvasRoot, "ModalLayer");
            BuildLayout(W, H);
            // 最后创建提示层，保证西瓜币提示盖在所有弹窗之上
            toastLayer = UIKit.Stretch(canvasRoot, "ToastLayer");
            // 光标层比提示层还靠上，任何弹窗/提示出现时都能看到手持工具
            cursorLayer = UIKit.Stretch(canvasRoot, "CursorLayer");
            BuildCursor();
            tickerText.text = "【小知识】" + FarmData.FACTS[factIndex % FarmData.FACTS.Length];
        }

        void BuildBackdrop(Transform canvasRoot)
        {
            RectTransform bgRT = UIKit.Stretch(canvasRoot, "Background");
            Image bgImg = bgRT.gameObject.AddComponent<Image>();
            Sprite bgArt = ArtBank.Background;
            hasArt = bgArt != null;
            if (hasArt)
            {
                bgImg.sprite = bgArt;
                bgImg.color = Color.white;
                RectTransform washRT = UIKit.Stretch(canvasRoot, "ArtWash");
                Image wash = washRT.gameObject.AddComponent<Image>();
                wash.color = new Color(1f, 0.99f, 0.94f, 0.22f);
                wash.raycastTarget = false;
            }
            else bgImg.color = C("#7EC8E3");
            bgImg.raycastTarget = false;
            bgImg.preserveAspect = false;
        }

        // ── 工具光标 ──────────────────────────────────────────────────────
        void BuildCursor()
        {
            float size = CursorSize();
            RectTransform rt = UIKit.Place(cursorLayer, "ToolCursor", 0, 0, size, size);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = ToolCursor.Pivot("water");
            rt.anchoredPosition = Vector2.zero;
            cursorRT = rt;
            cursorImg = rt.gameObject.AddComponent<Image>();
            cursorImg.raycastTarget = false;      // 绝不能挡住地块 / 按钮的点击
            cursorImg.preserveAspect = false;
            cursorImg.color = Color.white;
            rt.gameObject.SetActive(false);
            cursorTool = "";                      // 让 Render 重新套用当前工具与尺寸
        }

        float CursorSize() { return 76f * (L != null ? L.S : 1f); }

        // Render 每次都会调用；P.tool 变化（点工具按钮、化肥用尽回退、徽章快捷操作）都自动同步
        public void ApplyToolCursor(string tool)
        {
            if (!ToolCursor.IsTool(tool)) tool = "water";
            if (cursorTool == tool || cursorRT == null) return;
            cursorTool = tool;

            Sprite s = ToolCursor.Sprite(tool);
            cursorRT.pivot = ToolCursor.Pivot(tool);
            float size = CursorSize();
            cursorRT.sizeDelta = new Vector2(size, size);
            cursorImg.sprite = s;

            // 系统光标也换成同一张农具图；UGUI 软光标显示时把它隐藏，回退时直接露出
            ToolCursor.ApplyOS(tool);
            bool show = s != null && softCursor;
            cursorRT.gameObject.SetActive(show);
            Cursor.visible = !show;
        }

        void TickCursor()
        {
            if (cursorRT == null || string.IsNullOrEmpty(cursorTool)) return;
            if (!softCursor) return;
            Vector2 screen;
            try { screen = Input.mousePosition; }
            catch (System.InvalidOperationException)
            {
                // 工程只启用新输入系统：改用 Cursor.SetCursor 设好的 OS 自定义光标
                softCursor = false;
                cursorRT.gameObject.SetActive(false);
                Cursor.visible = true;
                return;
            }
            // 窗口重新获得焦点后（ReleaseCursor 曾隐藏光标）自动恢复显示
            if (!cursorRT.gameObject.activeSelf && cursorImg != null && cursorImg.sprite != null)
                cursorRT.gameObject.SetActive(true);
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    cursorLayer, screen, null, out local))
                cursorRT.anchoredPosition = local;
            Cursor.visible = false;
        }

        // 退出 Play / 窗口失焦时还原系统箭头，避免编辑器里留下隐藏光标
        public void ReleaseCursor()
        {
            ToolCursor.ResetOS();
            if (cursorRT != null) cursorRT.gameObject.SetActive(false);
        }

        void BuildLayout(float W, float H)
        {
            L = FarmLayout.Compute(W, H);
            UIKit.FontScale = L.S;

            GameObject rootGO = new GameObject("LayoutRoot", typeof(RectTransform));
            rootGO.transform.SetParent(canvasRef, false);
            root = (RectTransform)rootGO.transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(W, H);

            BuildTopBar();
            BuildToolBar();
            BuildFarm();
            BuildTickerAndLog();
            // 分辨率重建后新 root 会排到最后，必须把弹窗层重新置顶
            if (modalLayer != null) modalLayer.SetAsLastSibling();
        }

        // 分辨率变化时只重建布局层，游戏进度与日志完整保留
        public void Rebuild(float W, float H)
        {
            for (int i = 0; i < modals.Count; i++)
                if (modals[i] != null) { modals[i].SetActive(false); Object.Destroy(modals[i]); }
            modals.Clear();
            if (root != null) { root.gameObject.SetActive(false); Object.Destroy(root.gameObject); }
            root = null;
            plots.Clear();
            toolBtns.Clear();
            speedBtns.Clear();
            lastLogVersion = -1;
            BuildLayout(W, H);
            // 重建布局后新的 root 会排到最后，提示层与光标层必须重新置顶（提示本身保留）
            if (toastLayer != null) toastLayer.SetAsLastSibling();
            if (cursorLayer != null) cursorLayer.SetAsLastSibling();
            cursorTool = "";   // 让随后的 Render 按新倍率重设光标尺寸
            tickerText.text = "【小知识】" + FarmData.FACTS[factIndex % FarmData.FACTS.Length];
        }

        void BuildTopBar()
        {
            float S = L.S, W = L.W, m = L.M, topH = L.topH;
            UIKit.Img(root, "TopBar", 0, 0, W, topH, C("#14532D"));
            UIKit.Img(root, "TopBarLine", 0, topH - 2f * S, W, 2f * S, C("#0F3D21"));

            float logo = 40f * S;
            Image melon = UIKit.Img(root, "Logo", m, (topH - logo) / 2f, logo, logo,
                Color.white, SpriteFactory.Plant(7, false, "jx"));
            melon.preserveAspect = true;
            melon.raycastTarget = false;
            UIKit.Txt(root, "Title", m + 48f * S, 0, 200f * S, topH, "瓜田农场", 23,
                Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);

            float py = (topH - 36f * S) / 2f, ph = 36f * S;
            coinsText = Pill(root, "Coins", m + 240f * S, py, 112f * S, ph, "西瓜币 0", 14);
            lvText = Pill(root, "Level", m + 360f * S, py, 78f * S, ph, "Lv.1", 14);
            expLabel = UIKit.Txt(root, "ExpLabel", m + 448f * S, 1f * S, 180f * S, 16f * S,
                "经验 0 / 60", 11, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft);
            UIKit.Img(root, "ExpBG", m + 448f * S, 22f * S, 180f * S, 12f * S,
                new Color(1, 1, 1, 0.22f), SpriteFactory.Panel((int)(180f * S), (int)(12f * S), (int)(6f * S)));
            Image ef = UIKit.Img(root, "ExpFill", m + 448f * S, 22f * S, 0, 12f * S, C("#FDE047"),
                SpriteFactory.Panel((int)(180f * S), (int)(12f * S), (int)(6f * S)));
            expFill = ef.rectTransform;
            plotsText = Pill(root, "Plots", m + 640f * S, py, 150f * S, ph, "耕地 4/9", 13);

            // 顶栏只保留一份「西瓜币 N」；原碎片胶囊位置改为礼品兑换入口
            float giftX = m + 800f * S, giftW = 210f * S;
            UIKit.Button(root, "GiftRedeemBtn", giftX, py, giftW, ph, "礼品兑换", 14,
                C("#FDE68A"), C("#713F12"), delegate
                {
                    WatermelonGiftPanel.Open(canvasRef);
                }, (int)(9f * S));

            // 右侧五个功能按钮整体右对齐；最后一个「返回课堂」用于回到主工程模块
            float bw = 96f * S, bg = 8f * S;
            float by = (topH - ph) / 2f, right = W - m;
            float shopX = right - 5f * bw - 4f * bg;
            UIKit.Button(root, "ShopBtn", shopX, by, bw, ph, "商城", 14,
                new Color(1, 1, 1, 0.16f), Color.white, delegate { M.ShopDialog(); }, (int)(9f * S));
            UIKit.Button(root, "WhBtn", right - 4f * bw - 3f * bg, by, bw, ph, "仓库", 14,
                new Color(1, 1, 1, 0.16f), Color.white, delegate { M.WarehouseDialog(); }, (int)(9f * S));
            UIKit.Button(root, "BookBtn", right - 3f * bw - 2f * bg, by, bw, ph, "科普图鉴", 14,
                new Color(1, 1, 1, 0.16f), Color.white, delegate { M.BookDialog(); }, (int)(9f * S));
            UIKit.Button(root, "HelpBtn", right - 2f * bw - bg, by, bw, ph, "玩法", 14,
                new Color(1, 1, 1, 0.16f), Color.white, delegate { M.HelpDialog(); }, (int)(9f * S));
            Btn returnBtn = null;
            returnBtn = UIKit.Button(root, "ReturnClassroomBtn", right - bw, by, bw, ph, "返回课堂", 13,
                C("#FDE68A"), C("#713F12"), delegate
                {
                    if (onReturnClassroom == null) return;
                    if (returnBtn != null) returnBtn.btn.interactable = false;
                    onReturnClassroom();
                }, (int)(9f * S));

            float sx = giftX + giftW + 12f * S;
            float sw = shopX - sx - 14f * S;
            if (sw > 90f * S)
                UIKit.Txt(root, "Slogan", sx, 0, sw, topH, "庞各庄西瓜", 12,
                    new Color(1, 1, 1, 0.62f), TextAnchor.MiddleCenter);
        }

        void BuildToolBar()
        {
            float S = L.S, W = L.W, m = L.M;
            RectTransform bar = UIKit.Place(root, "ToolBar", 0, L.toolY, W, L.toolH);
            float bh = 40f * S, by = (L.toolH - bh) / 2f;
            string[] tools = { "water", "fert", "hoe", "spray" };
            string[] labels = { "水壶", "施肥 ×0", "锄头·除草", "除虫" };
            float[] ws = { 110f, 120f, 130f, 100f };
            float tx = m;
            for (int i = 0; i < tools.Length; i++)
            {
                float w = ws[i] * S;
                string t = tools[i];
                Btn b = UIKit.Button(bar, "Tool_" + t, tx, by, w, bh, labels[i], 14,
                    Color.white, C("#334155"), delegate { M.SetTool(t); }, (int)(10f * S));
                toolBtns[t] = b;
                tx += w + 10f * S;
            }
            fertBtn = toolBtns["fert"];

            // 倍速组居中：正式版只保留 1x / 2x
            float labelW = 66f * S, sGap = 8f * S;
            float[] speeds = FarmData.SPEEDS;
            string[] slabels = FarmData.SPEED_LABELS;
            float[] sws = { 66f, 66f };
            float sum = 0f;
            for (int i = 0; i < sws.Length; i++) sum += sws[i] * S;
            float groupW = labelW + 8f * S + sum + (speeds.Length - 1) * sGap;
            float gx = (W - groupW) / 2f;
            UIKit.Txt(bar, "SpeedLabel", gx, by, labelW, bh, "倍速", 13,
                C("#334155"), TextAnchor.MiddleCenter, FontStyle.Bold);
            float sx = gx + labelW + 8f * S;
            for (int i = 0; i < speeds.Length; i++)
            {
                float s = speeds[i], w = sws[i] * S;
                Btn b = UIKit.Button(bar, "Speed_" + s, sx, by, w, bh, slabels[i], 13,
                    Color.white, C("#475569"), delegate { M.SetSpeed(s); }, (int)(20f * S));
                speedBtns[s] = b;
                sx += w + sGap;
            }

            // 一键操作组右对齐
            float aw = 160f * S, ac = 196f * S, rgap = 12f * S;
            float rx = W - m - aw - ac - rgap;
            allWaterBtn = UIKit.Button(bar, "AllWater", rx, by, aw, bh, "一键浇水", 14,
                C("#14532D"), Color.white, delegate { M.AllWater(); }, (int)(10f * S));
            allCleanBtn = UIKit.Button(bar, "AllClean", rx + aw + rgap, by, ac, bh, "一键除草除虫", 14,
                C("#14532D"), Color.white, delegate { M.AllClean(); }, (int)(10f * S));
        }

        void BuildFarm()
        {
            float S = L.S;
            RectTransform farm = UIKit.Place(root, "Farm", L.farmX, L.farmY, L.farmW, L.farmH);

            // 有手绘全景时让 farm 区透出背景，仅保留天空裁剪框承载太阳/云的动画
            Image skyImg = UIKit.Img(farm, "Sky", 0, 0, L.farmW, L.skyH,
                hasArt ? new Color(1, 1, 1, 0f) : Color.white,
                hasArt ? null : SpriteFactory.Sky((int)L.farmW, (int)L.skyH));
            skyImg.gameObject.AddComponent<RectMask2D>();
            RectTransform sky = skyImg.rectTransform;

            float sunSize = 72f * S;
            float sunX = L.farmW - sunSize - 20f * S;
            float sunY = (L.skyH - sunSize) / 2f;
            Image sun = UIKit.Img(sky, "Sun", sunX, sunY, sunSize, sunSize,
                hasArt ? new Color(1f, 0.97f, 0.84f, 0.90f) : Color.white, SpriteFactory.Sun());
            sunRt = sun.rectTransform;
            sunRt.pivot = new Vector2(0.5f, 0.5f);
            sunRt.anchoredPosition = new Vector2(sunX + sunSize / 2f, -(sunY + sunSize / 2f));

            Color cloudTint = new Color(1, 1, 1, hasArt ? 0.86f : 0.95f);
            float c1w = 140f * S, c1h = 44f * S, c2w = 100f * S, c2h = 32f * S;
            cloud1 = UIKit.Img(sky, "Cloud1", -c1w, 12f * S, c1w, c1h, cloudTint,
                SpriteFactory.Cloud((int)c1w, (int)c1h)).rectTransform;
            cloud2 = UIKit.Img(sky, "Cloud2", -c2w, 38f * S, c2w, c2h, cloudTint,
                SpriteFactory.Cloud((int)c2w, (int)c2h)).rectTransform;

            // 田地区域用半透明浅色罩住背景，形成层次又不遮挡风景
            UIKit.Img(farm, "Ground", 0, L.skyH, L.farmW, L.groundH,
                hasArt ? new Color(0.96f, 0.98f, 0.86f, 0.30f) : C("#8FBF6A"),
                SpriteFactory.Panel((int)L.farmW, (int)L.groundH, (int)(16f * S)));
            RectTransform ground = UIKit.Place(farm, "GroundSlots", 0, L.skyH, L.farmW, L.groundH);

            for (int i = 0; i < FarmData.NPL; i++)
            {
                int col = i % 3, row = i / 3;
                float px = L.gridPadX + col * (L.plotW + L.gap);
                float py = L.pad + row * (L.plotH + L.gap);
                PlotView pv = new PlotView();
                int idx = i;

                pv.root = UIKit.Place(ground, "Plot" + (i + 1), px, py, L.plotW, L.plotH);
                pv.bg = pv.root.gameObject.AddComponent<Image>();
                pv.bg.sprite = SpriteFactory.Soil((int)L.plotW, (int)L.plotH, false, false);
                pv.bg.color = Color.white;
                pv.bg.raycastTarget = true;
                pv.click = pv.root.gameObject.AddComponent<Button>();
                pv.click.targetGraphic = pv.bg;
                pv.click.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = pv.click.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
                cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                cb.fadeDuration = 0.08f;
                pv.click.colors = cb;
                pv.click.onClick.AddListener(delegate { M.ClickPlot(idx); });

                pv.name = UIKit.Txt(pv.root, "Variety", 0, 4f * S, L.plotW - 96f * S, 20f * S,
                    "", 12, C("#FFF7DB"), TextAnchor.MiddleCenter);
                pv.center = UIKit.Txt(pv.root, "Center", 0, 42f * S, L.plotW, 48f * S, "", 18,
                    C("#FFEDD5"), TextAnchor.MiddleCenter);
                pv.plant = UIKit.Img(pv.root, "Plant", L.plotW / 2f - 45f * S, 26f * S,
                    90f * S, 90f * S, Color.white, SpriteFactory.Plant(0, false));
                pv.plant.preserveAspect = true;   // 京秀是椭圆瓜，不能按方框拉伸
                pv.plantRt = pv.plant.rectTransform;

                float barX = 8f * S;
                float waterW = L.plotW * 0.34f, waterH = 8f * S;
                float waterY = L.plotH - 30f * S;
                pv.waterBG = UIKit.Img(pv.root, "WaterBG", barX, waterY, waterW, waterH,
                    new Color(0, 0, 0, 0.4f),
                    SpriteFactory.Panel((int)waterW, (int)waterH, (int)(4f * S)));
                Image wf = UIKit.Img(pv.root, "WaterFill", barX, waterY, 0, waterH, C("#38BDF8"));
                pv.waterFill = wf.rectTransform;
                float progW = L.plotW - 16f * S, progH = 10f * S, progY = L.plotH - 16f * S;
                pv.progBG = UIKit.Img(pv.root, "ProgBG", barX, progY, progW, progH,
                    new Color(0, 0, 0, 0.4f),
                    SpriteFactory.Panel((int)progW, (int)progH, (int)(5f * S)));
                Image pf = UIKit.Img(pv.root, "ProgFill", barX, progY, 0, progH, C("#22C55E"));
                pv.progFill = pf.rectTransform;

                pv.upgrade = UIKit.Button(pv.root, "Upgrade", L.plotW - 96f * S, 6f * S,
                    88f * S, 28f * S, "升级 300", 12, C("#FDE047"), C("#713F12"),
                    delegate { M.UpgradePlotDialog(idx); }, (int)(8f * S));
                pv.overWet = UIKit.Txt(pv.root, "OverWet", L.plotW - 106f * S, 38f * S,
                    98f * S, 20f * S, "过湿!", 12, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
                pv.badgeHost = UIKit.Place(pv.root, "Badges", 6f * S, 34f * S,
                    132f * S, Mathf.Max(30f, L.plotH - 42f * S));

                plots.Add(pv);
            }
        }

        void BuildTickerAndLog()
        {
            float S = L.S;
            overviewLeft = null;
            overviewRight = null;
            if (L.side)
            {
                // 右侧信息栏底板，让宽屏右侧不显得空
                float bx = L.tickerX - 10f * S, by = L.bodyY - 10f * S;
                float bw = L.logW + 20f * S, bh = L.bodyH + 20f * S;
                UIKit.Img(root, "SidePanel", bx, by, bw, bh,
                    new Color(1f, 1f, 0.98f, 0.74f),
                    SpriteFactory.Panel((int)bw, (int)bh, (int)(16f * S)));
                UIKit.Txt(root, "SideTitle", L.tickerX, L.bodyY + L.tickerH + 4f * S,
                    L.logW, 26f * S, "农场动态", 13, C("#14532D"),
                    TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            UIKit.Img(root, "TickerBG", L.tickerX, L.tickerY, L.tickerW, L.tickerH, C("#FFFBEF"),
                SpriteFactory.Panel((int)L.tickerW, (int)L.tickerH, (int)(10f * S)));
            tickerText = UIKit.Txt(root, "Ticker", L.tickerX + 12f * S, L.tickerY,
                L.tickerW - 24f * S, L.tickerH, "", 13, C("#713F12"), TextAnchor.MiddleLeft);
            logHost = UIKit.Place(root, "Log", L.logX, L.logY, L.logW, L.logH);

            if (L.side)
            {
                float ox = L.logX, oy = L.overviewY, ow = L.logW, oh = L.overviewH;
                UIKit.Img(root, "OverviewBG", ox, oy, ow, oh, C("#F0FDF4"),
                    SpriteFactory.Panel((int)ow, (int)oh, (int)(14f * S)));
                UIKit.Txt(root, "OverviewTitle", ox + 14f * S, oy + 8f * S,
                    ow - 28f * S, 28f * S, "农场概览", 14, C("#14532D"),
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                float colW = (ow - 34f * S) / 2f;
                overviewLeft = UIKit.Txt(root, "OverviewL", ox + 14f * S, oy + 42f * S,
                    colW, oh - 50f * S, "", 13, C("#334155"), TextAnchor.UpperLeft);
                overviewRight = UIKit.Txt(root, "OverviewR", ox + 20f * S + colW, oy + 42f * S,
                    colW, oh - 50f * S, "", 13, C("#334155"), TextAnchor.UpperLeft);
            }
        }

        // ══════════ 渲染 ══════════
        public void Render()
        {
            if (M == null || M.P == null || root == null || L == null) return;
            SaveData P = M.P;
            float S = L.S;

            coinsText.text = "西瓜币 " + WatermelonCurrencyWallet.GetBalance();
            lvText.text = "Lv." + P.lv;
            int need = 30 + P.lv * 30;
            expLabel.text = "经验 " + Mathf.FloorToInt(P.exp) + " / " + need;
            expFill.sizeDelta = new Vector2(180f * S * Mathf.Clamp01(P.exp / need), 12f * S);
            plotsText.text = "耕地 " + P.plotsOwned + "/" + FarmData.NPL + (P.autoWater ? " · 自动" : "");
            // 余额只由统一钱包提供，不随农场存档重置

            foreach (KeyValuePair<string, Btn> kv in toolBtns)
            {
                bool on = P.tool == kv.Key;
                Color bg = on ? (kv.Key == "fert" ? C("#ECFCCB") : C("#FFFBEB")) : Color.white;
                kv.Value.Tint(bg);
                kv.Value.label.color = on ? C("#B45309") : C("#334155");
            }
            fertBtn.Visible(P.fert > 0);
            if (P.fert > 0) fertBtn.Caption("施肥 ×" + P.fert);
            ApplyToolCursor(P.tool);   // 当前工具变了就换光标（含化肥用尽自动回退水壶）

            // 一键操作：未购买对应自动化系统时置灰，但仍可点击 → 弹出去商城购买的提示
            if (allWaterBtn != null)
            {
                allWaterBtn.Tint(P.autoWater ? C("#14532D") : C("#9CA3AF"));
                allWaterBtn.label.color = P.autoWater ? Color.white : C("#E5E7EB");
                allWaterBtn.Caption(P.autoWater ? "一键浇水" : "一键浇水 · 未解锁");
                allWaterBtn.btn.interactable = true;
            }
            if (allCleanBtn != null)
            {
                allCleanBtn.Tint(P.autoClean ? C("#14532D") : C("#9CA3AF"));
                allCleanBtn.label.color = P.autoClean ? Color.white : C("#E5E7EB");
                allCleanBtn.Caption(P.autoClean ? "一键除草除虫" : "一键除草除虫 · 未解锁");
                allCleanBtn.btn.interactable = true;
            }

            foreach (KeyValuePair<float, Btn> kv in speedBtns)
            {
                bool on = Mathf.Abs(P.speed - kv.Key) < 0.01f;
                kv.Value.Tint(on ? C("#FEF2F2") : Color.white);
                kv.Value.label.color = on ? C("#B91C1C") : C("#475569");
            }

            if (overviewLeft != null && overviewRight != null)
            {
                int upgraded = 0;
                for (int i = 0; i < P.plots.Count; i++) if (P.plots[i].upgraded) upgraded++;
                overviewLeft.text = "西瓜币 <b>" + WatermelonCurrencyWallet.GetBalance() + "</b>\n耕地 <b>" + P.plotsOwned + "/" +
                    FarmData.NPL + "</b>\n化肥 <b>×" + P.fert + "</b>\n倍速 <b>" + P.speed +
                    "x</b>\n仓库 <b>" + P.wh.Count + "</b> 瓜";
                overviewRight.text = "等级 <b>Lv." + P.lv + "</b>\n升级 <b>" + upgraded +
                    "</b> 块\n自动浇水 <b>" + (P.autoWater ? "已开" : "未开") + "</b>\n自动除虫 <b>" +
                    (P.autoClean ? "已开" : "未开") + "</b>\n图鉴 <b>" +
                    P.cards.Count + "/" + FarmData.V.Length + "</b>";
            }

            for (int i = 0; i < plots.Count; i++) RenderPlot(plots[i], P.plots[i], i, P);
            RenderLogs();
        }

        void RenderPlot(PlotView pv, PlotData pl, int i, SaveData P)
        {
            float S = L.S;
            bool locked = i >= P.plotsOwned;
            bool dead = pl.st == FarmData.DEAD_ST;
            bool ripe = pl.st == FarmData.RIPE_ST;
            bool empty = string.IsNullOrEmpty(pl.v);
            Variety v = empty ? null : FarmData.GetVariety(pl.v);

            pv.bg.sprite = SpriteFactory.Soil((int)L.plotW, (int)L.plotH, locked, pl.upgraded && !locked);
            Color soilTint = (hasArt && !locked && pl.upgraded)
                ? new Color(1f, 0.87f, 0.55f, 1f) : Color.white;
            ColorBlock soilColors = pv.click.colors;
            soilColors.normalColor = soilTint;
            soilColors.highlightedColor = soilTint * 1.10f;
            soilColors.pressedColor = soilTint * 0.86f;
            pv.click.colors = soilColors;
            pv.bg.color = soilTint;
            pv.name.gameObject.SetActive(!locked && !empty);
            pv.center.gameObject.SetActive(locked || empty || dead);
            pv.plant.gameObject.SetActive(!locked && !empty);
            bool showBars = !locked && !empty;
            pv.waterFill.gameObject.SetActive(showBars);
            pv.progFill.gameObject.SetActive(showBars);
            if (pv.waterBG != null) pv.waterBG.gameObject.SetActive(showBars);
            if (pv.progBG != null) pv.progBG.gameObject.SetActive(showBars);
            pv.upgrade.Visible(!locked);
            pv.badgeHost.gameObject.SetActive(!locked);
            pv.ripe = ripe && !locked;

            if (locked)
            {
                pv.center.text = "第" + (i + 1) + "号地 · 未开垦\n<b>点击开垦 · " + M.PlotPrice() + " 金</b>";
                pv.center.color = C("#3F4A45");
                pv.center.fontSize = UIKit.FS(16);
                pv.upgrade.Visible(false);
                pv.waterFill.sizeDelta = new Vector2(0, 8f * S);
                pv.progFill.sizeDelta = new Vector2(0, 10f * S);
                pv.overWet.gameObject.SetActive(false);
                UIKit.ClearChildren(pv.badgeHost);
                pv.badgeSig = "";
                return;
            }

            if (pl.upgraded)
            {
                pv.upgrade.Caption("×3 已升级");
                pv.upgrade.Tint(C("#F59E0B"));
                pv.upgrade.label.color = Color.white;
                pv.upgrade.btn.interactable = false;
            }
            else
            {
                bool enough = WatermelonCurrencyWallet.GetBalance() >= FarmData.UPG_PRICE;
                pv.upgrade.Caption("升级 " + FarmData.UPG_PRICE);
                pv.upgrade.Tint(enough ? C("#FDE047") : C("#6B7280"));
                pv.upgrade.label.color = enough ? C("#713F12") : C("#CBD5E1");
                pv.upgrade.btn.interactable = true;
            }

            if (empty)
            {
                pv.center.text = "<size=" + UIKit.FS(30) + ">＋</size>\n第" + (i + 1) + "号地 · 空闲（点击种植）";
                pv.center.color = C("#FFEDD5");
                pv.center.fontSize = UIKit.FS(14);
                pv.waterFill.sizeDelta = new Vector2(L.plotW * 0.34f * (55f / 100f), 8f * S);
                pv.progFill.sizeDelta = new Vector2(0, 10f * S);
                pv.overWet.gameObject.SetActive(false);
                UIKit.ClearChildren(pv.badgeHost);
                pv.badgeSig = "";
                return;
            }

            int st = Mathf.Clamp((int)pl.st, 0, 7);
            pv.name.text = dead ? "枯萎 · 点击清理" :
                v.name + " · " + (ripe ? "成熟" : FarmData.ST[st].name) + (pl.crack ? "（裂瓜）" : "");
            pv.name.color = dead ? C("#FCA5A5") : C("#FFF7DB");
            pv.center.gameObject.SetActive(false);

            // 作物尺寸随地块高度等比放大，底部留出状态条空间
            float size = L.plotH * (0.227f + st * 0.068f);
            pv.plantRt.sizeDelta = new Vector2(size, size);
            pv.plantBase = new Vector2(L.plotW / 2f - size / 2f, -(L.plotH - 32f * S - size));
            pv.plantRt.anchoredPosition = pv.plantBase;
            pv.plant.sprite = SpriteFactory.Plant(st, dead, v != null ? v.id : null);
            // 成熟期已按品种换成专属插画，不再染色；生长期仍用极轻色调提示品种
            if (hasArt && !dead && !ripe && v != null)
            {
                if (v.id == "hx") pv.plant.color = new Color(0.94f, 1f, 0.96f, 1f);
                else if (v.id == "jxiu") pv.plant.color = new Color(1f, 0.99f, 0.88f, 1f);
                else pv.plant.color = Color.white;
            }
            else pv.plant.color = Color.white;

            float sd = FarmData.ST[st].dur * v.dur;
            float pct = dead ? 100f : ripe ? 100f :
                Mathf.Clamp01(sd <= 0 ? 1f : pl.prog / sd) * 100f;
            pv.waterFill.sizeDelta = new Vector2(L.plotW * 0.34f * Mathf.Clamp01(pl.water / 100f), 8f * S);
            pv.progFill.sizeDelta = new Vector2((L.plotW - 16f * S) * pct / 100f, 10f * S);
            pv.overWet.gameObject.SetActive(!dead && pl.st >= 6 && pl.water > 90);

            // 左上状态徽章：仅在状态变化时重建；告警期间附带 10 秒生死线倒计时
            List<string[]> badges = new List<string[]>();
            string sig = "";
            float left = dead ? 0f : Mathf.Max(0f, FarmData.DANGER_SEC - pl.danger);
            bool danger = !dead && pl.danger > 0f;
            int secs = Mathf.CeilToInt(left);
            bool urgent = danger && secs <= 3;
            string secTag = danger ? " " + secs + "s" : "";
            string[] bg = { "#DCFCE7", "#FEE2E2", "#E0F2FE" };
            string[] fg = { "#166534", "#B91C1C", "#0369A1" };
            if (urgent)
            {
                bg = new string[] { "#FEE2E2", "#FEE2E2", "#FEE2E2" };
                fg = new string[] { "#B91C1C", "#B91C1C", "#B91C1C" };
            }
            if (dead) badges.Add(new string[] { "clean", "清理枯苗", "#F1F5F9", "#334155" });
            else
            {
                if (ripe) badges.Add(new string[] { "pick", "可收获！", "#FDE047", "#713F12" });
                if (pl.st == 4f && !pl.pol) badges.Add(new string[] { "pol", "授粉！", "#FEF9C3", "#A16207" });
                if (pl.weed) badges.Add(new string[] { "weed", "有草" + secTag, bg[0], fg[0] });
                if (!string.IsNullOrEmpty(pl.pest))
                    badges.Add(new string[] { "pest", pl.pest + secTag, bg[1], fg[1] });
                if (pl.water < FarmData.DANGER_WATER)
                    badges.Add(new string[] { "water", "缺水" + secTag, bg[2], fg[2] });
            }
            for (int b = 0; b < badges.Count; b++) sig += badges[b][0] + badges[b][1] + "|";
            if (sig != pv.badgeSig)
            {
                pv.badgeSig = sig;
                UIKit.ClearChildren(pv.badgeHost);
                for (int b = 0; b < badges.Count; b++)
                {
                    string kind = badges[b][0];
                    int idx = i;
                    UIKit.Button(pv.badgeHost, "Badge_" + kind, 0, b * 28f * S, 116f * S, 24f * S,
                        badges[b][1], 12, C(badges[b][2]), C(badges[b][3]),
                        delegate { M.Badge(idx, kind); }, (int)(11f * S));
                }
            }
        }

        // ══════════ 日志 ══════════
        public void AddLog(string type, string text)
        {
            logs.Add(new string[] { type, text });
            if (logs.Count > 60) logs.RemoveAt(0);
            logVersion++;
        }

        void RenderLogs()
        {
            if (logVersion == lastLogVersion) return;
            lastLogVersion = logVersion;
            UIKit.ClearChildren(logHost);
            float S = L.S, rowH = L.logRowH, rowBgH = rowH - 2f * S;
            int shown = 0;
            for (int k = logs.Count - 1; k >= 0 && shown < L.logMax; k--, shown++)
            {
                string type = logs[k][0], msg = logs[k][1];
                string bg = "#FFFFFF", tag = "#4338CA", label = "系统";
                if (type == "tip") { bg = "#EFFDF4"; tag = "#16A34A"; label = "提示"; }
                else if (type == "warn") { bg = "#FFF7ED"; tag = "#B45309"; label = "注意"; }
                else if (type == "bad") { bg = "#FEF2F2"; tag = "#DC2626"; label = "警示"; }
                else bg = "#EEF2FF";
                UIKit.Img(logHost, "LogBG" + shown, 0, shown * rowH, L.logW, rowBgH,
                    C(bg), SpriteFactory.Panel((int)L.logW, (int)rowBgH, (int)(8f * S)));
                UIKit.Txt(logHost, "LogTxt" + shown, 10f * S, shown * rowH, L.logW - 20f * S, rowBgH,
                    "<color=" + tag + "><b>[" + label + "]</b></color> " + msg, 12,
                    C("#374151"), TextAnchor.MiddleLeft);
            }
        }

        // 卡片内文字一律「换行 + 截断」，配合固定行高，彻底避免溢出卡片
        static Text FitTxt(Transform parent, string name, float x, float y, float w, float h,
            string content, int size, Color color, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            Text t = UIKit.Txt(parent, name, x, y, w, h, content, size, color, align, style);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.lineSpacing = 1.05f;
            return t;
        }

        // ── 提示条：普通弹窗在正文下；商城则移到「购买区下方」 ──
        static float BuildTip(RectTransform content, ModalSpec spec, float q,
            float inner, float y, bool dark, string label)
        {
            if (string.IsNullOrEmpty(spec.tip)) return 0f;
            float tipW = inner - 20f * q;
            string rich = "<b>" + label + "</b>" + spec.tip;
            float th = UIKit.EstimateHeight(rich, tipW, 13);
            Color fg = dark ? Color.white : (spec.shopMode ? C("#475569") : C("#166534"));
            Text tipTxt = UIKit.Txt(content, "TipTxt", 22f * q, y + 4f * q, tipW, 10f * q,
                rich, 13, fg, TextAnchor.UpperLeft);
            if (spec.shopMode)
            {
                // 补充说明用 UGUI 实测高度，避免保守估算多出两行空白
                Canvas.ForceUpdateCanvases();
                th = Mathf.Max(34f * q, tipTxt.preferredHeight + 12f * q);
                tipTxt.rectTransform.sizeDelta = new Vector2(tipW, th - 8f * q);
            }
            int tipH = Mathf.Max(24, Mathf.RoundToInt(th / 8f) * 8);
            Color bg = dark ? new Color(1, 1, 1, 0.16f)
                : (spec.shopMode ? C("#F1F5F9") : C("#EFFDF4"));
            UIKit.Img(content, "TipBG", 12f * q, y, inner, th, bg,
                SpriteFactory.Panel((int)inner, tipH, (int)(10f * q)), false);
            if (spec.shopMode)   // 购买区下方的补充说明用灰竖条弱化，不抢购买按钮的视觉重心
                UIKit.Img(content, "TipBar", 12f * q, y, 5f * q, th, C("#94A3B8"), null, false);
            tipTxt.transform.SetAsLastSibling();   // 背景在文字下层
            return th + 10f * q;
        }

        // ── 商城醒目购买区：深绿分区头写明购买方式 + 大号商品卡（价格 / 效果 / 立即购买按钮）──
        float BuildShopSection(RectTransform content, ModalSpec spec, float q,
            float inner, float y)
        {
            const float HEAD_H = 56f;
            const float CARD_H = 88f;
            const float GAP = 8f;
            UIKit.Img(content, "ShopHeader", 12f * q, y, inner, HEAD_H * q, C("#15803D"),
                SpriteFactory.Panel((int)inner, (int)(HEAD_H * q), (int)(12f * q)), false);
            FitTxt(content, "ShopHeaderTxt", 24f * q, y + 4f * q, inner * 0.66f, 26f * q,
                spec.shopHeader, 16, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            FitTxt(content, "ShopHeaderSub", 24f * q, y + 30f * q, inner * 0.72f, 22f * q,
                spec.shopSub, 11, new Color(1, 1, 1, 0.88f), TextAnchor.MiddleLeft);
            FitTxt(content, "ShopHeaderCnt", 12f * q + inner * 0.76f, y + 6f * q,
                inner * 0.21f, 44f * q, "共 " + spec.rows.Count + " 件商品\n点右侧按钮 ↓",
                12, new Color(1, 1, 1, 0.92f), TextAnchor.MiddleRight, FontStyle.Bold);

            float start = y + (HEAD_H + 8f) * q;
            for (int r = 0; r < spec.rows.Count; r++)
            {
                ModalRow row = spec.rows[r];
                float cy = start + r * (CARD_H + GAP) * q;
                bool owned = row.owned, afford = row.afford;
                Color accent = owned ? C("#94A3B8") : (afford ? C("#16A34A") : C("#F59E0B"));
                RectTransform card = UIKit.Place(content, "ShopItem" + r, 12f * q, cy, inner, CARD_H * q);
                Image cardBase = card.gameObject.AddComponent<Image>();
                cardBase.color = new Color(0, 0, 0, 0);
                cardBase.raycastTarget = false;
                card.gameObject.AddComponent<RectMask2D>();
                UIKit.Img(card, "Border", 0, 0, inner, CARD_H * q, accent,
                    SpriteFactory.Panel((int)inner, (int)(CARD_H * q), (int)(13f * q)), false);
                UIKit.Img(card, "Inner", 3f * q, 3f * q, inner - 6f * q, CARD_H * q - 6f * q,
                    owned ? C("#F1F5F9") : C("#F7FEF9"),
                    SpriteFactory.Panel((int)(inner - 6f * q), (int)(CARD_H * q - 6f * q),
                        (int)(11f * q)), false);
                UIKit.Img(card, "Accent", 3f * q, 3f * q, 7f * q, CARD_H * q - 6f * q,
                    accent, null, false);
                UIKit.Img(card, "NoBg", 20f * q, 22f * q, 44f * q, 44f * q, accent,
                    SpriteFactory.Panel((int)(44f * q), (int)(44f * q), (int)(22f * q)), false);
                FitTxt(card, "No", 20f * q, 22f * q, 44f * q, 44f * q,
                    (r + 1).ToString("00"), 17, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

                float btnW = 166f * q, priceW = 108f * q;
                float tx = 76f * q;
                float tw = inner - tx - btnW - priceW - 26f * q;   // 商品名 / 说明 / 徽章区
                float px = inner - btnW - priceW - 16f * q;         // 价格区
                float bx = inner - btnW - 12f * q;                   // 购买按钮
                FitTxt(card, "ShopName", tx, 7f * q, tw, 28f * q,
                    row.title, 17, owned ? C("#64748B") : C("#14532D"),
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                FitTxt(card, "ShopDesc", tx, 36f * q, tw, 32f * q,
                    row.desc, 11, C("#475569"), TextAnchor.UpperLeft);
                FitTxt(card, "ShopTag", tx, 68f * q, tw, 17f * q,
                    "· " + row.tag, 10, owned ? C("#94A3B8") : C("#15803D"), TextAnchor.MiddleLeft);

                FitTxt(card, "ShopPriceLbl", px, 12f * q, priceW, 17f * q,
                    "售价", 10, C("#94A3B8"), TextAnchor.MiddleCenter);
                FitTxt(card, "ShopPrice", px, 29f * q, priceW, 34f * q,
                    row.price, 18, owned ? C("#94A3B8") : C("#EA580C"),
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                FitTxt(card, "ShopPriceSub", px, 64f * q, priceW, 17f * q,
                    owned ? "已生效" : "一次买断", 9, C("#94A3B8"), TextAnchor.MiddleCenter);

                System.Action rowAction = row.onClick;
                UnityAction act = delegate { CloseTopModal(); if (rowAction != null) rowAction(); };
                string cap = owned ? "已拥有" : (afford ? "立即购买" : "西瓜币不足");
                Color bg = owned ? C("#CBD5E1") : (afford ? C("#16A34A") : C("#F59E0B"));
                UIKit.Button(card, "ShopBuy" + r, bx, 20f * q, btnW, 48f * q,
                    cap, 16, bg, owned ? C("#475569") : Color.white, act, (int)(12f * q));
                FitTxt(card, "ShopBuyHint", bx, 69f * q, btnW, 17f * q,
                    owned ? "全场永久生效" : (afford ? "点此下单 · 立即到账" : "先去仓库卖瓜"),
                    9, C("#64748B"), TextAnchor.MiddleCenter);
            }
            return (HEAD_H + 8f) * q + spec.rows.Count * (CARD_H + GAP) * q;
        }

        // ── 图鉴收藏卡：左侧网络实物资料图 + 右侧结构化成绩，卡内所有元素被 RectMask2D 裁切 ──
        void BuildBookCard(RectTransform content, float q, float x, float y, float w,
            BookCardInfo bc, int idx)
        {
            const float CH = 148f;
            Color accent = C(bc.tierHex);
            RectTransform card = UIKit.Place(content, "BookCard" + idx, x, y, w, CH * q);
            Image cardBase = card.gameObject.AddComponent<Image>();
            cardBase.color = new Color(0, 0, 0, 0);
            cardBase.raycastTarget = false;
            card.gameObject.AddComponent<RectMask2D>();
            UIKit.Img(card, "Border", 0, 0, w, CH * q, accent,
                SpriteFactory.Panel((int)w, (int)(CH * q), (int)(14f * q)), false);
            UIKit.Img(card, "Inner", 3f * q, 3f * q, w - 6f * q, CH * q - 6f * q,
                bc.got ? C("#FFFDF5") : C("#F1F5F9"),
                SpriteFactory.Panel((int)(w - 6f * q), (int)(CH * q - 6f * q), (int)(12f * q)), false);

            // 实物照片（网络公开资料图），未解锁时压暗并加「未解锁」条
            float px = 12f * q, py = 8f * q, pw = 140f * q, ph = 118f * q;
            UIKit.Img(card, "PhotoFrame", px, py, pw, ph, C("#DCEFD3"),
                SpriteFactory.Panel((int)pw, (int)ph, (int)(10f * q)), false);
            Sprite photo = ArtBank.Photo(bc.vid);
            if (photo == null) photo = ArtBank.Ripe(bc.vid);
            if (photo == null && hasArt) photo = ArtBank.Get("plant7");
            if (photo != null)
            {
                Image pi = UIKit.Img(card, "Photo", px, py, pw, ph,
                    bc.got ? Color.white : new Color(0.58f, 0.63f, 0.58f, 0.42f), photo, false);
                pi.preserveAspect = true;
            }
            else
                FitTxt(card, "PhotoFallback", px, py + 44f * q, pw, 30f * q, "西瓜", 18,
                    C("#64748B"), TextAnchor.MiddleCenter, FontStyle.Bold);
            if (!bc.got)
            {
                UIKit.Img(card, "LockBG", px, py + 44f * q, pw, 30f * q,
                    new Color(0.07f, 0.14f, 0.10f, 0.68f),
                    SpriteFactory.Panel((int)pw, (int)(30f * q), (int)(8f * q)), false);
                FitTxt(card, "LockTxt", px, py + 44f * q, pw, 30f * q, "未 解 锁", 13,
                    Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            FitTxt(card, "PhotoCap", px, 126f * q, pw, 16f * q, "实物资料图 · 网络", 9,
                C("#64748B"), TextAnchor.MiddleCenter);

            // 右侧信息区
            float rx = 164f * q, rw = w - 174f * q;
            float badgeW = 148f * q;
            Color titleColor = bc.got ? C("#14532D") : C("#64748B");
            FitTxt(card, "No", rx, 7f * q, 46f * q, 26f * q,
                "No." + bc.no.ToString("00"), 10, accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            FitTxt(card, "Name", rx + 46f * q, 5f * q, rw - 46f * q - badgeW - 8f * q, 30f * q,
                bc.name, 18, titleColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            float badgeX = rx + rw - badgeW;
            UIKit.Img(card, "TierBG", badgeX, 8f * q, badgeW, 26f * q,
                bc.got ? accent : C("#CBD5E1"),
                SpriteFactory.Panel((int)badgeW, (int)(26f * q), (int)(13f * q)), false);
            FitTxt(card, "Tier", badgeX, 8f * q, badgeW, 26f * q,
                bc.tier + " " + bc.stars, 10, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);

            FitTxt(card, "Desc", rx, 35f * q, rw, 20f * q, bc.desc, 11,
                C("#475569"), TextAnchor.MiddleLeft);

            string[] labs = { "最佳品质", "中心糖度", "单瓜重", "售价基数" };
            string[] vals = {
                bc.quality, bc.brix == "--" ? "--" : bc.brix + "°",
                bc.wt == "--" ? "--" : bc.wt + "kg", bc.coins + "金"
            };
            float gap = 8f * q, chipW = (rw - 3f * gap) / 4f;
            for (int i = 0; i < 4; i++)
            {
                float cx = rx + i * (chipW + gap);
                UIKit.Img(card, "ChipBG" + i, cx, 58f * q, chipW, 38f * q,
                    bc.got ? C("#F1F7EC") : C("#E2E8F0"),
                    SpriteFactory.Panel((int)chipW, (int)(38f * q), (int)(8f * q)), false);
                FitTxt(card, "ChipLbl" + i, cx, 59f * q, chipW, 14f * q, labs[i], 9,
                    C("#64748B"), TextAnchor.MiddleCenter);
                FitTxt(card, "ChipVal" + i, cx, 73f * q, chipW, 22f * q, vals[i], 13,
                    bc.got ? accent : C("#94A3B8"), TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            FitTxt(card, "File", rx, 100f * q, rw, 32f * q,
                "<b>品种档案 · </b>" + bc.file, 10, C("#334155"), TextAnchor.UpperLeft);
            FitTxt(card, "State", rx, 132f * q, rw, 14f * q,
                bc.got ? "★ 已收入图鉴 · 记录的是你的最佳一瓜" : "种出该品种即可点亮此卡",
                9, bc.got ? accent : C("#94A3B8"), TextAnchor.MiddleLeft);
        }

        // ══════════ 弹窗 ══════════
        public void ShowModal(ModalSpec spec)
        {
            float q = L.modalScale;
            bool book = spec.book.Count > 0;
            bool shop = spec.shopMode && !book;
            // 图鉴 / 商城用更宽更高的面板承载收藏卡与醒目购买区，其余弹窗保持原尺寸
            float pw = (book ? 880f : (shop ? 820f : 660f)) * q;
            float ph = (book ? 760f : (shop ? 660f : 680f)) * q;
            float cw = pw - 32f * q;             // 滚动内容区宽度
            float inner = cw - 24f * q;          // 行 / 卡片宽度
            float scrollH = ph - 54f * q - 78f * q;
            float btnY = ph - 64f * q;

            GameObject overlayGO = new GameObject("Modal", typeof(RectTransform));
            overlayGO.transform.SetParent(modalLayer, false);
            RectTransform ort = (RectTransform)overlayGO.transform;
            ort.anchorMin = Vector2.zero;
            ort.anchorMax = Vector2.one;
            ort.offsetMin = Vector2.zero;
            ort.offsetMax = Vector2.zero;
            Image maskImg = overlayGO.AddComponent<Image>();
            maskImg.color = new Color(0.06f, 0.09f, 0.16f, 0.62f);
            maskImg.raycastTarget = true;
            Button maskBtn = overlayGO.AddComponent<Button>();
            maskBtn.targetGraphic = maskImg;
            maskBtn.transition = Selectable.Transition.None;
            maskBtn.onClick.AddListener(delegate { CloseTopModal(); });

            GameObject panelGO = new GameObject("Panel", typeof(RectTransform));
            panelGO.transform.SetParent(ort, false);
            RectTransform panel = (RectTransform)panelGO.transform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(pw, ph);
            Image panelImg = panelGO.AddComponent<Image>();
            panelImg.sprite = SpriteFactory.Panel((int)pw, (int)ph, (int)(18f * q));
            panelImg.color = Color.white;
            panelImg.raycastTarget = true;
            Button absorber = panelGO.AddComponent<Button>();   // 吸收点击，避免误关弹窗
            absorber.targetGraphic = panelImg;
            absorber.transition = Selectable.Transition.None;

            UIKit.Txt(panel, "Title", 18f * q, 14f * q, pw - 90f * q, 32f * q, spec.title, 20,
                C("#14532D"), TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Button(panel, "Close", pw - 46f * q, 12f * q, 32f * q, 32f * q, "×", 20,
                C("#F1F5F9"), C("#64748B"), delegate { CloseTopModal(); }, (int)(16f * q));

            RectTransform content;
            ScrollRect sr = UIKit.Scroll(panel, "BodyScroll", 16f * q, 54f * q,
                cw, scrollH, spec.bodyBack, out content);
            bool dark = spec.bodyText == Color.white;
            float cy = 8f * q;

            if (!string.IsNullOrEmpty(spec.body))
            {
                // 收获科普卡：正文右侧同时展示「网络实物照片」与「游戏内品种插画」
                Sprite cardArt = string.IsNullOrEmpty(spec.artVid) ? null : ArtBank.Ripe(spec.artVid);
                Sprite cardPhoto = string.IsNullOrEmpty(spec.photoVid) ? null : ArtBank.Photo(spec.photoVid);
                float bodyW = cardArt != null ? inner - 156f * q : inner;
                float bh;
                if (shop)
                {
                    // 商城正文是固定短文案：用 UGUI 实测高度排版，避免保守估算在购买区上方留大块空白
                    Text bt = UIKit.Txt(content, "Body", 12f * q, cy, bodyW, 10f * q,
                        spec.body, 15, spec.bodyText, TextAnchor.UpperLeft);
                    Canvas.ForceUpdateCanvases();
                    bh = Mathf.Max(76f * q, bt.preferredHeight + 8f * q);
                    bt.rectTransform.sizeDelta = new Vector2(bodyW, bh);
                }
                else
                {
                    bh = UIKit.EstimateHeight(spec.body, bodyW, 15);
                    UIKit.Txt(content, "Body", 12f * q, cy, bodyW, bh, spec.body, 15,
                        spec.bodyText, TextAnchor.UpperLeft);
                }
                if (cardArt != null)
                {
                    float ax = 12f * q + bodyW + 10f * q;
                    float aw = inner - bodyW - 10f * q;
                    if (cardPhoto != null)
                    {
                        Image pi = UIKit.Img(content, "CardPhoto", ax, cy + 4f * q,
                            aw, 96f * q, Color.white, cardPhoto);
                        pi.preserveAspect = true;
                        pi.raycastTarget = false;
                        FitTxt(content, "CardPhotoCap", ax, cy + 100f * q, aw, 16f * q,
                            "实物资料图", 10, spec.bodyText, TextAnchor.MiddleCenter);
                        Image ai = UIKit.Img(content, "CardArt", ax, cy + 120f * q,
                            aw, 78f * q, Color.white, cardArt);
                        ai.preserveAspect = true;
                        ai.raycastTarget = false;
                        FitTxt(content, "CardArtCap", ax, cy + 198f * q, aw, 16f * q,
                            "游戏内插画", 10, spec.bodyText, TextAnchor.MiddleCenter);
                        cy += Mathf.Max(bh, 222f * q) + 10f * q;
                    }
                    else
                    {
                        Image ca = UIKit.Img(content, "CardArt", ax, cy + 6f * q,
                            aw, 142f * q, Color.white, cardArt);
                        ca.preserveAspect = true;
                        ca.raycastTarget = false;
                        cy += Mathf.Max(bh, 152f * q) + 10f * q;
                    }
                }
                else cy += bh + 10f * q;
            }
            // 普通弹窗：提示在正文下；商城：先呈现醒目购买区，补充说明一律放到购买区下方
            if (!shop) cy += BuildTip(content, spec, q, inner, cy, dark, "提示：");
            if (shop)
            {
                cy += BuildShopSection(content, spec, q, inner, cy);
                cy += BuildTip(content, spec, q, inner, cy, dark, "补充说明：");
            }
            else
            {
                for (int r = 0; r < spec.rows.Count; r++)
                {
                    ModalRow row = spec.rows[r];
                    UIKit.Img(content, "RowBG" + r, 12f * q, cy, inner, 42f * q,
                        dark ? new Color(1, 1, 1, 0.14f) : C("#F8FAFC"),
                        SpriteFactory.Panel((int)inner, (int)(42f * q), (int)(10f * q)));
                    UIKit.Txt(content, "RowTxt" + r, 22f * q, cy + 2f * q, inner - 144f * q, 38f * q,
                        row.text, 13, spec.bodyText, TextAnchor.MiddleLeft);
                    System.Action rowAction = row.onClick;
                    UnityAction act = delegate { CloseTopModal(); if (rowAction != null) rowAction(); };
                    UIKit.Button(content, "RowBtn" + r, 12f * q + inner - 118f * q, cy + 4f * q,
                        110f * q, 34f * q, row.button, 12,
                        row.button == "已拥有" ? C("#9CA3AF") : C("#16A34A"),
                        Color.white, act, (int)(9f * q));
                    cy += 50f * q;
                }
            }
            for (int o = 0; o < spec.options.Count; o++)
            {
                ModalOption op = spec.options[o];
                System.Action optAction = op.onClick;
                UnityAction act = delegate { CloseTopModal(); if (optAction != null) optAction(); };
                Btn b = UIKit.Button(content, "Opt" + o, 12f * q, cy, inner, 66f * q,
                    "<b>" + op.title + "</b>\n" + op.sub, 13,
                    C("#F0FDF4"), C("#14532D"), act, (int)(12f * q));
                b.label.alignment = TextAnchor.MiddleLeft;
                cy += 74f * q;
            }
            if (spec.book.Count > 0)
            {
                for (int b = 0; b < spec.book.Count; b++)
                {
                    BuildBookCard(content, q, 12f * q, cy, inner, spec.book[b], b);
                    cy += 158f * q;
                }
                // 收集进度：把卡片下方的空白收尾，一眼看到图鉴完成度
                int gotCount = 0;
                for (int b = 0; b < spec.book.Count; b++) if (spec.book[b].got) gotCount++;
                float barH = 10f * q;
                UIKit.Img(content, "ProgBG", 12f * q, cy + 4f * q, inner, barH, C("#E2E8F0"),
                    SpriteFactory.Panel((int)inner, (int)barH, (int)(barH / 2f)), false);
                float fillW = inner * gotCount / Mathf.Max(1, spec.book.Count);
                if (fillW > 1f)
                    UIKit.Img(content, "ProgFill", 12f * q, cy + 4f * q, fillW, barH, C("#16A34A"),
                        SpriteFactory.Panel((int)fillW, (int)barH, (int)(barH / 2f)), false);
                FitTxt(content, "ProgTxt", 12f * q, cy + 18f * q, inner, 20f * q,
                    "已收集 " + gotCount + " / " + spec.book.Count + " 张品种收藏卡",
                    10, C("#64748B"), TextAnchor.MiddleCenter);
                cy += 44f * q;
            }

            content.sizeDelta = new Vector2(0, cy + 12f * q);
            sr.verticalNormalizedPosition = 1f;

            List<ModalButton> btns = spec.buttons;
            if (btns.Count == 0)
                btns = new List<ModalButton> { new ModalButton("关闭", true, null) };
            int n = btns.Count;
            float bw = (cw - 8f * q * (n - 1)) / n;
            for (int b = 0; b < n; b++)
            {
                ModalButton mb = btns[b];
                System.Action btnAction = mb.onClick;
                UnityAction act = delegate { CloseTopModal(); if (btnAction != null) btnAction(); };
                UIKit.Button(panel, "Btn" + b, 16f * q + b * (bw + 8f * q), btnY, bw, 48f * q,
                    mb.label, 14,
                    mb.primary ? C("#16A34A") : Color.white,
                    mb.primary ? Color.white : C("#374151"), act, (int)(10f * q));
            }

            modals.Add(overlayGO);
        }

        public void CloseTopModal()
        {
            if (modals.Count == 0) return;
            GameObject g = modals[modals.Count - 1];
            modals.RemoveAt(modals.Count - 1);
            if (g != null) { g.SetActive(false); Object.Destroy(g); }
        }

        // ══════════ 轻量 Toast：西瓜币到账（2 秒）/ 瓜苗枯萎遗憾提示（4 秒） ══════════
        // 全屏透明 Button 吸收任意点击；卡片上的图形全部 raycastTarget=false，
        // 因此点卡片本身也会穿透到全屏 Button 上并关闭提示。
        public void ShowShardToast(int gained, int total)
        {
            if (toastLayer == null || L == null) return;
            toastGained += gained;
            int shownTotal = total > 0 ? total : ShardWallet.Get(ShardWallet.WATERMELON);
            ShowToast("shard", 2f,
                "★ 西瓜币到账",
                "西瓜币 +" + toastGained,
                "西瓜币 " + shownTotal + " 个",
                "每卖出 1 个西瓜 +" + ShardWallet.PER_MELON + " 西瓜币 · 西瓜币永久保存，可跨游戏联动",
                "点击屏幕任意处关闭 · 2 秒后自动消失");
        }

        // 田间告警超时枯萎：点击屏幕任意处或 4 秒后自动消失
        public void ShowWiltToast(string variety, string cause)
        {
            if (toastLayer == null || L == null) return;
            toastGained = 0;
            ShowToast("wilt", 4f,
                "!! 田间告警",
                "真遗憾……" + variety + "枯萎了",
                cause + " " + FarmData.DANGER_SEC.ToString("F0") + " 秒未处理 · 点击清理可补种",
                "看到「有草 / 蚜虫 / 缺水」徽章后 10 秒内处理即可解除倒计时",
                "点击屏幕任意处关闭 · 4 秒后自动消失");
        }

        void ShowToast(string kind, float seconds, string tag, string title, string sub,
            string rule, string hint)
        {
            toastTimer = seconds;              // 真实时间，不受游戏倍速影响
            if (toastGO == null) BuildToast();
            bool wilt = kind == "wilt";
            if (toastBorder != null) toastBorder.color = C(wilt ? "#DC2626" : "#FBBF24");
            if (toastCardBG != null) toastCardBG.color = C(wilt ? "#FEF2F2" : "#FFFBEB");
            if (toastMelon != null)
            {
                toastMelon.sprite = SpriteFactory.Plant(7, wilt, "jx");
                toastMelon.color = wilt ? new Color(0.80f, 0.74f, 0.68f, 1f) : Color.white;
            }
            if (toastTag != null)
            {
                toastTag.text = tag;
                toastTag.color = C(wilt ? "#B91C1C" : "#B45309");
            }
            toastTitle.text = title;
            toastTitle.fontSize = UIKit.FS(wilt ? 21 : 26);
            toastTitle.color = C(wilt ? "#B91C1C" : "#15803D");
            toastSub.text = sub;
            toastSub.fontSize = UIKit.FS(wilt ? 14 : 17);
            if (toastRule != null)
            {
                toastRule.text = rule;
                toastRule.color = C(wilt ? "#9F1239" : "#92400E");
            }
            if (toastHint != null) toastHint.text = hint;
            toastGO.SetActive(true);
            toastLayer.SetAsLastSibling();
        }

        void BuildToast()
        {
            float S = L.S;
            GameObject host = new GameObject("ToastHitArea", typeof(RectTransform));
            host.transform.SetParent(toastLayer, false);
            RectTransform hrt = (RectTransform)host.transform;
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            Image hit = host.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0.01f);
            hit.raycastTarget = true;
            Button hitBtn = host.AddComponent<Button>();
            hitBtn.targetGraphic = hit;
            hitBtn.transition = Selectable.Transition.None;
            hitBtn.onClick.AddListener(delegate { HideToast(); });
            toastGO = host;

            float w = 480f * S, h = 256f * S;
            GameObject cardGO = new GameObject("ToastCard", typeof(RectTransform));
            cardGO.transform.SetParent(hrt, false);
            RectTransform card = (RectTransform)cardGO.transform;
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.sizeDelta = new Vector2(w, h);

            toastBorder = UIKit.Img(card, "Border", 0, 0, w, h, C("#FBBF24"),
                SpriteFactory.Panel((int)w, (int)h, (int)(22f * S)), false);
            toastCardBG = UIKit.Img(card, "CardBG", 4f * S, 4f * S, w - 8f * S, h - 8f * S, C("#FFFBEB"),
                SpriteFactory.Panel((int)(w - 8f * S), (int)(h - 8f * S), (int)(18f * S)), false);

            toastMelon = UIKit.Img(card, "Melon", 22f * S, 42f * S, 118f * S, 118f * S,
                Color.white, SpriteFactory.Plant(7, false, "jx"), false);
            toastMelon.preserveAspect = true;

            toastTag = UIKit.Txt(card, "Tag", 168f * S, 28f * S, w - 190f * S, 26f * S,
                "★ 西瓜币到账", 13, C("#B45309"), TextAnchor.MiddleLeft, FontStyle.Bold);
            toastTitle = UIKit.Txt(card, "Title", 168f * S, 56f * S, w - 190f * S, 54f * S,
                "西瓜币 +2", 26, C("#15803D"), TextAnchor.MiddleLeft, FontStyle.Bold);
            toastSub = UIKit.Txt(card, "Sub", 168f * S, 112f * S, w - 190f * S, 38f * S,
                "西瓜币 0 个", 17, C("#334155"), TextAnchor.MiddleLeft);
            toastRule = UIKit.Txt(card, "Rule", 22f * S, 172f * S, w - 44f * S, 26f * S,
                "每卖出 1 个西瓜 +" + ShardWallet.PER_MELON + " 西瓜币 · 西瓜币永久保存，可跨游戏联动",
                12, C("#92400E"), TextAnchor.MiddleCenter);
            toastHint = UIKit.Txt(card, "Hint", 22f * S, h - 42f * S, w - 44f * S, 28f * S,
                "点击屏幕任意处关闭 · 2 秒后自动消失", 13, C("#64748B"), TextAnchor.MiddleCenter);
        }

        void HideToast()
        {
            toastTimer = 0f;
            toastGained = 0;
            if (toastGO != null)
            {
                toastGO.SetActive(false);
#if UNITY_EDITOR
                // 编辑期自检会真实构建 Toast，Destroy 在非播放模式不生效，改用立即销毁
                if (!Application.isPlaying) Object.DestroyImmediate(toastGO);
                else Object.Destroy(toastGO);
#else
                Object.Destroy(toastGO);
#endif
                toastGO = null;
            }
            toastTitle = null; toastSub = null;
            toastTag = null; toastRule = null; toastHint = null;
            toastBorder = null; toastCardBG = null; toastMelon = null;
        }

        // ══════════ 动画 ══════════
        public void TickVisual(float dt)
        {
            TickCursor();   // 光标跟随鼠标，独立于布局层
            // 西瓜币 / 遗憾提示用 unscaledDeltaTime 计时，不受倍速影响
            if (toastGO != null)
            {
                toastTimer -= dt;
                if (toastTimer <= 0f) HideToast();
            }
            if (root == null || L == null) return;
            animTime += dt;
            if (sunRt != null) sunRt.localRotation = Quaternion.Euler(0, 0, -animTime * 360f / 14f);
            float span = L.farmW + 300f * L.S;
            if (cloud1 != null)
                cloud1.anchoredPosition = new Vector2(-150f * L.S + Mathf.Repeat(animTime / 34f, 1f) * span,
                    -12f * L.S);
            if (cloud2 != null)
                cloud2.anchoredPosition = new Vector2(-120f * L.S + Mathf.Repeat(animTime / 50f + 0.4f, 1f) * span,
                    -38f * L.S);

            factTimer += dt;
            if (factTimer >= 6f)
            {
                factTimer -= 6f;
                factIndex++;
                tickerText.text = "【小知识】" + FarmData.FACTS[factIndex % FarmData.FACTS.Length];
            }
            for (int i = 0; i < plots.Count; i++)
            {
                PlotView pv = plots[i];
                if (pv.ripe && pv.plantRt != null)
                    pv.plantRt.anchoredPosition = pv.plantBase +
                        new Vector2(0, Mathf.Abs(Mathf.Sin(animTime * 3.2f)) * 7f * L.S);
            }
        }
    }
}
