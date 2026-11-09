#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace WatermelonFarm.EditorTools
{
    /// <summary>
    /// 从源工程导入后的农场编辑器自检工具。
    /// 只做只读校验与临时 PlayerPrefs 测试，不重建场景、不修改主工程 PlayerSettings / Build Settings。
    /// </summary>
    public static class FarmProjectSetup
    {
        [MenuItem("Tools/瓜田农场/刷新美术素材")]
        public static void ReimportArt()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new string[] { "Assets/Subgames/WatermelonFarm/Resources/Art" });
            for (int i = 0; i < guids.Length; i++)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guids[i]),
                    ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log("瓜田农场：已按 Sprite/透明/不压缩重新导入 " + guids.Length + " 张美术素材。");
        }

        [MenuItem("Tools/瓜田农场/校验美术资源")]
        public static void VerifyArt()
        {
            string[] names = { "bg_farm", "soil", "soil_locked", "plant0", "plant1", "plant2",
                "plant3", "plant4", "plant5", "plant6", "plant7", "plant_dead",
                "melon_jx", "melon_hx", "melon_jxiu",
                "photo_jx", "photo_hx", "photo_jxiu",
                "tool_water", "tool_fert", "tool_hoe", "tool_spray" };
            int ok = 0;
            for (int i = 0; i < names.Length; i++)
            {
                Sprite s = ArtBank.Get(names[i]);
                if (s != null) ok++;
                Debug.Log("瓜田农场·素材 " + names[i] + " = " +
                    (s != null ? s.texture.width + "x" + s.texture.height : "缺失（将回退程序化绘制）"));
            }
            Debug.Log("瓜田农场：可加载素材 " + ok + "/" + names.Length);

            // 品种 → 成熟瓜图 的映射检查（田里成熟与图鉴卡都走这条路径）
            for (int i = 0; i < FarmData.V.Length; i++)
            {
                Sprite r = ArtBank.Ripe(FarmData.V[i].id);
                Debug.Log("瓜田农场·品种图 " + FarmData.V[i].name + " → " +
                    (r != null ? r.name + " " + r.texture.width + "x" + r.texture.height
                               : "缺失（回退 plant7）"));
            }
        }

        // 打印各分辨率下的板块坐标并检查越界 / 重叠，便于验证响应式布局
        [MenuItem("Tools/瓜田农场/校验响应式布局")]
        public static void VerifyLayout()
        {
            float[,] cases = { { 1920f, 1080f }, { 2560f, 1440f }, { 1920f, 1350f }, { 1920f, 1200f } };
            for (int c = 0; c < cases.GetLength(0); c++)
            {
                float w = cases[c, 0], h = cases[c, 1];
                FarmLayout L = FarmLayout.Compute(w, h);
                float gridRight = L.farmX + L.gridPadX + 3f * L.plotW + 2f * L.gap;
                float gridBottom = L.farmY + L.skyH + L.pad + 3f * L.plotH + 2f * L.gap;
                float farmRight = L.farmX + L.farmW;
                float logBottom = L.logY + L.logH;
                float panelH = 680f * L.modalScale;
                // 图鉴 / 商城弹窗使用加宽加高面板，同样必须完整落在画布内
                float bookW = 880f * L.modalScale, bookH = 760f * L.modalScale;
                float shopW = 820f * L.modalScale, shopH = 660f * L.modalScale;

                bool ok = L.farmX >= 0f && farmRight <= w + 0.5f &&
                          gridRight <= farmRight + 0.5f && gridBottom <= L.farmY + L.farmH + 0.5f &&
                          logBottom <= L.bodyBottom + 0.5f &&
                          L.tickerY + L.tickerH <= L.logY + 0.5f &&
                          panelH <= h && bookW <= w && bookH <= h &&
                          shopW <= w && shopH <= h &&
                          L.plotW > 0f && L.plotH > 0f;

                Debug.Log("瓜田农场·布局 " + w + "x" + h +
                    " | 侧栏=" + L.side + " 倍率=" + L.S.ToString("F2") +
                    " | 顶栏=" + L.topH.ToString("F0") +
                    " 工具栏Y=" + L.toolY.ToString("F0") +
                    " | 田地=(" + L.farmX.ToString("F0") + "," + L.farmY.ToString("F0") +
                    ") " + L.farmW.ToString("F0") + "x" + L.farmH.ToString("F0") +
                    " | 地块=" + L.plotW.ToString("F0") + "x" + L.plotH.ToString("F0") +
                    " | 日志=(" + L.logX.ToString("F0") + "," + L.logY.ToString("F0") +
                    ") " + L.logW.ToString("F0") + "x" + L.logH.ToString("F0") +
                    " 行数=" + L.logMax +
                    " | 弹窗=" + (660f * L.modalScale).ToString("F0") + "x" + panelH.ToString("F0") +
                    " | " + (ok ? "通过" : "★越界★"));
            }
        }

        // 校验 4 张工具光标图 + 系统光标纹理 + 倍速只保留 1x/2x（含旧存档收敛）
        [MenuItem("Tools/瓜田农场/校验工具光标与倍速")]
        public static void VerifyToolsAndSpeed()
        {
            int ok = 0;
            for (int i = 0; i < ToolCursor.Tools.Length; i++)
            {
                string t = ToolCursor.Tools[i];
                Sprite s = ToolCursor.Sprite(t);
                Texture2D tex = ToolCursor.OSTexture(t);
                Vector2 pivot = ToolCursor.Pivot(t);
                bool good = s != null && tex != null &&
                            pivot.x >= 0f && pivot.x <= 1f && pivot.y >= 0f && pivot.y <= 1f;
                if (good) ok++;
                Debug.Log("瓜田农场·工具光标 " + t + " → 精灵=" +
                    (s != null ? s.texture.width + "x" + s.texture.height : "缺失") +
                    " 系统光标=" + (tex != null ? tex.width + "x" + tex.height : "缺失") +
                    " 热点pivot=" + pivot.ToString("F2") + " | " + (good ? "通过" : "★失败★"));
            }

            bool cfg = FarmData.SPEEDS.Length == 2 && FarmData.SPEED_LABELS.Length == 2 &&
                       Mathf.Abs(FarmData.SPEEDS[0] - 1f) < 0.01f &&
                       Mathf.Abs(FarmData.SPEEDS[1] - 2f) < 0.01f &&
                       FarmData.SPEED_LABELS[0] == "1x" && FarmData.SPEED_LABELS[1] == "2x";

            bool hadSave = PlayerPrefs.HasKey(FarmModel.SAVE_KEY);
            string saveBackup = PlayerPrefs.GetString(FarmModel.SAVE_KEY, "");
            bool okSet = false, okLoad = false;
            try
            {
                FarmModel m = new FarmModel();
                m.view = new VerifyFarmView();
                m.P = SaveData.NewGame();
                m.SetSpeed(1.5f);
                bool a = Mathf.Abs(m.P.speed - 2f) < 0.01f;
                m.SetSpeed(3f);
                bool b = Mathf.Abs(m.P.speed - 2f) < 0.01f;
                m.SetSpeed(1f);
                bool c = Mathf.Abs(m.P.speed - 1f) < 0.01f;
                m.SetSpeed(2f);
                bool d = Mathf.Abs(m.P.speed - 2f) < 0.01f;
                okSet = a && b && c && d;

                // 模拟旧存档里的 1.5x：读档后应自动收敛为 2x
                SaveData old = SaveData.NewGame();
                old.speed = 1.5f;
                PlayerPrefs.SetString(FarmModel.SAVE_KEY, JsonUtility.ToJson(old));
                FarmModel m2 = new FarmModel();
                m2.view = new VerifyFarmView();
                m2.Init();
                okLoad = Mathf.Abs(m2.P.speed - 2f) < 0.01f;
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·光标与倍速自检 异常：" + e.Message);
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(FarmModel.SAVE_KEY, saveBackup);
                else PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
                PlayerPrefs.Save();
            }

            bool all = ok == ToolCursor.Tools.Length && cfg && okSet && okLoad;
            Debug.Log("瓜田农场·光标与倍速自检 工具图=" + ok + "/" + ToolCursor.Tools.Length +
                " 倍速档位=1x/2x:" + cfg + " SetSpeed归一化=" + okSet +
                " 旧存档归一化=" + okLoad + " | " + (all ? "全部通过" : "★存在失败项★"));
        }

        // 校验：图鉴真实照片 / 收藏卡结构化数据 / 一键按钮购买前锁定 / 1500金新系统 / 存档持久化
        [MenuItem("Tools/瓜田农场/校验图鉴与自动化系统")]
        public static void VerifyBookAndSystems()
        {
            int photoOk = 0;
            for (int i = 0; i < FarmData.V.Length; i++)
            {
                Sprite s = ArtBank.Photo(FarmData.V[i].id);
                if (s != null) photoOk++;
                Debug.Log("瓜田农场·实物照片 " + FarmData.V[i].name + " → " +
                    (s != null ? s.texture.width + "x" + s.texture.height : "缺失（回退插画）"));
            }

            bool hadSave = PlayerPrefs.HasKey(FarmModel.SAVE_KEY);
            string saveBackup = PlayerPrefs.GetString(FarmModel.SAVE_KEY, "");
            bool gatedWater = false, gatedClean = false, promptWater = false, promptClean = false;
            bool shopOk = false, buyOk = false, cleanWorks = false, persist = false, oldSaveOk = false;
            bool bookOk = false;
            try
            {
                VerifyFarmView v = new VerifyFarmView();
                FarmModel m = new FarmModel();
                m.view = v;
                m.P = SaveData.NewGame();

                // 图鉴：3 张结构化卡，字段齐全、无超长拼接文本
                m.P.cards.Add(new CardEntry { key = "jx", q = 88, tier = "瓜王金卡", brix = "12.1", wt = "4.60" });
                m.BookDialog();
                ModalSpec bk = v.lastModal;
                bookOk = bk != null && bk.book.Count == 3 && bk.book[0].got &&
                    bk.book[0].name == FarmData.V[0].name && bk.book[0].quality == "88" &&
                    bk.book[0].tier == "瓜王金卡" && !bk.book[1].got &&
                    !string.IsNullOrEmpty(bk.book[2].file) &&
                    bk.body.Contains("实物资料图");
                m.P.cards.Clear();

                // 旧存档（没有 autoClean 字段）读取后应为 false，不误解锁
                SaveData old = SaveData.NewGame();
                PlayerPrefs.SetString(FarmModel.SAVE_KEY, JsonUtility.ToJson(old));
                FarmModel m0 = new FarmModel();
                m0.view = v;
                m0.Init();
                oldSaveOk = !m0.P.autoClean;

                // 未买自动浇水：一键浇水不生效并弹「请前往商城购买」
                m.P.plots[0].v = "jx";
                m.P.plots[0].water = 10f;
                m.AllWater();
                gatedWater = Mathf.Abs(m.P.plots[0].water - 10f) < 0.01f;
                promptWater = v.lastModal != null &&
                    v.lastModal.body.Contains("请前往商城购买自动浇水系统") &&
                    v.lastModal.buttons.Count == 2 && v.lastModal.buttons[0].label == "前往商城";

                // 未买自动除虫：一键除草除虫不生效并弹对应提示
                m.P.plots[0].weed = true;
                m.P.plots[0].pest = "蚜虫";
                m.AllClean();
                gatedClean = m.P.plots[0].weed && m.P.plots[0].pest == "蚜虫";
                promptClean = v.lastModal != null &&
                    v.lastModal.body.Contains("请前往商城购买自动除草除虫系统");

                // 商城出现 1500 金新系统（第三行）
                m.ShopDialog();
                shopOk = v.lastModal != null && v.lastModal.rows.Count == 3 &&
                    v.lastModal.body.Contains(FarmData.AC_PRICE + "金");

                // 购买 → 扣款 → 解锁 → 一键清理生效 → 存档持久化
                m.P.coins = FarmData.AC_PRICE;
                m.BuyAutoClean();
                buyOk = m.P.autoClean && m.P.coins == 0;
                m.AllClean();
                cleanWorks = !m.P.plots[0].weed && string.IsNullOrEmpty(m.P.plots[0].pest);
                m.Save();
                FarmModel m2 = new FarmModel();
                m2.view = v;
                m2.Init();
                persist = m2.P.autoClean;
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·图鉴与自动化自检 异常：" + e.Message);
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(FarmModel.SAVE_KEY, saveBackup);
                else PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
                PlayerPrefs.Save();
            }

            bool priceOk = FarmData.AC_PRICE == 1500;
            bool all = photoOk == 3 && bookOk && priceOk && oldSaveOk && gatedWater &&
                gatedClean && promptWater && promptClean && shopOk && buyOk && cleanWorks && persist;
            Debug.Log("瓜田农场·图鉴与自动化自检 实物照片=" + photoOk + "/3 图鉴卡=" + bookOk +
                " 新系统价格1500=" + priceOk + " 旧存档兼容=" + oldSaveOk +
                " 浇水锁定=" + gatedWater + " 除虫锁定=" + gatedClean +
                " 浇水提示=" + promptWater + " 除虫提示=" + promptClean +
                " 商城行=" + shopOk + " 购买扣款=" + buyOk + " 一键清理=" + cleanWorks +
                " 存档持久化=" + persist + " | " + (all ? "全部通过" : "★存在失败项★"));
        }

        // 一次跑完全部自检，供批处理命令 -executeMethod 调用
        [MenuItem("Tools/瓜田农场/全部自检")]
        public static void VerifyAll()
        {
            Debug.Log("======== 瓜田农场 · 全部自检开始 ========");
            ReimportArt();
            VerifyArt();
            VerifyLayout();
            VerifyShards();
            VerifyToolsAndSpeed();
            VerifyBookAndSystems();
            VerifyBookVisual();
            VerifyShopVisual();
            VerifyWiltRule();
            VerifyWiltVisual();
            Debug.Log("======== 瓜田农场 · 全部自检结束 ========");
        }

        // 真实构建一次图鉴弹窗，用 UGUI 的 preferredHeight 测量每个卡片文本框是否溢出
        [MenuItem("Tools/瓜田农场/校验图鉴文字不溢出（真实UGUI测量）")]
        public static void VerifyBookVisual()
        {
            GameObject canvasGO = null;
            try
            {
                canvasGO = new GameObject("__BookVerifyCanvas", typeof(RectTransform), typeof(Canvas));
                Canvas cv = canvasGO.GetComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform crt = (RectTransform)canvasGO.transform;
                crt.sizeDelta = new Vector2(1920f, 1080f);

                FarmModel m = new FarmModel();
                FarmUI ui = new FarmUI();
                m.view = ui;
                m.P = SaveData.NewGame();
                // 覆盖「已解锁 / 未解锁 / 最高品级」三种卡面，用最长的字段压测
                m.P.cards.Add(new CardEntry { key = "jx", q = 92, tier = "瓜王金卡", brix = "12.8", wt = "5.20" });
                m.P.cards.Add(new CardEntry { key = "hx", q = 71, tier = "金牌卡", brix = "13.1", wt = "5.05" });
                ui.M = m;
                ui.Build(crt, 1920f, 1080f);
                ui.Render();
                m.BookDialog();
                Canvas.ForceUpdateCanvases();

                Text[] texts = canvasGO.GetComponentsInChildren<Text>(true);
                int cardTexts = 0, overflow = 0;
                float worst = 0f;
                string worstName = "";
                for (int i = 0; i < texts.Length; i++)
                {
                    Text t = texts[i];
                    Transform p = t.transform;
                    bool inCard = false;
                    while (p != null)
                    {
                        if (p.name.StartsWith("BookCard")) { inCard = true; break; }
                        p = p.parent;
                    }
                    if (!inCard) continue;
                    cardTexts++;
                    float need = t.preferredHeight;
                    float have = t.rectTransform.rect.height;
                    if (need > have + 2f)
                    {
                        overflow++;
                        if (need - have > worst)
                        {
                            worst = need - have;
                            worstName = t.name + " (" + t.text.Replace("\n", " ").Substring(0,
                                Mathf.Min(24, t.text.Length)) + ")";
                        }
                    }
                }
                ui.ReleaseCursor();
                Debug.Log("瓜田农场·图鉴文字实测 卡片文本框=" + cardTexts +
                    " 溢出=" + overflow + " 最大超出=" + worst.ToString("F1") + "px" +
                    (overflow > 0 ? " 最差=" + worstName : "") +
                    " | " + (cardTexts >= 30 && overflow == 0 ? "全部通过" : "★需检查★"));

                // 一键按钮锁定文案不得换行（真实 UGUI TextGenerator 测量行数）
                int wrapCount = 0, checkedBtns = 0;
                Transform[] all = canvasGO.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name != "AllWater" && all[i].name != "AllClean") continue;
                    Text lab = null;
                    for (int c = 0; c < all[i].childCount; c++)
                    {
                        Text tt = all[i].GetChild(c).GetComponent<Text>();
                        if (tt != null) { lab = tt; break; }
                    }
                    if (lab == null) continue;
                    checkedBtns++;
                    TextGenerator tg = new TextGenerator();
                    TextGenerationSettings st = lab.GetGenerationSettings(lab.rectTransform.rect.size);
                    st.horizontalOverflow = HorizontalWrapMode.Wrap;
                    st.verticalOverflow = VerticalWrapMode.Truncate;
                    tg.Populate(lab.text, st);
                    int lines = tg.lineCount;
                    if (lines > 1) wrapCount++;
                    Debug.Log("瓜田农场·一键按钮文案 " + all[i].name + " 「" + lab.text +
                        "」 行数=" + lines + " 可用宽=" + lab.rectTransform.rect.width.ToString("F0") +
                        " | " + (lines == 1 ? "通过" : "★换行★"));
                }
                Debug.Log("瓜田农场·一键按钮文案实测 检查=" + checkedBtns +
                    " 换行=" + wrapCount + " | " +
                    (checkedBtns == 2 && wrapCount == 0 ? "全部通过" : "★需检查★"));
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·图鉴文字实测 异常：" + e.Message);
            }
            finally
            {
                if (canvasGO != null) UnityEngine.Object.DestroyImmediate(canvasGO);
            }
        }

        // 真实构建一次商城弹窗：验证购买方式标题、大号按钮、提示位于购买区下方、卡内文字不溢出
        [MenuItem("Tools/瓜田农场/校验商城购买区（真实UGUI测量）")]
        public static void VerifyShopVisual()
        {
            GameObject canvasGO = null;
            try
            {
                canvasGO = new GameObject("__ShopVerifyCanvas", typeof(RectTransform), typeof(Canvas));
                Canvas cv = canvasGO.GetComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform crt = (RectTransform)canvasGO.transform;
                crt.sizeDelta = new Vector2(1920f, 1080f);

                FarmModel m = new FarmModel();
                FarmUI ui = new FarmUI();
                m.view = ui;
                m.P = SaveData.NewGame();
                m.P.coins = 5000;                 // 三件商品均可买，检查「立即购买」醒目态
                ui.M = m;
                ui.Build(crt, 1920f, 1080f);
                ui.Render();
                m.ShopDialog();
                Canvas.ForceUpdateCanvases();

                Transform[] all = canvasGO.GetComponentsInChildren<Transform>(true);
                float firstItemY = float.NaN, lastItemY = float.NaN, headerY = float.NaN, tipY = float.NaN;
                float bodyBottomY = float.NaN;
                int items = 0, bigBtns = 0;
                float minBtnW = float.MaxValue, minBtnH = float.MaxValue;
                bool tipTextOk = false, headerOk = false;
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    RectTransform rt = t as RectTransform;
                    if (rt == null) continue;
                    if (t.name == "ShopHeader") headerY = rt.anchoredPosition.y;
                    if (t.name == "TipBG") tipY = rt.anchoredPosition.y;
                    if (t.name == "Body" && t.parent != null && t.parent.name == "Content")
                        bodyBottomY = rt.anchoredPosition.y - rt.rect.height;
                    if (t.name.StartsWith("ShopItem"))
                    {
                        items++;
                        float y = rt.anchoredPosition.y;
                        if (float.IsNaN(firstItemY) || y > firstItemY) firstItemY = y;
                        if (float.IsNaN(lastItemY) || y < lastItemY) lastItemY = y;
                    }
                    if (t.name == "TipTxt")
                    {
                        Text tx = t.GetComponent<Text>();
                        if (tx != null && tx.text.Contains("补充说明")) tipTextOk = true;
                    }
                    if (t.name == "ShopHeaderTxt")
                    {
                        Text tx = t.GetComponent<Text>();
                        if (tx != null && tx.text.Contains("购买方式")) headerOk = true;
                    }
                    if (t.name.StartsWith("ShopBuy") && t.name.Length == 8)   // ShopBuy0 / 1 / 2
                    {
                        minBtnW = Mathf.Min(minBtnW, rt.rect.width);
                        minBtnH = Mathf.Min(minBtnH, rt.rect.height);
                        if (rt.rect.width >= 150f * 1.2f && rt.rect.height >= 44f * 1.2f) bigBtns++;
                    }
                }
                // 卡内文字（含按钮 Label）用真实 UGUI preferredHeight 复核
                Text[] texts = canvasGO.GetComponentsInChildren<Text>(true);
                int cardTexts = 0, overflow = 0;
                float worst = 0f; string worstName = "";
                for (int i = 0; i < texts.Length; i++)
                {
                    Text t = texts[i];
                    Transform p = t.transform;
                    bool inCard = false;
                    while (p != null)
                    {
                        if (p.name.StartsWith("ShopItem")) { inCard = true; break; }
                        p = p.parent;
                    }
                    if (!inCard) continue;
                    cardTexts++;
                    float need = t.preferredHeight, have = t.rectTransform.rect.height;
                    if (need > have + 2f)
                    {
                        overflow++;
                        if (need - have > worst) { worst = need - have; worstName = t.name; }
                    }
                }
                // 坐标系 y 向下为负：分区头在首卡上方，补充说明在末卡下方
                bool orderOk = !float.IsNaN(tipY) && !float.IsNaN(lastItemY) &&
                    tipY < lastItemY && !float.IsNaN(headerY) && headerY > firstItemY;
                bool sizeOk = bigBtns == 3 && minBtnW > 110f * 1.2f && minBtnH > 34f * 1.2f;
                // 正文用实测高度排版：购买区紧贴正文，不留大块空白
                float gap = bodyBottomY - headerY;
                bool compact = !float.IsNaN(gap) && gap >= -2f && gap < 30f;
                bool pass = items == 3 && sizeOk && orderOk && headerOk && tipTextOk &&
                    compact && cardTexts >= 24 && overflow == 0;
                ui.ReleaseCursor();
                Debug.Log("瓜田农场·商城购买区实测 商品卡=" + items +
                    " 醒目大按钮=" + bigBtns + "/3 最小按钮=" +
                    minBtnW.ToString("F0") + "x" + minBtnH.ToString("F0") + "（旧版132x41）" +
                    " 购买方式标题=" + headerOk + " 提示在购买区下方=" + orderOk +
                    " 补充说明=" + tipTextOk + " 正文→购买区=" + gap.ToString("F0") + "px" +
                    " 卡内文本框=" + cardTexts + " 溢出=" + overflow +
                    (overflow > 0 ? " 最差=" + worstName + "+" + worst.ToString("F1") : "") +
                    " | " + (pass ? "全部通过" : "★需检查★"));
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·商城购买区实测 异常：" + e.Message);
            }
            finally
            {
                if (canvasGO != null) UnityEngine.Object.DestroyImmediate(canvasGO);
            }
        }

        // 校验「有草 / 有虫 / 缺水 任一持续 10 秒未处理则枯萎」的逻辑层闭环
        [MenuItem("Tools/瓜田农场/校验10秒枯萎规则")]
        public static void VerifyWiltRule()
        {
            bool hadSave = PlayerPrefs.HasKey(FarmModel.SAVE_KEY);
            string backup = PlayerPrefs.GetString(FarmModel.SAVE_KEY, "");
            bool secOk = FarmData.DANGER_SEC == 10f && FarmData.DANGER_WATER == 30f;
            bool grassOk = false, pestOk = false, dryOk = false, halfOk = false;
            bool rescueOk = false, exemptOk = false, persistOk = false, toastOk = false;
            try
            {
                VerifyFarmView v = new VerifyFarmView();
                FarmModel m = new FarmModel();
                m.view = v;
                m.P = SaveData.NewGame();

                // 有草 10 秒 → 枯死 + 一条遗憾 Toast（品种名 / 原因正确）
                PlotData a = m.P.plots[0];
                a.v = "jx"; a.st = 3f; a.water = 60f; a.weed = true;
                m.TickDanger(FarmData.DANGER_SEC + 0.01f);
                grassOk = a.st == FarmData.DEAD_ST;
                toastOk = v.wiltToastCalls == 1 && v.wiltVariety == FarmData.V[0].name &&
                    v.wiltCause.Contains("有草");
                m.P.plots[0] = PlotData.NewPlot(0);

                // 虫害 10 秒 → 枯死
                PlotData b = m.P.plots[1];
                b.v = "hx"; b.st = 2f; b.water = 60f; b.pest = "蚜虫";
                m.TickDanger(FarmData.DANGER_SEC + 0.01f);
                pestOk = b.st == FarmData.DEAD_ST;
                m.P.plots[1] = PlotData.NewPlot(1);

                // 缺水 10 秒 → 枯死
                PlotData c = m.P.plots[2];
                c.v = "jxiu"; c.st = 1f; c.water = 20f;
                m.TickDanger(FarmData.DANGER_SEC + 0.01f);
                dryOk = c.st == FarmData.DEAD_ST;
                m.P.plots[2] = PlotData.NewPlot(2);

                // 半程 6 秒不死；浇水「做了相应操作」→ 计时清零，再等 9.9 秒也不死
                PlotData d = m.P.plots[3];
                d.v = "jx"; d.st = 2f; d.water = 15f;
                m.TickDanger(6f);
                halfOk = d.st < FarmData.RIPE_ST && d.danger >= 5.99f;
                m.DoWater(d);
                rescueOk = d.danger == 0f && d.st < FarmData.RIPE_ST;
                m.TickDanger(9.9f);
                rescueOk = rescueOk && d.st < FarmData.RIPE_ST && d.danger == 0f;

                // 成熟地块与空地豁免
                PlotData e = m.P.plots[4];
                e.v = "jx"; e.st = FarmData.RIPE_ST; e.water = 5f; e.weed = true;
                PlotData f = m.P.plots[5];
                f.water = 0f; f.weed = true; f.pest = "蚜虫";
                m.TickDanger(30f);
                exemptOk = e.st == FarmData.RIPE_ST && f.st < FarmData.RIPE_ST && f.danger == 0f;

                // 倒计时与原因写入存档（JsonUtility 往返）
                PlotData g = m.P.plots[6];
                g.v = "jx"; g.st = 2f; g.water = 60f; g.weed = true;
                m.TickDanger(3.5f);
                SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(m.P));
                persistOk = Mathf.Abs(restored.plots[6].danger - 3.5f) < 0.01f &&
                    restored.plots[6].dangerCause.Contains("有草");
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·枯萎规则自检 异常：" + e.Message);
            }
            finally
            {
                if (hadSave) PlayerPrefs.SetString(FarmModel.SAVE_KEY, backup);
                else PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
                PlayerPrefs.Save();
            }
            bool pass = secOk && grassOk && pestOk && dryOk && halfOk &&
                rescueOk && exemptOk && persistOk && toastOk;
            Debug.Log("瓜田农场·枯萎规则自检 阈值10s=" + secOk +
                " 有草枯死=" + grassOk + " 虫害枯死=" + pestOk + " 缺水枯死=" + dryOk +
                " 6s未死=" + halfOk + " 浇水后清零获救=" + rescueOk +
                " 成熟/空地豁免=" + exemptOk + " 倒计时存档=" + persistOk +
                " 遗憾Toast=" + toastOk + " | " + (pass ? "全部通过" : "★存在失败项★"));
        }

        // 真实构建一次 UGUI：检查徽章倒计时文本、遗憾 Toast 的 4 秒自动消失与点击任意处消失
        [MenuItem("Tools/瓜田农场/校验枯萎倒计时与遗憾弹窗（真实UGUI）")]
        public static void VerifyWiltVisual()
        {
            GameObject canvasGO = null;
            try
            {
                canvasGO = new GameObject("__WiltVerifyCanvas", typeof(RectTransform), typeof(Canvas));
                Canvas cv = canvasGO.GetComponent<Canvas>();
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform crt = (RectTransform)canvasGO.transform;
                crt.sizeDelta = new Vector2(1920f, 1080f);

                FarmModel m = new FarmModel();
                FarmUI ui = new FarmUI();
                m.view = ui;
                m.P = SaveData.NewGame();
                m.P.plots[0].v = "jx";
                m.P.plots[0].st = 3f;
                m.P.plots[0].water = 60f;
                m.P.plots[0].weed = true;
                m.P.plots[0].danger = 7f;        // 剩余 3 秒，徽章应变红并显示 3s
                ui.M = m;
                ui.Build(crt, 1920f, 1080f);
                ui.Render();
                Canvas.ForceUpdateCanvases();

                string badgeTxt = "";
                Color badgeColor = Color.clear;
                Transform[] all = canvasGO.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name != "Badge_weed") continue;
                    Image im = all[i].GetComponent<Image>();
                    if (im != null) badgeColor = im.color;
                    for (int c = 0; c < all[i].childCount; c++)
                    {
                        Text t = all[i].GetChild(c).GetComponent<Text>();
                        if (t != null) { badgeTxt = t.text; break; }
                    }
                }
                bool badgeOk = badgeTxt.Contains("有草") && badgeTxt.Contains("3s") &&
                    badgeColor.r > badgeColor.g;   // 危急红色

                ui.ShowWiltToast(FarmData.V[0].name, "缺水");
                Canvas.ForceUpdateCanvases();
                string title = "", hint = "";
                Color border = Color.clear;
                GameObject hitArea = null;
                Transform card = null;
                all = canvasGO.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "ToastHitArea") hitArea = all[i].gameObject;
                    if (all[i].name == "ToastCard") card = all[i];
                }
                if (card != null)
                {
                    for (int c = 0; c < card.childCount; c++)
                    {
                        Transform ch = card.GetChild(c);
                        Text t = ch.GetComponent<Text>();
                        Image im = ch.GetComponent<Image>();
                        if (t != null && t.text.Contains("真遗憾")) title = t.text;
                        if (t != null && t.text.Contains("4 秒")) hint = t.text;
                        if (ch.name == "Border" && im != null && im.color.r > im.color.g + 0.2f)
                            border = im.color;
                    }
                }
                bool cardOk = hitArea != null && hitArea.activeSelf && title.Contains("枯萎") &&
                    hint.Contains("4 秒") && hint.Contains("点击") && border != Color.clear;

                // 4 秒后自动消失（编辑期对象会被立即销毁，用「场景中是否还有 active 命中区」判定）
                ui.TickVisual(4.1f);
                int activeAfter4s = 0;
                all = canvasGO.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].name == "ToastHitArea" && all[i].gameObject.activeSelf) activeAfter4s++;
                bool autoGone = activeAfter4s == 0;
                // 再弹一次，点击全屏命中区关闭
                ui.ShowWiltToast(FarmData.V[0].name, "有草");
                GameObject hit2 = null;
                all = canvasGO.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].name == "ToastHitArea" && all[i].gameObject.activeSelf)
                        hit2 = all[i].gameObject;
                Button hitBtn = hit2 != null ? hit2.GetComponent<Button>() : null;
                bool hadBtn = hitBtn != null;      // Invoke 后对象会被销毁，先记下是否找到
                if (hitBtn != null) hitBtn.onClick.Invoke();
                int activeAfterClick = 0;
                all = canvasGO.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                    if (all[i].name == "ToastHitArea" && all[i].gameObject.activeSelf) activeAfterClick++;
                bool clickGone = hadBtn && activeAfterClick == 0;
                ui.ReleaseCursor();
                bool pass = badgeOk && cardOk && autoGone && clickGone;
                Debug.Log("瓜田农场·枯萎UI实测 徽章「" + badgeTxt + "」变红=" +
                    (badgeColor.r > badgeColor.g) + " 遗憾卡=" + cardOk +
                    " 4s自动消失=" + autoGone + " 点击任意处消失=" + clickGone +
                    " | " + (pass ? "全部通过" : "★需检查★"));
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·枯萎UI实测 异常：" + e.Message);
            }
            finally
            {
                if (canvasGO != null) UnityEngine.Object.DestroyImmediate(canvasGO);
            }
        }

        [MenuItem("Tools/瓜田农场/碎片钱包/查看余额与存档路径")]
        public static void ShowShards()
        {
            Debug.Log("瓜田农场·碎片钱包 已获得碎片 " + ShardWallet.Get(ShardWallet.WATERMELON) +
                " 个 | 独立存档键=" + ShardWallet.PREF_KEY +
                " | 跨游戏共享文件=" + ShardWallet.SharedFilePath +
                " | 全钱包JSON=" + ShardWallet.Export());
        }

        [MenuItem("Tools/瓜田农场/碎片钱包/清零（测试用）")]
        public static void ClearShards()
        {
            ShardWallet.Remove(ShardWallet.WATERMELON);
            Debug.Log("瓜田农场：西瓜碎片已清零（农场存档不受影响）。");
        }

        // 假视图：只记录 Toast 调用，不创建任何 UGUI 物体，供批处理自检使用
        sealed class VerifyFarmView : IFarmView
        {
            public ModalSpec lastModal;
            public int toastCalls, toastGained, toastTotal;
            public int wiltToastCalls;
            public string wiltVariety = "", wiltCause = "";
            public void Render() { }
            public void AddLog(string type, string text) { }
            public void ShowModal(ModalSpec spec) { lastModal = spec; }
            public void CloseTopModal() { }
            public void ShowShardToast(int gained, int total)
            { toastCalls++; toastGained += gained; toastTotal = total; }
            public void ShowWiltToast(string variety, string cause)
            { wiltToastCalls++; wiltVariety = variety; wiltCause = cause; }
        }

        [MenuItem("Tools/瓜田农场/碎片钱包/自检（累计+持久化+售卖闭环）")]
        public static void VerifyShards()
        {
            // 自检会临时改动 PlayerPrefs / 共享文件，结束前完整恢复，不污染真实进度
            bool hadSave = PlayerPrefs.HasKey(FarmModel.SAVE_KEY);
            string saveBackup = PlayerPrefs.GetString(FarmModel.SAVE_KEY, "");
            int shardBackup = ShardWallet.Get(ShardWallet.WATERMELON);
            string fileBackup = null;
            bool hadFile = false;
            try
            {
                if (File.Exists(ShardWallet.SharedFilePath))
                {
                    fileBackup = File.ReadAllText(ShardWallet.SharedFilePath, Encoding.UTF8);
                    hadFile = true;
                }
            }
            catch (Exception) { }

            bool okWallet = false, okSell = false, okAll = false, okToast = false, okIndependent = false;
            int afterSell = -1, afterAll = -1;
            try
            {
                // A) 累计 + 强制重新读盘后的持久化
                ShardWallet.Remove("__selftest__");
                int a = ShardWallet.Add("__selftest__", 2);
                int b = ShardWallet.Add("__selftest__", 2);
                ShardWallet.Reload();
                int r = ShardWallet.Get("__selftest__");
                okWallet = (a == 2 && b == 4 && r == 4);
                ShardWallet.Remove("__selftest__");

                // B) 仓库售卖闭环：单个卖 +2、全部卖 +2×剩余瓜数
                ShardWallet.Set(ShardWallet.WATERMELON, 10);
                FarmModel m = new FarmModel();
                VerifyFarmView v = new VerifyFarmView();
                m.view = v;
                m.P = SaveData.NewGame();
                for (int i = 0; i < 2; i++)
                {
                    WarehouseItem w = new WarehouseItem();
                    w.v = "jx"; w.q = 70; w.wt = 4.2f; w.brix = 11f; w.coins = 80;
                    m.P.wh.Add(w);
                }
                m.Sell(0);
                afterSell = ShardWallet.Get(ShardWallet.WATERMELON);
                okSell = (afterSell == 12 && m.P.wh.Count == 1 && v.toastCalls == 1 && v.toastGained == 2);

                m.WarehouseDialog();
                Action allSell = null;
                if (v.lastModal != null)
                    for (int i = 0; i < v.lastModal.buttons.Count; i++)
                        if (v.lastModal.buttons[i].label.StartsWith("全部卖出"))
                            allSell = v.lastModal.buttons[i].onClick;
                if (allSell != null) allSell();
                afterAll = ShardWallet.Get(ShardWallet.WATERMELON);
                okAll = (afterAll == 14 && m.P.wh.Count == 0 && v.toastCalls == 2 && v.toastGained == 4);
                okToast = (v.toastTotal == 14);

                // C) 碎片与农场存档解耦：删掉农场存档键，碎片仍在
                PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
                ShardWallet.Reload();
                okIndependent = (ShardWallet.Get(ShardWallet.WATERMELON) == afterAll);
            }
            catch (Exception e)
            {
                Debug.LogError("瓜田农场·碎片自检 异常：" + e.Message);
            }
            finally
            {
                ShardWallet.Set(ShardWallet.WATERMELON, shardBackup);
                if (hadSave) PlayerPrefs.SetString(FarmModel.SAVE_KEY, saveBackup);
                else PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
                PlayerPrefs.Save();
                try
                {
                    if (hadFile) File.WriteAllText(ShardWallet.SharedFilePath, fileBackup,
                        new UTF8Encoding(false));
                }
                catch (Exception) { }
                ShardWallet.Reload();
            }

            Debug.Log("瓜田农场·碎片自检 累计持久化=" + okWallet + " 单卖+2=" + okSell +
                "(" + afterSell + ") 全卖+2N=" + okAll + "(" + afterAll + ") Toast=" + okToast +
                " 存档独立=" + okIndependent +
                " | " + (okWallet && okSell && okAll && okToast && okIndependent ? "全部通过" : "★存在失败项★") +
                " | 已恢复真实碎片余额 " + shardBackup);
        }

        [MenuItem("Tools/瓜田农场/清空存档（重新开档）")]
        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
            PlayerPrefs.Save();
            Debug.Log("瓜田农场：存档已清空，下次 Play 从 100 金 / 4 块地重新开始。" +
                " 西瓜碎片保存在独立钱包中，不会被清空（如需清零请用「碎片钱包/清零」）。");
        }
    }
}
#endif
