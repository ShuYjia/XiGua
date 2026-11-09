using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/* ============================================================
 * GuiliUI.cs —— 全套 uGUI 界面（像素级对齐 css/atelier.css）
 *
 * 设计基准 1920 x 1080。调用处仍写 1440x900 网页稿数值，
 * 由 GuiliUIKit.Legacy* 统一换算，禁止在调用点散落手工缩放数字。
 * 所有坐标直接取自 CSS：
 *   header 88px / #canvas-container left265 right136 top10 bottom132
 *   .journal left3% top30 width215 / #tools right3% top30
 *   .knowledge left3% right3% bottom20 height87 / #view-caption left40% bottom145
 *   .sheet-overlay inset:88px 0 0 / .challenge-sheet width1000 padding24 38
 *   #cut-panel right4% top140 width320 / cut 视口 left4% right390
 * 配色 --paper #f4efdc --ink #333b2c --green #405b42 --red #9f4b3b
 *      --line #bab398 --muted #8a8b72，楷体标题 + 黑体正文。
 *
 * 关键修正：
 *   1) 旧版用 alpha .55 的纸纹图全屏覆盖，把 3D 视口也染成灰黄；
 *      网页 .paper-grain 仅 opacity .06。现改为纸面上叠 .06，3D 区域通透。
 *   2) 旧版按 1600x900 估坐标且 pivot 取中心，导致整体错位、文字互相压叠；
 *      现统一用左上锚点 (0,1) + CSS 数值定位，并禁止文本 Overflow。
 *   3) 切瓜阶段旧版整屏铺不透明纸底，遮住了 3D 剖面；现改为
 *      左侧视口 + 右侧 320px 半透明卡，与网页 v5-cut.png 一致。
 * ============================================================ */

public class GuiliUI : MonoBehaviour
{
    public static GuiliUI Instance { get; private set; }
    public GuiliGame Game;
    public GuiliScene Scene;

    Canvas canvas;
    GameObject homeGo, playGo, challengeGo, cutGo, resultGo, modalGo, toastGo;
    GameObject journalGo, toolsGo, viewControlsGo, captionGo, knowledgeGo, waveGo, labelsGo;
    public RectTransform PlayViewport { get; private set; }
    public RectTransform CutViewport { get; private set; }

    Text brandText, brandSubText, settingsText;
    Text currencyText, rewardText;
    Button giftRedeemButton;
    Transform panelParent;
    Text[] stageNum = new Text[5], stageName = new Text[5];
    Image[] stageBox = new Image[5];

    Text chapterText, taskText, hintText, footText, knowledgeText, captionText, ledgerProgress;
    Text nextLabel;
    Button nextButton;
    readonly Text[] ledgerId = new Text[9], ledgerSub = new Text[9];
    readonly Image[] ledgerBg = new Image[9];
    readonly Outline[] ledgerOutline = new Outline[9];
    readonly Text[] tutorialStepText = new Text[4];
    readonly Image[] tutorialStepIcon = new Image[4];
    GameObject tutorialStepsGo, ledgerGridGo;

    readonly Button[] toolButtons = new Button[6];
    readonly Text[] toolLabels = new Text[6];
    readonly Text[] toolChecks = new Text[6];
    readonly Image[] toolIcons = new Image[6];
    readonly string[] toolKeys = { "skin", "spot", "stem", "sound", "record", "back" };
    Button viewOrbit, viewReset, viewFollow;

    Text cutTitle, cutStatus, cutText, cutEvidence, cutCaption;
    GameObject cutCard, knifeGo;
    RectTransform knifeRt;
    CanvasGroup knifeGroup, cutGroup;
    Coroutine knifeRoutine;
    Vector2 cutCardHome, knifeHome;
    public bool CutVisible { get { return cutCard != null && cutCard.activeSelf; } }

    readonly Button[] choiceButtons = new Button[9];
    readonly Image[] choiceImages = new Image[9];
    readonly Outline[] choiceOutline = new Outline[9];
    readonly Text[] choiceSel = new Text[9];
    readonly Text[] choiceId = new Text[9];
    readonly Button[] reasonButtons = new Button[6];
    readonly Image[] reasonCheck = new Image[6];
    readonly Image[] reasonBg = new Image[6];
    Text challengeSummary, warnText;
    readonly List<string> reasons = new List<string>();
    string chosenId;

    Text scoreText, rankText, resultMessageText;
    readonly Image[] scoreFills = new Image[3];
    readonly Text[] scoreValues = new Text[3];

    Text modalTitle, modalBody;
    Transform modalExtra;
    Action modalDone;
    Button modalDoneButton, modalCloseButton;
    public Button ModalDoneButton { get { return modalDoneButton; } }
    public Button ModalCloseButton { get { return modalCloseButton; } }
    public bool ModalOpen { get { return modalGo != null && modalGo.activeSelf; } }

    Text toastText;
    readonly Image[] waveBars = new Image[8];
    Text waveLabel;
    Coroutine waveRoutine;
    /* 「04 拍一拍」听音三辨卡：四张小卡，当前音色高亮 */
    GameObject knockGo;
    readonly Image[] knockCardBg = new Image[4];
    readonly Text[] knockCardTitle = new Text[4];
    readonly Text[] knockCardBody = new Text[4];
    readonly Text[] labelTexts = new Text[9];
    readonly RectTransform[] labelRoots = new RectTransform[9];

    /* 网页坐标系：相对父级左上角，x 向右、y 向下，与 CSS left/top 完全一致 */
    static RectTransform At(Transform parent, string name, float x, float y, float w, float h)
    {
        float lx = GuiliUIKit.LegacyX(x), ly = GuiliUIKit.LegacyY(y);
        float lw = GuiliUIKit.LegacyWidth(w), lh = GuiliUIKit.LegacyHeight(h);
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.offsetMin = new Vector2(lx, -ly - lh);
        r.offsetMax = new Vector2(lx + lw, -ly);
        return r;
    }

    /* 在 At 容器内铺满一个文本 */
    static Text TextAt(Transform parent, string name, float x, float y, float w, float h,
                       string value, int size, Color color, TextAnchor align, bool kai = false)
    {
        RectTransform r = At(parent, name, x, y, w, h);
        return GuiliUIKit.T(name + "字", r, value, size, color, align, kai);
    }

    /* 带底色的格子（返回容器，Image 挂在其上） */
    static RectTransform BoxAt(Transform parent, string name, float x, float y, float w, float h, Color bg)
    {
        RectTransform r = At(parent, name, x, y, w, h);
        Image im = r.gameObject.AddComponent<Image>();
        im.color = bg;
        return r;
    }

    public void Build()
    {
        Instance = this;
        GuiliUIKit.LoadFonts();
        GameObject root = new GameObject("主界面Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler cs = root.GetComponent<CanvasScaler>();
        WatermelonResolution.EnsureCanvasScaler(cs);

        // 固定 1920x1080 内容根节点；缩放只由 CanvasScaler 做一次。
        // Cut 不铺全屏背景 Image，避免遮住 3D 瓜摊；非 16:9 的留白由相机 rect 之外承担。
        RectTransform content = WatermelonResolution.EnsureContentRoot(root.transform);
        panelParent = content;

        BuildHeader(content);
        BuildHome(content);
        BuildPlay(content);
        BuildChallenge(content);
        BuildCut(content);
        BuildResult(content);
        BuildModal(content);
        BuildToast(content);
        HideAll();

        WatermelonCurrencyHUD hud = root.AddComponent<WatermelonCurrencyHUD>();
        hud.balanceText = currencyText;
        hud.giftButton = giftRedeemButton;
        hud.panelParent = content;
        hud.Refresh();
    }

    /* ---------------- header : height 88, padding 18px 4% ---------------- */
    void BuildHeader(Transform root)
    {
        RectTransform h = GuiliUIKit.R("顶部栏", root, new Vector2(0, 1), new Vector2(1, 1),
                                       new Vector2(0, -88), Vector2.zero);
        GuiliUIKit.Img("顶栏底", h, new Color(244 / 255f, 239 / 255f, 220 / 255f, .95f));
        GuiliUIKit.Box("顶栏细线", h, new Vector2(.5f, 0), Vector2.zero, new Vector2(3200, 1), GuiliData.Line);

        brandText = TextAt(h, "品牌", 58, 16, 320, 34, GuiliData.Brand, 26, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        brandSubText = TextAt(h, "副题", 58, 54, 320, 20, GuiliData.BrandSub, 10, GuiliData.Muted, TextAnchor.MiddleLeft, false);
        Button brandBtn = GuiliUIKit.Quiet("回首页", h, new Vector2(0, 1), new Vector2(208, -44),
                                           new Vector2(300, 70), "", () => Game.GoHome(), 12);

        /* nav: 5 个阶段，每列 84px，居中 */
        for (int i = 0; i < 5; i++)
        {
            float x = 510 + i * 84f;
            RectTransform box = At(h, "阶段框" + i, x, 30, 27, 27);
            stageBox[i] = box.gameObject.AddComponent<Image>();
            stageBox[i].color = new Color(0, 0, 0, 0);
            stageBox[i].raycastTarget = false;
            GuiliUIKit.Border(stageBox[i], GuiliData.Line, 1f);
            stageNum[i] = GuiliUIKit.T("阶段数" + i, box, GuiliData.StageNumbers[i], 13,
                                       GuiliData.Muted, TextAnchor.MiddleCenter, true);
            stageName[i] = TextAt(h, "阶段名" + i, x + 34, 28, 60, 30,
                                  GuiliData.StageNames[i], 15, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        }
        Button setBtn = GuiliUIKit.Quiet("设置", h, new Vector2(1, 1), new Vector2(-103, -44),
                                         new Vector2(90, 34), "设置", () => Game.Settings(), 14);
        settingsText = setBtn.GetComponentInChildren<Text>();

        // 统一西瓜币余额与礼品兑换入口（顶部常驻，与 Farm / 主课堂共用同一服务）
        currencyText = TextAt(h, "西瓜币余额", 930, 8, 230, 30, "西瓜币 0", 15,
                              GuiliData.Green, TextAnchor.MiddleRight, true);
        giftRedeemButton = GuiliUIKit.Btn("礼品兑换",
            At(h, "兑换钮", 930, 42, 230, 34), new Vector2(.5f, .5f), Vector2.zero,
            new Vector2(230, 34), "礼品兑换",
            () => WatermelonGiftPanel.Open(panelParent), true, 14);

        /* 主工程入口：返回课堂。保留原顶部栏、首页、教学、重开/返回功能，
           这里只新增一个同风格的文字入口 */
        Button classroomBtn = null;
        classroomBtn = GuiliUIKit.Quiet("返回课堂", h, new Vector2(1, 1), new Vector2(-212, -44),
                                        new Vector2(110, 34), "返回课堂", () =>
                                        {
                                            if (classroomBtn != null) classroomBtn.interactable = false;
                                            SubgameSceneRouter.ReturnToClassroom();
                                        }, 14);
    }

    /* ---------------- 首页 : grid 52% / 48%, padding 2vh 7% 7vh ---------------- */
    void BuildHome(Transform root)
    {
        homeGo = GuiliUIKit.R("首页", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -88)).gameObject;
        Transform h = homeGo.transform;
        GuiliUIKit.PaperPanel("首页纸面", h);

        /* 左列插画：网页 .home-illustration>img 用 mix-blend-mode:multiply。
           Unity 端先做「底色归一化 + 正片叠底 + 墨浓度 alpha + 四周羽化」，
           白底彻底透明，所以不会露出一块浅色矩形；背后再垫一层极淡水彩晕，
           让工笔插画与纸面自然过渡，看不出是「垫了一张图」。 */
        RectTransform wash = At(h, "插画纸晕", 108, 12, 628, 730);
        Image washIm = GuiliUIKit.Img("晕", wash, Color.white);
        washIm.sprite = GuiliTextures.FromTexture(GuiliTextures.MakeWash(), "home-wash");
        washIm.preserveAspect = false;
        RectTransform pic = At(h, "工笔藤蔓西瓜插画", 100, 18, 644, 720);
        GuiliUIKit.Pic("插画", pic, "botanical", Color.white);

        /* 朱红印章：CSS 双线装帧（border 2px + outline 1px/offset 3px）+ 旋转 -3° */
        RectTransform seal = At(h, "印章", 177, 121, 68, 92);
        seal.localRotation = Quaternion.Euler(0, 0, -3f);
        Image sealOuter = GuiliUIKit.Img("印外框", seal, new Color(0, 0, 0, 0));
        GuiliUIKit.Border(sealOuter, GuiliData.Red, 1f);
        RectTransform sealInner = GuiliUIKit.R("印内框", seal, Vector2.zero, Vector2.one,
                                               new Vector2(5, 5), new Vector2(-5, -5));
        Image sealInnerIm = GuiliUIKit.Img("印框", sealInner, new Color(0, 0, 0, 0));
        GuiliUIKit.Border(sealInnerIm, GuiliData.Red, 2f);
        GuiliUIKit.T("印文", sealInner, GuiliData.HomeSeal, 19, GuiliData.Red, TextAnchor.MiddleCenter, true);

        /* 右列文案：按 CSS h1 clamp(70,7.4vw,116)=106px / line-height 1.08
           / letter-spacing 18px 重排；空格近似字距，单行大字不再被裁掉。 */
        TextAt(h, "眉题", 817, 88, 520, 22, GuiliData.HomeEyebrow, 11, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        TextAt(h, "大题一", 817, 128, 520, 124, GuiliData.HomeTitleA, 106, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        TextAt(h, "大题二", 817, 248, 520, 124, GuiliData.HomeTitleB, 106, GuiliData.Green, TextAnchor.MiddleLeft, true);
        TextAt(h, "引文", 817, 388, 520, 76, GuiliData.HomeLead, 17,
               GuiliData.Hex("75765f"), TextAnchor.UpperLeft, true);
        GuiliUIKit.Btn("展卷学挑瓜", At(h, "起", 817, 474, 190, 48), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(190, 48), GuiliData.HomeStart, () => Game.StartTutorial(), true, 17);
        GuiliUIKit.Btn("直接入瓜摊", At(h, "练", 1019, 474, 158, 48), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(158, 48), GuiliData.HomePractice, () => Game.SkipToPractice(), false, 17);
        GuiliUIKit.Quiet("认识庞各庄", At(h, "识", 817, 538, 180, 30), new Vector2(.5f, .5f), Vector2.zero,
                         new Vector2(180, 30), GuiliData.HomeAbout, () => Game.About(), 14);
        TextAt(h, "首页小字", 817, 586, 520, 22, GuiliData.HomeSmall, 10, GuiliData.Muted, TextAnchor.MiddleLeft, false);

        /* .vine-border{bottom:0;right:0;width:48%;height:120px} —— 贴右下角，随窗口高度自适应 */
        RectTransform vine = GuiliUIKit.Fixed("藤蔓边框", h, new Vector2(1, 0),
                                              new Vector2(-345.5f, 60), new Vector2(691, 120));
        GuiliUIKit.Pic("藤蔓", vine, "vine-border", new Color(1, 1, 1, .22f));
    }

    /* ---------------- 瓜摊 #play ---------------- */
    void BuildPlay(Transform root)
    {
        playGo = GuiliUIKit.R("瓜摊", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -88)).gameObject;
        Transform p = playGo.transform;
        /* left:265 right:136 top:10 bottom:132 */
        PlayViewport = GuiliUIKit.R("三维视口", p, Vector2.zero, Vector2.one,
                                    new Vector2(265, 132), new Vector2(-136, -10));

        /* 左侧手记 .journal left:3% top:30 width:215 */
        journalGo = At(p, "侧栏手记", 43, 30, 215, 520).gameObject;
        Transform j = journalGo.transform;
        chapterText = TextAt(j, "卷次", 0, 0, 215, 20, "", 11, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        taskText = TextAt(j, "任务", 0, 30, 215, 46, "", 30, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        hintText = TextAt(j, "提示", 0, 86, 215, 104, "", 12,
                          GuiliData.Hex("76745d"), TextAnchor.UpperLeft, false);

        ledgerGridGo = At(j, "九瓜记录", 0, 210, 215, 210).gameObject;
        ledgerProgress = TextAt(ledgerGridGo.transform, "进度", 0, 0, 215, 22, "", 11,
                                GuiliData.Hex("7d7e67"), TextAnchor.MiddleLeft, false);
        for (int i = 0; i < 9; i++)
        {
            float x = (i % 3) * 74f, y = 30 + (i / 3) * 63f;
            RectTransform cell = At(ledgerGridGo.transform, "编号" + i, x, y, 67, 56);
            Button b = cell.gameObject.AddComponent<Button>();
            ledgerBg[i] = cell.gameObject.GetComponent<Image>();
            if (ledgerBg[i] == null) ledgerBg[i] = cell.gameObject.AddComponent<Image>();
            ledgerBg[i].color = GuiliData.Cream;
            b.targetGraphic = ledgerBg[i];
            ledgerOutline[i] = cell.gameObject.AddComponent<Outline>();
            ledgerOutline[i].effectColor = GuiliData.Line;
            ledgerOutline[i].effectDistance = new Vector2(1, -1);
            string id = (i + 1).ToString("00");
            b.onClick.AddListener(() => Game.LedgerClick(id));
            ledgerId[i] = TextAt(cell, "号码", 0, 6, 67, 28, id, 20, GuiliData.Green, TextAnchor.MiddleCenter, true);
            ledgerSub[i] = TextAt(cell, "细目", 0, 34, 67, 18, "未观", 9, GuiliData.Muted, TextAnchor.MiddleCenter, false);
        }

        tutorialStepsGo = At(j, "教学步骤", 0, 210, 215, 230).gameObject;
        for (int i = 0; i < 4; i++)
        {
            RectTransform row = At(tutorialStepsGo.transform, "步骤" + i, 0, i * 54, 215, 46);
            RectTransform ib = At(row, "图标", 0, 8, 30, 30);
            tutorialStepIcon[i] = GuiliUIKit.Icon("步骤图标", ib, i, GuiliData.Muted);
            tutorialStepText[i] = TextAt(row, "步骤名", 40, 6, 175, 34,
                                         GuiliData.StepTitles[i], 16, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        }
        GuiliUIKit.Box("脚注线", At(j, "脚线", 0, 452, 215, 1), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(215, 1), GuiliData.Line);
        footText = TextAt(j, "手记脚注", 0, 462, 215, 58, GuiliData.JournalFoot, 12,
                          GuiliData.Hex("99917a"), TextAnchor.UpperLeft, true);

        /* 右侧工具栏 #tools right:3% top:30 */
        toolsGo = At(p, "观察工具", 1314, 30, 83, 520).gameObject;
        for (int i = 0; i < 6; i++)
        {
            string key = toolKeys[i];
            string label = i == 5 ? "返回九瓜" : GuiliData.KeyNames[i];
            RectTransform cell = At(toolsGo.transform, "工具" + key, 0, i * 85, 83, 76);
            Button b = cell.gameObject.AddComponent<Button>();
            Image bg = cell.gameObject.GetComponent<Image>();
            if (bg == null) bg = cell.gameObject.AddComponent<Image>();
            bg.color = GuiliData.Cream;
            b.targetGraphic = bg;
            GuiliUIKit.Border(bg, GuiliData.Line, 1f);
            b.onClick.AddListener(() => Game.Tool(key));
            toolButtons[i] = b;
            RectTransform ib = At(cell, "工具图标", 24, 10, 34, 34);
            toolIcons[i] = GuiliUIKit.Icon("图标", ib, i == 5 ? 5 : i, GuiliData.Green);
            toolLabels[i] = TextAt(cell, "名称", 0, 48, 83, 22, label, 12, GuiliData.Green, TextAnchor.MiddleCenter, true);
            toolChecks[i] = TextAt(cell, "已完成", 60, 2, 20, 16, "", 11, GuiliData.Red, TextAnchor.MiddleRight, false);
        }

        /* .view-controls top:12 left:50% */
        viewControlsGo = GuiliUIKit.Fixed("视角工具条", p, new Vector2(.5f, 1),
                                          new Vector2(0, -28), new Vector2(410, 34)).gameObject;
        Image vcBg = GuiliUIKit.Img("工具条底", viewControlsGo.transform,
                                    new Color(244 / 255f, 239 / 255f, 220 / 255f, .86f));
        GuiliUIKit.Border(vcBg, GuiliData.Line, 1f);
        viewOrbit = GuiliUIKit.Quiet("拖动看四周", viewControlsGo.transform, new Vector2(.5f, .5f),
                                     new Vector2(-135, 0), new Vector2(120, 28), GuiliData.ViewOrbitOff, () => Game.ViewTool(0), 12);
        viewReset = GuiliUIKit.Quiet("回到原视角", viewControlsGo.transform, new Vector2(.5f, .5f),
                                     new Vector2(0, 0), new Vector2(120, 28), GuiliData.ViewReset, () => Game.ViewTool(1), 12);
        viewFollow = GuiliUIKit.Quiet("随鼠标轻移", viewControlsGo.transform, new Vector2(.5f, .5f),
                                      new Vector2(135, 0), new Vector2(130, 28), GuiliData.ViewFollowOn, () => Game.ViewTool(2), 12);

        /* #view-caption left:40% bottom:145 */
        captionGo = GuiliUIKit.Fixed("视角提示", p, new Vector2(.4f, 0),
                                     new Vector2(0, 145), new Vector2(760, 26)).gameObject;
        captionText = GuiliUIKit.T("提示文字", captionGo.transform, GuiliData.CaptionDefault, 12,
                                   GuiliData.Hex("8b856c"), TextAnchor.MiddleCenter, true);

        /* .knowledge left:3% right:3% bottom:20 min-height:87 */
        knowledgeGo = GuiliUIKit.R("瓜农手记", p, new Vector2(0, 0), new Vector2(1, 0),
                                   new Vector2(43, 20), new Vector2(-43, 107)).gameObject;
        GuiliUIKit.Box("手记顶线", knowledgeGo.transform, new Vector2(.5f, 1), Vector2.zero,
                       new Vector2(3200, 1), GuiliData.Line);
        TextAt(knowledgeGo.transform, "手记标题", 24, 8, 34, 70, "瓜\n农\n手\n记", 14,
               GuiliData.Red, TextAnchor.MiddleCenter, true);
        knowledgeText = TextAt(knowledgeGo.transform, "手记正文", 82, 12, 900, 64, "", 12,
                               GuiliData.Hex("6e725e"), TextAnchor.MiddleLeft, false);
        nextButton = GuiliUIKit.Btn("下一步", At(knowledgeGo.transform, "钮", 1100, 18, 250, 52),
                                    new Vector2(.5f, .5f), Vector2.zero, new Vector2(250, 52),
                                    GuiliData.NextWithoutActive, () => Game.Next(), true, 16);
        nextLabel = nextButton.GetComponentInChildren<Text>();

        /* #sound-wave left:50% top:45% */
        waveGo = GuiliUIKit.Fixed("听瓜波形", p, new Vector2(.5f, 1),
                                  new Vector2(0, -365), new Vector2(240, 46)).gameObject;
        for (int i = 0; i < 8; i++)
            waveBars[i] = GuiliUIKit.Box("波" + i, waveGo.transform, new Vector2(0, .5f),
                                         new Vector2(18 + i * 14, 0), new Vector2(4, 12), GuiliData.Green);
        waveLabel = TextAt(waveGo.transform, "听瓜字", 140, 12, 96, 22, GuiliData.SoundWaveLabel, 11,
                           GuiliData.Green, TextAnchor.MiddleCenter, true);
        waveGo.SetActive(false);

        /* 「04 拍一拍」听音三辨卡：三档严格取自科普图（清脆/沉闷/浑浊），
           另加「略闷」作为过渡档，让四类切面结果各有唯一音色。
           轻叩时短暂浮现在视口上方，并高亮当前这颗瓜的那一档。 */
        knockGo = GuiliUIKit.Fixed("听音三辨", p, new Vector2(.5f, 1),
                                   new Vector2(0, -212), new Vector2(648, 126)).gameObject;
        Image knockBg = GuiliUIKit.Img("三辨纸底", knockGo.transform, new Color(.96f, .94f, .87f, .95f));
        GuiliUIKit.Border(knockBg, GuiliData.Line, 1f);
        TextAt(knockGo.transform, "三辨眉", 12, 4, 300, 20, "轻叩听音 · 四声可辨", 11,
               GuiliData.Muted, TextAnchor.MiddleLeft, true);
        TextAt(knockGo.transform, "三辨脚", 340, 4, 296, 20, "一声一瓜，各有脾性", 10,
               GuiliData.Muted, TextAnchor.MiddleRight, false);
        for (int i = 0; i < 4; i++)
        {
            RectTransform card = BoxAt(knockGo.transform, "声卡" + i, 8 + i * 158, 26, 150, 92, GuiliData.Cream);
            knockCardBg[i] = card.GetComponent<Image>();
            GuiliUIKit.Border(knockCardBg[i], GuiliData.Line, 1f);
            knockCardTitle[i] = TextAt(card, "声名" + i, 6, 6, 138, 24,
                                       GuiliData.KnockCards[i, 0], 14, GuiliData.Green,
                                       TextAnchor.MiddleCenter, true);
            TextAt(card, "声一" + i, 6, 32, 138, 20, GuiliData.KnockCards[i, 1], 11,
                   GuiliData.Muted, TextAnchor.MiddleCenter, false);
            knockCardBody[i] = TextAt(card, "声二" + i, 6, 54, 138, 32,
                                      GuiliData.KnockCards[i, 2], 10, GuiliData.Ink,
                                      TextAnchor.MiddleCenter, false);
        }
        knockGo.SetActive(false);

        /* 桌上编号牌（世界坐标投影） */
        labelsGo = GuiliUIKit.Stretch("编号牌层", p).gameObject;
        for (int i = 0; i < 9; i++)
        {
            RectTransform r = GuiliUIKit.Fixed("牌" + i, labelsGo.transform, new Vector2(.5f, .5f),
                                               Vector2.zero, new Vector2(52, 24));
            Image bg = r.gameObject.AddComponent<Image>();
            bg.color = GuiliData.Paper;
            bg.raycastTarget = false;
            GuiliUIKit.Border(bg, GuiliData.Hex("8a8b72"), 1f);
            labelRoots[i] = r;
            labelTexts[i] = GuiliUIKit.T("号码", r, (i + 1).ToString("00"), 14,
                                         GuiliData.Green, TextAnchor.MiddleCenter, true);
            r.gameObject.SetActive(false);
        }
    }

    /* ---------------- 卷三 · 独立挑瓜 ---------------- */
    void BuildChallenge(Transform root)
    {
        challengeGo = GuiliUIKit.R("独立挑瓜", root, Vector2.zero, Vector2.one, Vector2.zero,
                                   new Vector2(0, -88)).gameObject;
        GuiliUIKit.PaperPanel("挑战纸面", challengeGo.transform);
        RectTransform sheet = GuiliUIKit.Fixed("卷宗", challengeGo.transform, new Vector2(.5f, .5f),
                                               Vector2.zero, new Vector2(1000, 700));
        /* border-left/right: 6px double */
        GuiliUIKit.Box("左线一", sheet, new Vector2(0, .5f), new Vector2(-6, 0), new Vector2(1, 700), GuiliData.Line);
        GuiliUIKit.Box("左线二", sheet, new Vector2(0, .5f), new Vector2(-2, 0), new Vector2(1, 700), GuiliData.Line);
        GuiliUIKit.Box("右线一", sheet, new Vector2(1, .5f), new Vector2(6, 0), new Vector2(1, 700), GuiliData.Line);
        GuiliUIKit.Box("右线二", sheet, new Vector2(1, .5f), new Vector2(2, 0), new Vector2(1, 700), GuiliData.Line);

        TextAt(sheet, "眉", 38, 24, 300, 22, GuiliData.ChallengeChapter, 11, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "题", 38, 50, 500, 50, GuiliData.ChallengeTitle, 32, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "副", 38, 104, 900, 28, GuiliData.ChallengeSubtitle, 15,
               GuiliData.Hex("8a826d"), TextAnchor.MiddleLeft, true);

        TextAt(sheet, "录标", 38, 148, 380, 28, GuiliData.ChallengeCatalog, 16, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "录注", 418, 148, 90, 28, GuiliData.ChallengeCatalogNote, 10, GuiliData.Muted, TextAnchor.MiddleRight, false);
        GuiliUIKit.Box("图录线", At(sheet, "录线", 38, 178, 470, 1), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(470, 1), GuiliData.Line);
        TextAt(sheet, "据标", 553, 148, 320, 28, GuiliData.ChallengeReasons, 16, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "据注", 873, 148, 89, 28, GuiliData.ChallengeReasonsNote, 10, GuiliData.Muted, TextAnchor.MiddleRight, false);
        GuiliUIKit.Box("依据线", At(sheet, "据线", 553, 178, 409, 1), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(409, 1), GuiliData.Line);

        for (int i = 0; i < 9; i++)
        {
            float x = 38 + (i % 3) * 160f, y = 190 + (i / 3) * 107f;
            RectTransform cell = BoxAt(sheet, "瓜格" + i, x, y, 150, 98, GuiliData.Cream);
            Button b = cell.gameObject.AddComponent<Button>();
            b.targetGraphic = cell.GetComponent<Image>();
            Outline ol = cell.gameObject.AddComponent<Outline>();
            ol.effectColor = GuiliData.Line; ol.effectDistance = new Vector2(1, -1);
            choiceButtons[i] = b; choiceOutline[i] = ol;
            RectTransform thumb = At(cell, "缩略", 36, 6, 78, 64);
            choiceImages[i] = GuiliUIKit.Img("瓜影", thumb, Color.white);
            choiceId[i] = TextAt(cell, "编号", 0, 72, 150, 22, (i + 1).ToString("00"), 15,
                                 GuiliData.Green, TextAnchor.MiddleCenter, true);
            choiceSel[i] = TextAt(cell, "选字", 118, 2, 28, 26, "", 19,
                                  GuiliData.Red, TextAnchor.MiddleCenter, true);
            /* 图录每轮、每次进入挑战表都重新洗牌，编号不再固定对应格子；
               点击时读当前格子的编号，避免玩家靠记住「第几格」选瓜。 */
            int idx = i;
            b.onClick.AddListener(() => Game.Choose(choiceId[idx].text));
        }
        for (int i = 0; i < 6; i++)
        {
            float x = 553 + (i % 2) * 205f, y = 190 + (i / 2) * 98f;
            RectTransform cell = BoxAt(sheet, "依据格" + i, x, y, 200, 90, GuiliData.Cream);
            Button b = cell.gameObject.AddComponent<Button>();
            Image bg = cell.GetComponent<Image>();
            bg.raycastTarget = true;
            b.targetGraphic = bg;
            GuiliUIKit.Border(bg, GuiliData.Hex("c9c1a9"), 1f);
            reasonBg[i] = bg;
            RectTransform cb = BoxAt(cell, "勾框", 8, 8, 18, 18, Color.white);
            GuiliUIKit.Border(cb.GetComponent<Image>(), GuiliData.Line, 1f);
            reasonCheck[i] = GuiliUIKit.Img("勾", cb, GuiliData.Red);
            reasonCheck[i].rectTransform.offsetMin = new Vector2(3, 3);
            reasonCheck[i].rectTransform.offsetMax = new Vector2(-3, -3);
            RectTransform ib = At(cell, "图标位", 84, 12, 32, 32);
            GuiliUIKit.Icon("依据图标", ib, i == 5 ? 6 : i, GuiliData.Green);
            TextAt(cell, "依据名", 0, 52, 200, 26, GuiliData.ReasonNames[i], 13,
                   GuiliData.Green, TextAnchor.MiddleCenter, true);
            string key = GuiliData.ReasonKeys[i];
            b.onClick.AddListener(() => Game.ToggleReason(key));
            reasonButtons[i] = b;
        }
        challengeSummary = TextAt(sheet, "摘", 553, 490, 409, 30, GuiliData.ChallengeEmpty, 13,
                                  GuiliData.Red, TextAnchor.MiddleLeft, true);
        warnText = TextAt(sheet, "警", 38, 530, 900, 26, "", 12, GuiliData.Red, TextAnchor.MiddleLeft, false);
        GuiliUIKit.Btn("回到瓜摊", At(sheet, "回", 560, 600, 160, 48), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(160, 48), GuiliData.ChallengeBack, () => Game.ObserveAgain(), false, 15);
        GuiliUIKit.Btn("确认选择", At(sheet, "交", 736, 600, 226, 48), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(226, 48), GuiliData.ChallengeSubmit, () => Game.Submit(), true, 15);
    }

    /* ---------------- 卷四 · 切瓜求证（左侧 3D + 右侧 320px 卡） ---------------- */
    void BuildCut(Transform root)
    {
        cutGo = GuiliUIKit.R("切瓜求证", root, Vector2.zero, Vector2.one, Vector2.zero,
                             new Vector2(0, -88)).gameObject;
        /* 注意：这里不铺全屏纸底 —— 3D 剖面由纸底相机 + 主相机渲染，
           全屏不透明 Image 会把整个游戏画面遮掉（旧版正是此问题）。 */

        /* body[data-state=cut] #canvas-container{left:4%;right:390px} */
        CutViewport = GuiliUIKit.R("切瓜视口", cutGo.transform, Vector2.zero, Vector2.one,
                                   new Vector2(58, 132), new Vector2(-390, -10));

        /* #cut-panel{right:4%;top:140px;width:320px;background:#f4efdcf2} */
        RectTransform card = At(cutGo.transform, "切瓜文字卡", 1062, 52, 320, 470);
        cutCard = card.gameObject;
        Image cardBg = cutCard.AddComponent<Image>();
        cardBg.color = new Color(244 / 255f, 239 / 255f, 220 / 255f, .95f);
        cardBg.raycastTarget = true;
        GuiliUIKit.Box("卡片竖线", At(card, "竖线", 0, 0, 1, 470), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(1, 470), GuiliData.Line);
        cutGroup = cutCard.AddComponent<CanvasGroup>();
        cutCardHome = card.anchoredPosition;

        TextAt(card, "眉", 25, 22, 270, 20, GuiliData.CutChapter, 11, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        cutTitle = TextAt(card, "题", 25, 48, 270, 48, "", 32, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        cutStatus = TextAt(card, "态", 25, 102, 270, 26, "", 16, GuiliData.Red, TextAnchor.MiddleLeft, true);
        cutText = TextAt(card, "文", 25, 138, 270, 112, "", 13, GuiliData.Ink, TextAnchor.UpperLeft, false);
        cutEvidence = TextAt(card, "据", 25, 252, 270, 60, "", 11,
                             GuiliData.Hex("83816b"), TextAnchor.UpperLeft, false);
        GuiliUIKit.Btn("收下瓜笺", At(card, "钮", 25, 320, 270, 48), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(270, 48), GuiliData.CutButton, () => Game.ResultButton(), true, 15);
        cutCard.SetActive(false);

        /* 刀光：斜向划过左侧视口，让「切」这个动作可见 */
        knifeGo = At(cutGo.transform, "刀光", 300, 300, 260, 4).gameObject;
        Image knifeImg = knifeGo.AddComponent<Image>();
        knifeImg.color = new Color(1, 1, 1, .85f);
        knifeImg.raycastTarget = false;
        knifeRt = knifeGo.GetComponent<RectTransform>();
        knifeHome = knifeRt.anchoredPosition;
        knifeRt.localRotation = Quaternion.Euler(0, 0, -35f);
        knifeGroup = knifeGo.AddComponent<CanvasGroup>();
        knifeGo.SetActive(false);

        /* #view-caption 底部提示（cut 状态仍显示） */
        RectTransform cap = GuiliUIKit.Fixed("切瓜提示", cutGo.transform, new Vector2(.4f, 0),
                                             new Vector2(0, 145), new Vector2(700, 26));
        cutCaption = GuiliUIKit.T("提示字", cap, GuiliData.CutCaptionBefore, 12,
                                  GuiliData.Hex("8b856c"), TextAnchor.MiddleCenter, true);
    }

    /* ---------------- 卷五 · 瓜乡结业笺 ---------------- */
    void BuildResult(Transform root)
    {
        resultGo = GuiliUIKit.R("瓜笺", root, Vector2.zero, Vector2.one, Vector2.zero,
                                new Vector2(0, -88)).gameObject;
        GuiliUIKit.PaperPanel("成绩纸面", resultGo.transform);
        RectTransform sheet = GuiliUIKit.Fixed("结业笺", resultGo.transform, new Vector2(.5f, .5f),
                                               Vector2.zero, new Vector2(760, 700));
        GuiliUIKit.Box("左线一", sheet, new Vector2(0, .5f), new Vector2(-6, 0), new Vector2(1, 700), GuiliData.Line);
        GuiliUIKit.Box("左线二", sheet, new Vector2(0, .5f), new Vector2(-2, 0), new Vector2(1, 700), GuiliData.Line);
        GuiliUIKit.Box("右线一", sheet, new Vector2(1, .5f), new Vector2(6, 0), new Vector2(1, 700), GuiliData.Line);
        GuiliUIKit.Box("右线二", sheet, new Vector2(1, .5f), new Vector2(2, 0), new Vector2(1, 700), GuiliData.Line);

        TextAt(sheet, "眉", 38, 24, 400, 22, GuiliData.ResultChapter, 11, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "题", 38, 52, 700, 48, GuiliData.ResultTitle, 30, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        scoreText = TextAt(sheet, "分", 38, 122, 180, 100, "0", 82, GuiliData.Green, TextAnchor.MiddleLeft, true);
        TextAt(sheet, "满", 208, 176, 80, 26, "/ 100", 15, GuiliData.Muted, TextAnchor.MiddleLeft, false);
        RectTransform seal = At(sheet, "印", 300, 132, 120, 46);
        Image sealBg = GuiliUIKit.Img("印框", seal, new Color(0, 0, 0, 0));
        GuiliUIKit.Border(sealBg, GuiliData.Red, 1f);
        rankText = GuiliUIKit.T("称号", seal, "", 19, GuiliData.Red, TextAnchor.MiddleCenter, true);
        resultMessageText = TextAt(sheet, "消", 300, 188, 420, 50, "", 12,
                                   GuiliData.Hex("7b7c66"), TextAnchor.UpperLeft, false);
        for (int i = 0; i < 3; i++)
        {
            float y = 254 + i * 42;
            TextAt(sheet, "名" + i, 38, y, 100, 26, GuiliData.ScoreNames[i], 11,
                   GuiliData.Muted, TextAnchor.MiddleLeft, false);
            RectTransform bar = BoxAt(sheet, "条" + i, 160, y + 8, 560, 8, new Color(.85f, .84f, .76f, 1));
            RectTransform fr = GuiliUIKit.Fixed("填", bar, new Vector2(0, .5f), Vector2.zero, new Vector2(0, 8));
            fr.pivot = new Vector2(0, .5f);
            scoreFills[i] = fr.gameObject.AddComponent<Image>();
            scoreFills[i].color = GuiliData.Green;
            scoreValues[i] = TextAt(sheet, "值" + i, 730, y, 70, 26, "0/0", 12,
                                    GuiliData.Ink, TextAnchor.MiddleRight, false);
        }
        // 本局西瓜币奖励三行（原有分项分数 / 称号 / 结业评语 / 按钮全部保留）
        rewardText = TextAt(sheet, "奖", 38, 430, 684, 76, "", 13,
                            GuiliData.Green, TextAnchor.UpperLeft, true);
        GuiliUIKit.Box("结业线", At(sheet, "线", 38, 512, 684, 1), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(684, 1), GuiliData.Line);
        RectTransform icon = At(sheet, "茎", 38, 530, 40, 40);
        GuiliUIKit.Icon("瓜柄图标", icon, 2, GuiliData.Green);
        TextAt(sheet, "语", 100, 528, 620, 56,
               GuiliData.RecapA + "\n<color=#" + ColorUtility.ToHtmlStringRGB(GuiliData.Red) + ">" + GuiliData.RecapB + "</color>",
               13, GuiliData.Hex("8a826d"), TextAnchor.MiddleLeft, true);
        GuiliUIKit.Btn("合卷归首页", At(sheet, "归", 380, 610, 170, 50), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(170, 50), GuiliData.ResultHome, () => Game.GoHome(), false, 15);
        GuiliUIKit.Btn("换一篮", At(sheet, "换", 566, 610, 176, 50), new Vector2(.5f, .5f), Vector2.zero,
                       new Vector2(176, 50), GuiliData.ResultRetry, () => Game.Retry(), true, 15);
    }

    /* ---------------- 弹窗 dialog : width 580 padding 35 ---------------- */
    void BuildModal(Transform root)
    {
        modalGo = GuiliUIKit.Stretch("瓜乡手记弹窗", root).gameObject;
        // 遮罩负责拦截弹窗期间的所有指针事件，避免点击穿透到下层 UI 或 3D 瓜摊
        Image mask = GuiliUIKit.Img("遮罩", modalGo.transform, new Color(.204f, .243f, .173f, .4f));
        mask.raycastTarget = true;
        RectTransform card = GuiliUIKit.Fixed("手记卡", modalGo.transform, new Vector2(.5f, .5f),
                                              Vector2.zero, new Vector2(580, 480));
        Image bg = GuiliUIKit.Img("卡底", card, GuiliData.Paper);
        bg.raycastTarget = true;
        GuiliUIKit.Border(bg, GuiliData.Line, 1f);
        modalCloseButton = GuiliUIKit.Quiet("关闭", At(card, "闭", 520, 6, 40, 36), new Vector2(.5f, .5f),
                                            Vector2.zero, new Vector2(40, 36), "×", () => CloseModal(), 26);
        TextAt(card, "眉", 35, 24, 400, 22, GuiliData.ModalEyebrow, 11, GuiliData.Muted, TextAnchor.MiddleLeft, true);
        modalTitle = TextAt(card, "题", 35, 52, 510, 44, "", 30, GuiliData.Ink, TextAnchor.MiddleLeft, true);
        modalBody = TextAt(card, "文", 35, 106, 510, 250, "", 15, GuiliData.Ink, TextAnchor.UpperLeft, true);
        modalExtra = At(card, "附件", 35, 106, 510, 250);
        // 按钮在卡底之后创建，深度高于卡底，保持为卡片内最高的命中目标
        modalDoneButton = GuiliUIKit.Btn("记住了", At(card, "钮", 35, 390, 180, 50), new Vector2(.5f, .5f),
                                        Vector2.zero, new Vector2(180, 50), GuiliData.ModalDone,
                                        HandleModalDoneClicked, true, 16);
        modalGo.SetActive(false);
    }

    void BuildToast(Transform root)
    {
        toastGo = GuiliUIKit.Fixed("提示条", root, new Vector2(.5f, 0),
                                   new Vector2(0, 40), new Vector2(520, 46)).gameObject;
        GuiliUIKit.Img("底", toastGo.transform, GuiliData.Green);
        toastText = GuiliUIKit.T("字", toastGo.transform, "", 13, GuiliData.Paper, TextAnchor.MiddleCenter, false);
        toastGo.SetActive(false);
    }

    /* ---------------- 面板切换 ---------------- */
    public void HideAll()
    {
        GuiliUIKit.SetActive(homeGo, false);
        GuiliUIKit.SetActive(playGo, false);
        GuiliUIKit.SetActive(challengeGo, false);
        GuiliUIKit.SetActive(cutGo, false);
        GuiliUIKit.SetActive(resultGo, false);
    }
    public void ShowHome() { HideAll(); GuiliUIKit.SetActive(homeGo, true); SetStage(-1); }
    public void ShowPlay() { HideAll(); GuiliUIKit.SetActive(playGo, true); }
    public void ShowChallenge() { HideAll(); GuiliUIKit.SetActive(challengeGo, true); }
    public void ShowCutStage() { HideAll(); GuiliUIKit.SetActive(cutGo, true); }
    public void ShowResult() { HideAll(); GuiliUIKit.SetActive(resultGo, true); }

    public void SetStage(int phase)
    {
        for (int i = 0; i < 5; i++)
        {
            Color col = i == phase ? GuiliData.Red : i < phase ? GuiliData.Green : GuiliData.Hex("999783");
            Color boxCol = i == phase ? GuiliData.Red : GuiliData.Line;
            stageNum[i].color = col;
            stageName[i].color = col;
            stageBox[i].color = new Color(0, 0, 0, 0);
            Outline o = stageBox[i].GetComponent<Outline>();
            if (o != null) o.effectColor = boxCol;
        }
    }

    /* ---------------- 侧栏 / 工具刷新 ---------------- */
    public void SetJournal(string chapter, string task, string hint)
    {
        chapterText.text = chapter;
        taskText.text = task;
        hintText.text = hint;
    }
    public void SetTutorialMode(bool on, int step)
    {
        GuiliUIKit.SetActive(tutorialStepsGo, on);
        GuiliUIKit.SetActive(ledgerGridGo, !on);
        if (!on) return;
        for (int i = 0; i < 4; i++)
        {
            Color c = i < step ? GuiliData.Red : GuiliData.Hex("a19b83");
            tutorialStepText[i].color = c;
            tutorialStepIcon[i].color = c;
        }
    }
    public void SetLedger(List<MelonInfo> infos, string activeId, int round)
    {
        int completed = 0;
        for (int i = 0; i < infos.Count; i++) if (infos[i].Complete()) completed++;
        ledgerProgress.text = GuiliData.LedgerText(completed, round);
        for (int i = 0; i < 9; i++)
        {
            MelonInfo info = i < infos.Count ? infos[i] : null;
            if (info == null) continue;
            ledgerId[i].text = info.id;
            ledgerSub[i].text = info.ObservedCount() == 0 ? "未观" :
                               info.ObservedCount() + " 项" + (info.Complete() ? " ✓" : "");
            ledgerBg[i].color = info.Complete() ? GuiliData.Hex("dde2ca") : GuiliData.Cream;
            ledgerOutline[i].effectColor = info.id == activeId ? GuiliData.Red : GuiliData.Line;
            ledgerId[i].color = info.id == activeId ? GuiliData.Red : GuiliData.Green;
        }
    }
    public void SetTools(string[] allowed, MelonInfo info, bool showBack)
    {
        for (int i = 0; i < 6; i++)
        {
            bool on = false;
            string key = toolKeys[i];
            if (i == 5) on = showBack;
            else if (allowed != null)
                for (int k = 0; k < allowed.Length; k++) if (allowed[k] == key) on = true;
            GuiliUIKit.SetActive(toolButtons[i].gameObject, on);
            if (on && info != null && i < 5) toolChecks[i].text = info.observed[i] ? "✓" : "";
            else toolChecks[i].text = "";
        }
    }
    public void SetKnowledge(string text) { knowledgeText.text = text; }
    public void SetCaption(string text) { captionText.text = text; }
    public void SetNext(string label, bool enabled)
    {
        nextLabel.text = label;
        nextButton.interactable = enabled;
        GuiliUIKit.SetActive(nextButton.gameObject, !string.IsNullOrEmpty(label));
    }
    public void SetViewControls(bool orbit, bool follow)
    {
        viewOrbit.GetComponentInChildren<Text>().text = orbit ? GuiliData.ViewOrbitOn : GuiliData.ViewOrbitOff;
        viewFollow.GetComponentInChildren<Text>().text = follow ? GuiliData.ViewFollowOn : GuiliData.ViewFollowOff;
    }
    public void SetViewControlsVisible(bool on) { GuiliUIKit.SetActive(viewControlsGo, on); GuiliUIKit.SetActive(captionGo, on); }
    public void SetPlayPanelsVisible(bool on)
    {
        GuiliUIKit.SetActive(journalGo, on);
        GuiliUIKit.SetActive(toolsGo, on);
        GuiliUIKit.SetActive(knowledgeGo, on);
    }

    /* ---------------- 挑战表 ---------------- */
    public void SetChallenge(List<MelonInfo> infos, List<Texture2D> thumbs, string chosen, List<string> checkedReasons)
    {
        chosenId = chosen;
        reasons.Clear();
        if (checkedReasons != null) reasons.AddRange(checkedReasons);
        for (int i = 0; i < 9; i++)
        {
            MelonInfo info = i < infos.Count ? infos[i] : null;
            if (info == null) continue;
            Texture2D th = i < thumbs.Count ? thumbs[i] : null;
            choiceImages[i].sprite = th == null ? null :
                Sprite.Create(th, new Rect(0, 0, th.width, th.height), new Vector2(.5f, .5f), 100f);
            choiceId[i].text = info.id;
            bool sel = chosen == info.id;
            choiceOutline[i].effectColor = sel ? GuiliData.Red : GuiliData.Line;
            choiceSel[i].text = sel ? "选" : "";
            choiceButtons[i].GetComponent<Image>().color = sel ? GuiliData.Hex("e6dfc8") : GuiliData.Cream;
        }
        for (int i = 0; i < 6; i++)
        {
            bool on = reasons.Contains(GuiliData.ReasonKeys[i]);
            GuiliUIKit.SetActive(reasonCheck[i].gameObject, on);
            reasonBg[i].color = on ? GuiliData.Hex("dfe4cf") : GuiliData.Cream;
        }
        challengeSummary.text = GuiliData.SelectionSummary(chosen);
        warnText.text = "";
    }
    public void SetChoiceSelected(string id)
    {
        chosenId = id;
        for (int i = 0; i < 9; i++)
        {
            bool sel = choiceId[i].text == id;
            choiceOutline[i].effectColor = sel ? GuiliData.Red : GuiliData.Line;
            choiceSel[i].text = sel ? "选" : "";
            choiceButtons[i].GetComponent<Image>().color = sel ? GuiliData.Hex("e6dfc8") : GuiliData.Cream;
        }
        challengeSummary.text = GuiliData.SelectionSummaryHint(id);
        warnText.text = "";
    }
    public void SetWarn(string text) { warnText.text = text; }
    public void ToggleReasonUI(string key)
    {
        if (reasons.Contains(key)) reasons.Remove(key); else reasons.Add(key);
        for (int i = 0; i < 6; i++)
        {
            bool on = reasons.Contains(GuiliData.ReasonKeys[i]);
            GuiliUIKit.SetActive(reasonCheck[i].gameObject, on);
            reasonBg[i].color = on ? GuiliData.Hex("dfe4cf") : GuiliData.Cream;
        }
        warnText.text = "";
    }
    public List<string> GetReasons() { return new List<string>(reasons); }
    public string Chosen() { return chosenId; }

    /* ---------------- 切瓜 / 成绩 ---------------- */
    public void SetCutCard(bool on)
    {
        GuiliUIKit.SetActive(cutCard, on);
        if (!on) return;
        /* 网页 5 版的切瓜卡在剖切动画结束后直接出现。这里不做透明度淡入或
           位移补间，保证任何帧率 / 时间缩放下都不会再出现「页面没出现」。 */
        cutGroup.alpha = 1f;
        cutCard.GetComponent<RectTransform>().anchoredPosition = cutCardHome;
    }

    /* 刀光扫过：从左上斜划到右下，配合「咔」一声切开 */
    public void PlayKnife()
    {
        if (knifeRoutine != null) StopCoroutine(knifeRoutine);
        knifeRoutine = StartCoroutine(KnifeAnim());
    }
    IEnumerator KnifeAnim()
    {
        GuiliUIKit.SetActive(knifeGo, true);
        Vector2 from = knifeHome - new Vector2(180, -115);
        float t = 0;
        while (t < .72f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / .72f);
            knifeRt.anchoredPosition = from + new Vector2(360 * k, -230 * k);
            knifeGroup.alpha = Mathf.Sin(k * Mathf.PI) * .85f;
            yield return null;
        }
        GuiliUIKit.SetActive(knifeGo, false);
        knifeRoutine = null;
    }

    public void SetCutCaption(string text) { if (cutCaption != null) cutCaption.text = text; }
    public void SetCut(string title, string status, string body, string evidence)
    {
        cutTitle.text = title;
        cutStatus.text = status;
        cutText.text = body;
        cutEvidence.text = evidence;
    }
    public void SetResult(int total, string rank, int[] parts, string message, int reward, int balance)
    {
        scoreText.text = total.ToString();
        rankText.text = rank;
        resultMessageText.text = message;
        for (int i = 0; i < 3; i++)
        {
            float ratio = GuiliData.ScoreMax[i] == 0 ? 0 : (float)parts[i] / GuiliData.ScoreMax[i];
            scoreFills[i].rectTransform.sizeDelta = new Vector2(
                GuiliUIKit.LegacyWidth(560 * ratio), GuiliUIKit.LegacyHeight(8));
            scoreValues[i].text = parts[i] + "/" + GuiliData.ScoreMax[i];
        }
        if (rewardText != null)
            rewardText.text = "本局得分 " + total + " / 100\n本局获得 +" + reward +
                              " 西瓜币\n当前余额 " + balance;
    }

    /* ---------------- 弹窗 ---------------- */
    public void ShowModal(string title, string body, Action<Transform> extra, Action onDone)
    {
        modalTitle.text = title;
        modalBody.text = body;
        modalDone = onDone;
        for (int i = modalExtra.childCount - 1; i >= 0; i--) Destroy(modalExtra.GetChild(i).gameObject);
        modalBody.gameObject.SetActive(string.IsNullOrEmpty(body) == false);
        if (extra != null) extra(modalExtra);
        modalGo.SetActive(true);
        // 弹窗置顶，避免运行时动态生成的 UI 覆盖到弹窗之上
        modalGo.transform.SetAsLastSibling();
        if (modalDoneButton != null)
        {
            modalDoneButton.gameObject.SetActive(true);
            modalDoneButton.enabled = true;
            modalDoneButton.interactable = true;
        }
    }

    /* 「记住了」按钮统一入口：弹窗已关闭时不再重复执行关闭回调 */
    void HandleModalDoneClicked()
    {
        if (!ModalOpen) return;
        CloseModal();
    }

    public void CloseModal(bool force = false)
    {
        if (!ModalOpen) return;
        modalGo.SetActive(false);
        Action cb = modalDone;
        modalDone = null;
        if (!force && cb != null) cb();
    }

    public void ShowArchive(MelonInfo info, Action onDone)
    {
        ShowModal(info.label + GuiliData.ArchiveTitleTail, "", extra =>
        {
            RectTransform art = At(extra, "工笔花", 360, 0, 130, 150);
            GuiliUIKit.Pic("花", art, "flower", new Color(1, 1, 1, .92f));
            string[,] rows = {
                { "品种", info.record.variety }, { "地块", info.record.field },
                { "定植", info.record.sowing }, { "采收", info.record.harvest }, { "糖度", info.record.sugar }
            };
            for (int i = 0; i < 5; i++)
            {
                float y = 4 + i * 38;
                TextAt(extra, "键" + i, 0, y, 80, 30, rows[i, 0], 13,
                       GuiliData.Hex("8e8269"), TextAnchor.MiddleLeft, false);
                TextAt(extra, "值" + i, 90, y, 250, 30, rows[i, 1], 13,
                       GuiliData.Ink, TextAnchor.MiddleRight, false);
                GuiliUIKit.Box("行线" + i, At(extra, "线", 0, y + 32, 340, 1), new Vector2(.5f, .5f), Vector2.zero,
                               new Vector2(340, 1), GuiliData.Hex("d3cab3"));
            }
            TextAt(extra, "注", 0, 196, 500, 30, info.record.note, 13, GuiliData.Ink, TextAnchor.MiddleLeft, false);
            TextAt(extra, "脚", 0, 226, 500, 24, GuiliData.RecordFoot, 11, GuiliData.Muted, TextAnchor.MiddleLeft, false);
        }, onDone);
    }

    public void ShowAbout(Action onDone)
    {
        ShowModal(GuiliData.AboutTitle, "", extra =>
        {
            TextAt(extra, "瓜乡正文", 0, 0, 340, 240, GuiliData.AboutBody, 14,
                   GuiliData.Ink, TextAnchor.UpperLeft, true);
            RectTransform art = At(extra, "瓜田", 360, 0, 150, 240);
            GuiliUIKit.Pic("工笔瓜田", art, "panggezhuang-field-gongbi", new Color(1, 1, 1, .94f));
        }, onDone);
    }

    public void ShowSettings(bool follow, Action onDone)
    {
        ShowModal(GuiliData.SettingsTitle, "", extra =>
        {
            TextAt(extra, "操作说明", 0, 0, 508, 132, GuiliData.SettingsControls, 14,
                   GuiliData.Ink, TextAnchor.UpperLeft, true);
            TextAt(extra, "量", 0, 140, 70, 26, "音量", 14, GuiliData.Green, TextAnchor.MiddleLeft, true);
            GuiliUIKit.VolumeSlider("音量杆", At(extra, "杆", 80, 140, 300, 26), new Vector2(.5f, .5f), Vector2.zero,
                                    new Vector2(300, 26), GuiliAudio.Volume(), v => GuiliAudio.SetVolume(v));
            TextAt(extra, "说", 0, 184, 508, 60, GuiliData.SettingsAudio, 12,
                   GuiliData.Muted, TextAnchor.UpperLeft, false);
        }, onDone);
    }

    /* ---------------- 波形 / 听音三辨卡 / toast / 编号牌 ---------------- */
    public void Wave(string knockKey)
    {
        int idx = GuiliData.KnockIndex(knockKey);
        waveLabel.text = GuiliData.KnockTitle(knockKey);
        for (int i = 0; i < 4; i++)
        {
            bool on = i == idx;
            knockCardBg[i].color = on ? GuiliData.Hex("e8dfbf") : GuiliData.Cream;
            knockCardTitle[i].color = on ? GuiliData.Red : GuiliData.Green;
            knockCardBody[i].color = on ? GuiliData.Red : GuiliData.Ink;
        }
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        waveRoutine = StartCoroutine(WaveAnim(knockKey));
    }
    IEnumerator WaveAnim(string knockKey)
    {
        /* 波形也随音色变化：清脆高而密、略闷中等、沉闷低而短、浑浊不规则 */
        float amp = 34f, speed = 17f, jitter = 0f;
        if (knockKey == "mellow") { amp = 24f; speed = 12f; }
        else if (knockKey == "dull") { amp = 10f; speed = 5f; }
        else if (knockKey == "muddy") { amp = 18f; speed = 9f; jitter = .45f; }

        GuiliUIKit.SetActive(waveGo, true);
        GuiliUIKit.SetActive(knockGo, true);
        float t = 0;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            float decay = 1f - Mathf.Clamp01(t / 1.5f) * .55f;
            for (int i = 0; i < 8; i++)
            {
                float wave = Mathf.Abs(Mathf.Sin(t * speed + i * .8f));
                if (jitter > 0f) wave = Mathf.Lerp(wave, Mathf.Abs(Mathf.Sin(t * 4.3f + i * 2.1f)), jitter);
                float h = 6f + wave * amp * decay;
                waveBars[i].rectTransform.sizeDelta = new Vector2(4, h);
                waveBars[i].color = knockKey == "dull" || knockKey == "muddy"
                    ? GuiliData.Muted : GuiliData.Green;
            }
            yield return null;
        }
        GuiliUIKit.SetActive(waveGo, false);
        GuiliUIKit.SetActive(knockGo, false);
        waveRoutine = null;
    }

    Coroutine toastRoutine;
    public void Toast(string text)
    {
        toastText.text = text;
        GuiliUIKit.SetActive(toastGo, true);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToast());
    }
    System.Collections.IEnumerator HideToast()
    {
        yield return new WaitForSeconds(2.8f);
        GuiliUIKit.SetActive(toastGo, false);
        toastRoutine = null;
    }

    public void UpdateLabels(List<GuiliMelon> melons, bool show)
    {
        if (Scene == null || Scene.MainCamera == null) show = false;
        for (int i = 0; i < 9; i++)
        {
            if (labelRoots[i] == null || labelTexts[i] == null) continue;
            bool on = show && i < melons.Count && melons[i].group != null && melons[i].group.activeInHierarchy;
            if (on)
            {
                Vector3 sp = Scene.MainCamera.WorldToScreenPoint(melons[i].LabelAnchor());
                on = sp.z > 0;
                if (on)
                {
                    labelRoots[i].position = new Vector3(sp.x, sp.y, 0);
                    labelTexts[i].text = melons[i].info.id;
                }
            }
            GuiliUIKit.SetActive(labelRoots[i].gameObject, on);
        }
    }
}
