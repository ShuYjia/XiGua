using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the phase-two UI prototype in the active scene. It is safe to run repeatedly.
/// </summary>
public static class WatermelonProjectSetup
{
    private static readonly Color Cream = new Color32(255, 248, 232, 255);
    private static readonly Color DeepGreen = new Color32(24, 91, 62, 255);
    private static readonly Color WatermelonGreen = new Color32(100, 183, 91, 255);
    private static readonly Color PaleGreen = new Color32(226, 245, 208, 255);
    private static readonly Color FleshRed = new Color32(239, 107, 94, 255);
    private static readonly Color Ink = new Color32(37, 54, 45, 255);
    private static readonly Color White = new Color32(255, 255, 255, 255);

    [MenuItem("Watermelon Science/Create Phase 2 Prototype")]
    public static void CreatePhaseTwoPrototype()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        GameObject existing = GameObject.Find("WatermelonClassroom");
        if (existing != null) Object.DestroyImmediate(existing);

        EnsureEventSystem();
        GameObject root = new GameObject("WatermelonClassroom");
        Undo.RegisterCreatedObjectUndo(root, "Create Watermelon Classroom");

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.localPosition = Vector3.zero;
        canvas.pixelPerfect = false;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        GameObject background = CreateImage("Background", root.transform, Cream, Stretch());
        background.transform.SetAsFirstSibling();

        GameObject home = CreatePanel("HomePanel", root.transform, Cream, Stretch());
        CreateText("Eyebrow", home.transform, "NATURE LAB  ·  2D SCIENCE CLASS", 20, DeepGreen, new Vector2(0, 385), new Vector2(1200, 45), TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText("Title", home.transform, "西瓜小课堂", 92, DeepGreen, new Vector2(0, 285), new Vector2(1200, 125), TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText("Subtitle", home.transform, "探索一颗西瓜的科学奥秘", 30, Ink, new Vector2(0, 205), new Vector2(1100, 55), TextAnchor.MiddleCenter, FontStyle.Normal);

        GameObject melon = CreateImage("DecorativeWatermelon", home.transform, FleshRed, new RectSpec(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-210, -10), new Vector2(180, 180)));
        AddOutline(melon, DeepGreen, 5f);
        GameObject rind = CreateImage("Rind", melon.transform, WatermelonGreen, new RectSpec(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-90, -95), new Vector2(180, 35)));
        AddOutline(rind, DeepGreen, 3f);
        CreateSeed(melon.transform, new Vector2(-38, 22), -20f);
        CreateSeed(melon.transform, new Vector2(0, 45), 5f);
        CreateSeed(melon.transform, new Vector2(36, 18), 24f);
        CreateText("DecorativeLabel", home.transform, "一起来发现\n甜蜜里的科学", 22, White, new Vector2(270, -12), new Vector2(230, 100), TextAnchor.MiddleCenter, FontStyle.Bold);

        CreateText("ChooseHint", home.transform, "选择一个主题，开始探索", 24, DeepGreen, new Vector2(0, 100), new Vector2(800, 45), TextAnchor.MiddleCenter, FontStyle.Bold);

        string[] labels = { "01  认识西瓜", "02  生长过程", "03  营养知识", "04  挑选西瓜" };
        string[] descriptions = { "从外形到内部结构", "看见一颗瓜的成长", "水分、糖类与能量", "用观察依据做选择" };
        Color[] accents = { PaleGreen, new Color32(229, 239, 198, 255), new Color32(255, 231, 180, 255), new Color32(255, 213, 196, 255) };
        Button[] moduleButtons = new Button[4];
        for (int i = 0; i < labels.Length; i++)
        {
            float x = -570 + i * 380;
            GameObject card = CreateButton("ModuleCard_" + (i + 1), home.transform, labels[i], accents[i], new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -105), new Vector2(330, 190)), 25, DeepGreen);
            CreateText("Description", card.transform, descriptions[i], 18, Ink, new Vector2(0, -47), new Vector2(290, 55), TextAnchor.MiddleCenter, FontStyle.Normal);
            moduleButtons[i] = card.GetComponent<Button>();
        }
        Button exitButton = CreateButton("ExitButton", home.transform, "退出程序", DeepGreen, new RectSpec(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(210, 58)), 20, White).GetComponent<Button>();
        GameObject homeFooter = CreateText("Footer", home.transform, "Windows PC  ·  单场景交互原型", 17, new Color32(92, 111, 96, 255), new Vector2(0, 25), new Vector2(600, 35), TextAnchor.MiddleCenter, FontStyle.Normal);
        AnchorBottomCenter(homeFooter, 25f);

        GameObject[] modules = new GameObject[4];
        Button[] backs = new Button[4];
        for (int i = 0; i < modules.Length; i++)
        {
            modules[i] = CreatePanel("ModulePanel_" + (i + 1), root.transform, Cream, Stretch());
            modules[i].SetActive(false);
            CreateText("ModuleIndex", modules[i].transform, labels[i].Substring(0, 2), 24, FleshRed, new Vector2(-690, 420), new Vector2(120, 45), TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateText("ModuleTitle", modules[i].transform, labels[i].Substring(4), 62, DeepGreen, new Vector2(0, 330), new Vector2(1300, 90), TextAnchor.MiddleCenter, FontStyle.Bold);
            if (i == 0)
            {
                CreateAnatomyModule(modules[i], out WatermelonAnatomyController anatomy);
            }
            else if (i == 1)
            {
                CreateGrowthModule(modules[i], out WatermelonGrowthController growth);
            }
            else if (i == 2)
            {
                CreateNutritionModule(modules[i], out WatermelonNutritionController nutrition);
            }
            else
            {
                CreateText("PrototypeBody", modules[i].transform, "这里将放置该主题的互动科普内容。\n阶段三已完成认识西瓜模块，后续阶段替换为真实图文、数据卡片和小游戏。", 28, Ink, new Vector2(0, 50), new Vector2(1100, 180), TextAnchor.MiddleCenter, FontStyle.Normal);
                GameObject contentCard = CreateImage("ContentPlaceholder", modules[i].transform, White, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(820, 180)));
                AddOutline(contentCard, accents[i], 4f);
                CreateText("ContentHint", contentCard.transform, i == 1 ? "四阶段生长时间线" : i == 2 ? "营养数字卡片与图表" : "三种教学样本与观察依据", 23, DeepGreen, Vector2.zero, new Vector2(760, 90), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            backs[i] = CreateButton("BackButton", modules[i].transform, "‹  返回首页", DeepGreen, new RectSpec(new Vector2(0, 0), new Vector2(0, 0), new Vector2(155, 75), new Vector2(240, 62)), 20, White).GetComponent<Button>();
            GameObject moduleFooter = CreateText("ModuleFooter", modules[i].transform, "Watermelon Science  ·  教学原型", 16, new Color32(92, 111, 96, 255), new Vector2(0, 25), new Vector2(600, 35), TextAnchor.MiddleCenter, FontStyle.Normal);
            AnchorBottomCenter(moduleFooter, 25f);
        }

        ModuleNavigationController navigation = root.AddComponent<ModuleNavigationController>();
        navigation.homePanel = home;
        navigation.modulePanels = modules;
        navigation.homeModuleButtons = moduleButtons;
        navigation.exitButton = exitButton;
        navigation.backButtons = backs;
        MainMenuController menu = root.AddComponent<MainMenuController>();
        menu.navigation = navigation;
        menu.decorativeWatermelon = melon.GetComponent<RectTransform>();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Watermelon Science: phase-two prototype created and saved to the active scene.");
    }

    private static void CreateAnatomyModule(GameObject module, out WatermelonAnatomyController anatomy)
    {
        CreateText("AnatomyGuide", module.transform, "点击上方切换视图，再选择右侧结构标签", 20, new Color32(92, 111, 96, 255), new Vector2(0, 258), new Vector2(1000, 42), TextAnchor.MiddleCenter, FontStyle.Normal);

        GameObject diagramArea = CreateImage("DiagramArea", module.transform, PaleGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-345, -35), new Vector2(720, 510)));
        AddOutline(diagramArea, new Color32(189, 221, 171, 255), 3f);
        Button wholeButton = CreateButton("WholeViewButton", module.transform, "完整外形", WatermelonGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-515, 205), new Vector2(205, 58)), 20, White).GetComponent<Button>();
        Button crossButton = CreateButton("CrossSectionButton", module.transform, "剖面结构", FleshRed, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-290, 205), new Vector2(205, 58)), 20, White).GetComponent<Button>();

        GameObject wholeView = CreateImage("WholeView", diagramArea.transform, White, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(510, 350)));
        AssignSprite(wholeView, "Assets/Art/watermelon_whole.png");
        GameObject wholeTip = CreateText("WholeTip", diagramArea.transform, "观察：绿色外皮是西瓜最外层的保护结构", 18, DeepGreen, new Vector2(0, -208), new Vector2(600, 45), TextAnchor.MiddleCenter, FontStyle.Normal);

        GameObject crossView = CreateImage("CrossSectionView", diagramArea.transform, White, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(510, 350)));
        AssignSprite(crossView, "Assets/Art/watermelon_cross_section.png");
        GameObject crossTip = CreateText("CrossTip", diagramArea.transform, "观察：切开后可以看到不同层次与种子分布", 18, DeepGreen, new Vector2(0, -208), new Vector2(600, 45), TextAnchor.MiddleCenter, FontStyle.Normal);
        crossView.SetActive(false);
        crossTip.SetActive(false);

        GameObject infoCard = CreateImage("InfoCard", module.transform, White, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(395, -35), new Vector2(540, 510)));
        AddOutline(infoCard, new Color32(232, 220, 190, 255), 3f);
        CreateText("InfoEyebrow", infoCard.transform, "结构小档案", 18, FleshRed, new Vector2(0, 205), new Vector2(460, 35), TextAnchor.MiddleCenter, FontStyle.Bold);
        Text infoTitle = CreateText("InfoTitle", infoCard.transform, "果皮", 38, DeepGreen, new Vector2(0, 158), new Vector2(460, 60), TextAnchor.MiddleCenter, FontStyle.Bold).GetComponent<Text>();
        Text infoBody = CreateText("InfoBody", infoCard.transform, "", 20, Ink, new Vector2(0, 86), new Vector2(450, 90), TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Text>();
        Button[] structureButtons = new Button[4];
        string[] structures = { "果皮  ·  绿色保护层", "果肉  ·  西瓜红可食部", "种子  ·  深褐色胚体", "瓜瓤  ·  浅绿色连接层" };
        Color[] structureColors = { new Color32(53, 128, 74, 255), new Color32(239, 102, 87, 255), new Color32(88, 65, 44, 255), new Color32(141, 192, 81, 255) };
        for (int i = 0; i < structures.Length; i++)
        {
            structureButtons[i] = CreateButton("StructureButton_" + i, infoCard.transform, structures[i], i == 0 ? structureColors[i] : new Color32(236, 240, 222, 255), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10 - i * 62), new Vector2(430, 50)), 17, i == 0 ? White : DeepGreen).GetComponent<Button>();
        }
        Text selectedHint = CreateText("SelectedHint", infoCard.transform, "当前视图：完整外形", 16, new Color32(92, 111, 96, 255), new Vector2(0, -218), new Vector2(460, 32), TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Text>();

        anatomy = module.AddComponent<WatermelonAnatomyController>();
        anatomy.wholeView = wholeView;
        anatomy.crossSectionView = crossView;
        anatomy.wholeViewButton = wholeButton;
        anatomy.crossSectionButton = crossButton;
        anatomy.wholeViewHint = wholeTip;
        anatomy.crossSectionHint = crossTip;
        anatomy.structureButtons = structureButtons;
        anatomy.infoTitle = infoTitle;
        anatomy.infoBody = infoBody;
        anatomy.selectedHint = selectedHint;
    }

    private static void CreateGrowthModule(GameObject module, out WatermelonGrowthController growth)
    {
        CreateText("GrowthGuide", module.transform, "一颗西瓜，从种子到成熟果实的四步旅程", 20, new Color32(92, 111, 96, 255), new Vector2(0, 258), new Vector2(1000, 42), TextAnchor.MiddleCenter, FontStyle.Normal);
        GameObject timeline = CreateImage("GrowthTimeline", module.transform, new Color32(250, 244, 218, 255), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-360, -45), new Vector2(760, 500)));
        AddOutline(timeline, new Color32(230, 214, 170, 255), 3f);
        GameObject line = CreateImage("TimelineLine", timeline.transform, WatermelonGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(620, 8)));
        line.transform.SetAsFirstSibling();

        string[] stages = { "种子萌发", "幼苗生长", "开花授粉", "果实成熟" };
        Button[] buttons = new Button[4];
        GameObject[] visuals = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            float x = -270 + i * 180;
            GameObject node = CreateImage("StageNode_" + i, timeline.transform, i == 0 ? FleshRed : PaleGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -30), new Vector2(46, 46)));
            AddOutline(node, DeepGreen, 3f);
            CreateText("StageNodeNumber", node.transform, "0" + (i + 1), 16, i == 0 ? White : DeepGreen, Vector2.zero, new Vector2(45, 45), TextAnchor.MiddleCenter, FontStyle.Bold);
            buttons[i] = CreateButton("StageButton_" + i, timeline.transform, stages[i], i == 0 ? FleshRed : PaleGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -105), new Vector2(150, 52)), 17, i == 0 ? White : DeepGreen).GetComponent<Button>();
            visuals[i] = CreateGrowthVisual("StageVisual_" + i, timeline.transform, i, new Vector2(x, 110));
        }
        Text stageNumber = CreateText("StageNumber", module.transform, "01", 46, FleshRed, new Vector2(160, 205), new Vector2(120, 65), TextAnchor.MiddleCenter, FontStyle.Bold).GetComponent<Text>();
        Text stageTitle = CreateText("StageTitle", module.transform, "种子萌发", 36, DeepGreen, new Vector2(400, 205), new Vector2(400, 65), TextAnchor.MiddleLeft, FontStyle.Bold).GetComponent<Text>();
        Text stageBody = CreateText("StageBody", module.transform, "", 20, Ink, new Vector2(400, 83), new Vector2(460, 155), TextAnchor.UpperLeft, FontStyle.Normal).GetComponent<Text>();
        Text selectedHint = CreateText("SelectedHint", module.transform, "已选择阶段：种子萌发", 16, new Color32(92, 111, 96, 255), new Vector2(400, -215), new Vector2(460, 35), TextAnchor.MiddleLeft, FontStyle.Normal).GetComponent<Text>();
        growth = module.AddComponent<WatermelonGrowthController>();
        growth.stageButtons = buttons;
        growth.stageVisuals = visuals;
        growth.stageNumber = stageNumber;
        growth.stageTitle = stageTitle;
        growth.stageBody = stageBody;
        growth.selectedHint = selectedHint;
    }

    private static GameObject CreateGrowthVisual(string name, Transform parent, int index, Vector2 position)
    {
        GameObject visual = CreateImage(name, parent, new Color32(255, 255, 255, 210), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(145, 110)));
        AddOutline(visual, new Color32(226, 220, 190, 255), 2f);
        if (index == 0)
        {
            CreateImage("Soil", visual.transform, new Color32(146, 105, 67, 255), new RectSpec(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -42), new Vector2(125, 30)));
            CreateSeed(visual.transform, new Vector2(0, -15), 0f);
        }
        else if (index == 1)
        {
            CreateImage("Stem", visual.transform, WatermelonGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -5), new Vector2(12, 65)));
            CreateImage("LeafA", visual.transform, PaleGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-25, 18), new Vector2(54, 26)));
            CreateImage("LeafB", visual.transform, PaleGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(25, 2), new Vector2(54, 26)));
        }
        else if (index == 2)
        {
            CreateImage("FlowerStem", visual.transform, WatermelonGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(12, 75)));
            for (int petal = 0; petal < 5; petal++)
            {
                float angle = petal * 72f * Mathf.Deg2Rad;
                CreateImage("Petal", visual.transform, new Color32(255, 220, 92, 255), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(angle) * 24f, 25 + Mathf.Sin(angle) * 24f), new Vector2(24, 34)));
            }
        }
        else
        {
            GameObject fruit = CreateImage("Fruit", visual.transform, FleshRed, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(86, 62)));
            AddOutline(fruit, DeepGreen, 3f);
            CreateImage("FruitLeaf", visual.transform, WatermelonGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-34, 37), new Vector2(45, 18)));
        }
        return visual;
    }

    private static void CreateNutritionModule(GameObject module, out WatermelonNutritionController nutrition)
    {
        CreateText("NutritionGuide", module.transform, "每 100 g 可食部的教学数据，点击卡片查看重点", 20, new Color32(92, 111, 96, 255), new Vector2(0, 258), new Vector2(1000, 42), TextAnchor.MiddleCenter, FontStyle.Normal);
        GameObject metricCard = CreateImage("MetricCard", module.transform, White, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400, -25), new Vector2(440, 450)));
        AddOutline(metricCard, new Color32(232, 220, 190, 255), 3f);
        Button[] nutrientButtons = new Button[3];
        string[] names = { "水分", "糖类", "能量" };
        for (int i = 0; i < 3; i++) nutrientButtons[i] = CreateButton("NutrientButton_" + i, metricCard.transform, names[i], i == 0 ? FleshRed : new Color32(255, 231, 180, 255), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-135 + i * 135, 150), new Vector2(115, 50)), 17, i == 0 ? White : DeepGreen).GetComponent<Button>();
        Text nutrientTitle = CreateText("NutrientTitle", metricCard.transform, "水分", 34, DeepGreen, new Vector2(0, 75), new Vector2(360, 55), TextAnchor.MiddleCenter, FontStyle.Bold).GetComponent<Text>();
        Text nutrientValue = CreateText("NutrientValue", metricCard.transform, "约 91.5", 60, FleshRed, new Vector2(0, 5), new Vector2(360, 80), TextAnchor.MiddleCenter, FontStyle.Bold).GetComponent<Text>();
        Text nutrientUnit = CreateText("NutrientUnit", metricCard.transform, "%", 20, Ink, new Vector2(0, -50), new Vector2(360, 40), TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Text>();
        Text nutrientBody = CreateText("NutrientBody", metricCard.transform, "", 18, Ink, new Vector2(0, -130), new Vector2(360, 95), TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Text>();
        Text selectedHint = CreateText("SelectedHint", metricCard.transform, "当前主题：水分", 15, new Color32(92, 111, 96, 255), new Vector2(0, -195), new Vector2(360, 30), TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Text>();
        GameObject chartCard = CreateImage("ChartCard", module.transform, new Color32(250, 244, 218, 255), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300, -25), new Vector2(620, 450)));
        AddOutline(chartCard, new Color32(230, 214, 170, 255), 3f);
        CreateText("ChartTitle", chartCard.transform, "相对比例示意", 23, DeepGreen, new Vector2(0, 165), new Vector2(500, 45), TextAnchor.MiddleCenter, FontStyle.Bold);
        CreateText("ChartHint", chartCard.transform, "用于课堂理解，不代表三项指标同一计量单位", 15, new Color32(92, 111, 96, 255), new Vector2(0, 125), new Vector2(550, 30), TextAnchor.MiddleCenter, FontStyle.Normal);
        string[] chartNames = { "水分", "糖类", "能量" };
        for (int i = 0; i < 3; i++)
        {
            float y = 55 - i * 75;
            CreateText("ChartLabel_" + i, chartCard.transform, chartNames[i], 18, Ink, new Vector2(-230, y), new Vector2(100, 35), TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateImage("ChartTrack_" + i, chartCard.transform, new Color32(231, 224, 198, 255), new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(45, y), new Vector2(320, 24)));
        }
        GameObject highlightedBar = CreateImage("HighlightedBar", chartCard.transform, FleshRed, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-115, 55), new Vector2(320, 24)));
        Text sourceText = CreateText("SourceText", chartCard.transform, "参考：常见生鲜营养成分表", 14, new Color32(92, 111, 96, 255), new Vector2(0, -175), new Vector2(540, 35), TextAnchor.MiddleCenter, FontStyle.Normal).GetComponent<Text>();
        nutrition = module.AddComponent<WatermelonNutritionController>();
        nutrition.nutrientButtons = nutrientButtons;
        nutrition.nutrientTitle = nutrientTitle;
        nutrition.nutrientValue = nutrientValue;
        nutrition.nutrientUnit = nutrientUnit;
        nutrition.nutrientBody = nutrientBody;
        nutrition.selectedHint = selectedHint;
        nutrition.highlightedBar = highlightedBar.GetComponent<RectTransform>();
        nutrition.sourceText = sourceText;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
    }

    private static void AssignSprite(GameObject go, string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Sprite candidate)
                {
                    sprite = candidate;
                    break;
                }
            }
        }
        if (sprite == null)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name + "_Sprite";
                AssetDatabase.AddObjectToAsset(sprite, texture);
                AssetDatabase.SaveAssets();
            }
        }
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color, RectSpec rect)
    {
        return CreateImage(name, parent, color, rect);
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, RectSpec rect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        ApplyRect(rt, rect);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return go;
    }

    private static GameObject CreateText(string name, Transform parent, string text, int fontSize, Color color, Vector2 position, Vector2 size, TextAnchor alignment, FontStyle style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        ApplyRect(rt, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size));
        Text label = go.GetComponent<Text>();
        label.text = text;
        label.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = alignment;
        label.fontStyle = style;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return go;
    }

    private static GameObject CreateButton(string name, Transform parent, string text, Color color, RectSpec rect, int fontSize, Color textColor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        ApplyRect(go.GetComponent<RectTransform>(), rect);
        Image image = go.GetComponent<Image>();
        image.color = color;
        AddOutline(go, new Color(0, 0, 0, 0.08f), 2f);
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = Color.Lerp(color, White, 0.16f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        CreateText("Label", go.transform, text, fontSize, textColor, Vector2.zero, rect.size - new Vector2(24, 10), TextAnchor.MiddleCenter, FontStyle.Bold);
        return go;
    }

    private static void CreateSeed(Transform parent, Vector2 position, float rotation)
    {
        GameObject seed = CreateImage("Seed", parent, DeepGreen, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(13, 24)));
        seed.transform.localRotation = Quaternion.Euler(0, 0, rotation);
    }

    private static void AddOutline(GameObject go, Color color, float width)
    {
        Outline outline = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
    }

    private static RectSpec Stretch()
    {
        return new RectSpec(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void ApplyRect(RectTransform rt, RectSpec spec)
    {
        rt.anchorMin = spec.anchorMin;
        rt.anchorMax = spec.anchorMax;
        rt.anchoredPosition = spec.position;
        rt.sizeDelta = spec.size;
        rt.localScale = Vector3.one;
    }

    private static void AnchorBottomCenter(GameObject go, float bottomOffset)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        // The project uses a centered full-screen canvas. Keep the footer centered
        // horizontally and offset it relative to the canvas center so it stays
        // below the content at the reference 1920x1080 resolution.
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -500f);
    }

    private readonly struct RectSpec
    {
        public readonly Vector2 anchorMin;
        public readonly Vector2 anchorMax;
        public readonly Vector2 position;
        public readonly Vector2 size;

        public RectSpec(Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            this.anchorMin = anchorMin;
            this.anchorMax = anchorMax;
            this.position = position;
            this.size = size;
        }
    }
}



