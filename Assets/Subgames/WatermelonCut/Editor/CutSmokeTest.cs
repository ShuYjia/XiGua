using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/* ============================================================
 * CutSmokeTest.cs —— 导入主工程后的切瓜运行时冒烟测试（仅 Editor）
 * 菜单：Tools/瓜里有数/运行时冒烟测试
 * 自动打开 Assets/Scenes/Subgames/Guayoulishu.unity 并进入 Play Mode，
 * 走 教学四步 → 九瓜观察 → 挑战校验 → 切瓜 → 结果 → 换一篮 → 首页，
 * 同时静态校验四类切面 / 音色 / 评分映射。结果写入 Logs/CutSmokeResult.txt。
 * 没有标记文件时不做任何事，不影响正常编辑。
 * ============================================================ */

[InitializeOnLoad]
public static class CutSmokeTest
{
    const string CutScenePath = "Assets/Scenes/Subgames/Guayoulishu.unity";

    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder trace = new StringBuilder();
    static GuiliGame game;
    static int stage, wait, frames, timeout;
    static bool finished, archivePending;
    static string firstSignature, firstCatalog;
    static bool currencyBackupValid, hadCurrency, hadMigrated;
    static int currencyBackup, migratedBackup;

    static string MarkerPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CutSmokeMarker")); }
    static string ResultPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "CutSmokeResult.txt")); }

    static CutSmokeTest()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            if (!msg.Contains("Licensing") && !msg.Contains("abort_threads")) errors.Add(msg);
    }

    [MenuItem("Tools/WatermelonCut/Run Runtime Smoke Test")]
    public static void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath()));
        BackupCurrency();
        File.WriteAllText(MarkerPath(), "run", new UTF8Encoding(false));
        if (File.Exists(ResultPath())) File.Delete(ResultPath());
        errors.Clear();
        EditorSceneManager.OpenScene(CutScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Say(string s) { trace.AppendLine("  " + s); }
    static void Check(bool ok, string what) { Say((ok ? "通过 · " : "失败 · ") + what); if (!ok) errors.Add(what); }

    static void Tick()
    {
        if (finished) return;
        if (!File.Exists(MarkerPath())) return;
        if (!EditorApplication.isPlaying) return;
        if (frames == 0)
        {
            trace.Length = 0;
            trace.AppendLine("《瓜里有数》Unity 运行时冒烟测试");
            trace.AppendLine("Unity " + Application.unityVersion);
            VerifyDeterministicMappings();
        }
        frames++;
        if (frames == 2) Time.timeScale = 40f;
        if (++timeout > 20000) { Diagnose(GuiliUI.Instance); Finish("超时：流程未在限定帧数内完成"); return; }
        if (game == null)
        {
            game = UnityEngine.Object.FindObjectOfType<GuiliGame>();
            if (game == null) return;
        }
        GuiliUI ui = GuiliUI.Instance;
        if (ui == null || GuiliScene.Instance == null) return;
        if (wait > 0) { wait--; return; }
        if (frames % 1000 == 0) Diagnose(ui);

        switch (stage)
        {
            case 0:
                Say("初始阶段 = " + game.State);
                game.StartTutorial();
                Say("开始教学 → " + game.State);
                stage = 1; wait = 8; break;
            case 1:
                game.Tool("skin");
                Say("教学 1/4 观瓜皮，弹窗=" + ui.ModalOpen);
                stage = 2; wait = 8; break;
            case 2:
                WaitModal("教学1弹窗", 1, GameStage.Tutorial); break;
            case 3:
                game.Tool("spot");
                Say("教学 2/4 看底部（翻底动画）");
                stage = 4; wait = 8; break;
            case 4:
                WaitModal("教学2弹窗", 2, GameStage.Tutorial); break;
            case 5:
                game.Tool("stem");
                Say("教学 3/4 辨瓜柄（瓜柄近观）");
                stage = 6; wait = 8; break;
            case 6:
                WaitModal("教学3弹窗", 3, GameStage.Tutorial); break;
            case 7:
                game.Tool("sound");
                Say("教学 4/4 轻叩听音，弹窗=" + ui.ModalOpen);
                stage = 8; wait = 8; break;
            case 8:
                WaitModal("教学4弹窗", 4, GameStage.Practice); break;
            case 9:
                if (game.State != GameStage.Practice) { wait = 2; return; }
                Say("教学完成 → 九瓜辨识，瓜数=" + GuiliScene.Instance.Melons.Count);
                SpotCheck("第 1 篮");
                firstSignature = game.RoundSignature;
                firstCatalog = game.CatalogOrder;
                stage = 10; wait = 5; break;

            case 10:
                game.LedgerClick("01");
                Say("点选 01 号瓜");
                game.Tool("skin");
                stage = 11; wait = 5; break;
            case 11:
                game.Tool("spot");
                stage = 12; wait = 8; break;
            case 12:
                game.Tool("stem");
                stage = 13; wait = 8; break;
            case 13:
                if (!archivePending)
                {
                    game.Tool("sound");
                    game.Tool("record");
                    Say("五项观察完成，档案弹窗=" + ui.ModalOpen);
                    /* 新激活的弹窗要等 Canvas 重建后才会注册到 GraphicRaycaster，
                       同一帧射线必然 0 命中，因此下一帧再点击 */
                    archivePending = true;
                    wait = 4;
                    break;
                }
                ClickModalOnly("档案弹窗");
                stage = 14; wait = 5; break;
            case 14:
                game.Next();
                Say("进入独立挑瓜 → " + game.State);
                stage = 15; wait = 5; break;
            case 15:
                string cat1 = game.CatalogOrder;
                game.ObserveAgain();
                game.Next();
                string cat2 = game.CatalogOrder;
                Say("同篮两次进入挑瓜，编号与西瓜排列保持不变 = " + (cat1 == cat2));
                if (cat1 != cat2) errors.Add("同一篮内编号位置或西瓜被改动，观察与选择将不对应");
                game.Submit();
                Say("空选择校验（应停留在 " + GameStage.Challenge + "）= " + game.State);
                stage = 16; wait = 5; break;
            case 16:
                game.Choose("01");
                game.Submit();
                Say("依据不足校验 = " + game.State);
                stage = 17; wait = 5; break;
            case 17:
                game.ToggleReason("skin");
                game.ToggleReason("record");
                game.Submit();
                Say("确认选择 → 切瓜 → " + game.State);
                stage = 18; wait = 8; break;
            case 18:
                if (!ui.CutVisible) { wait = 2; return; }
                Say("剖切动画完成，结果卡可见");
                stage = 19; wait = 5; break;
            case 19:
                {
                    int before = WatermelonCurrencyWallet.GetBalance();
                    game.ResultButton();
                    int delta = WatermelonCurrencyWallet.GetBalance() - before;
                    Check(delta >= 0 && delta <= 100,
                          "结算按 0-100 分发放等额西瓜币（+" + delta + "）");
                    Say("收下瓜笺 → " + game.State);
                }
                stage = 20; wait = 5; break;
            case 20:
                {
                    int beforeRetry = WatermelonCurrencyWallet.GetBalance();
                    game.ResultButton();
                    Check(WatermelonCurrencyWallet.GetBalance() == beforeRetry,
                          "重复点击结算按钮不重复发币");
                }
                game.Retry();
                Say("换一篮 → " + game.State + "，瓜数=" + GuiliScene.Instance.Melons.Count);
                SpotCheck("第 2 篮");
                Say("两篮随机签名不同 = " + (firstSignature != game.RoundSignature));
                Say("两篮「固定编号上的西瓜排列」不同 = " + (firstCatalog != game.CatalogOrder));
                if (firstSignature == game.RoundSignature)
                    errors.Add("相邻两篮随机签名相同，玩家可凭位置记忆选瓜");
                if (firstCatalog == game.CatalogOrder)
                    errors.Add("换一篮后固定编号上的西瓜未重新洗牌，玩家可凭记号码选瓜");
                stage = 21; wait = 5; break;
            case 21:
                game.GoHome();
                Say("合卷归首页 → " + game.State);
                stage = 22; wait = 5; break;
            case 22:
                game.Settings();
                Say("打开设置弹窗，modal=" + ui.ModalOpen);
                stage = 23; wait = 6; break;
            case 23:
                ClickModalCloseX("设置弹窗");
                stage = 24; wait = 4; break;
            case 24:
                Say("场景 = " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                Finish(null); break;
        }
    }

    /* 四类切面 / 音色 / 评分映射的确定性校验（不依赖随机瓜堆） */
    static void VerifyDeterministicMappings()
    {
        int bad = 0;
        string[] expectKnock = { "crisp", "muddy", "dull", "mellow" }; // ripe / raw / damaged / ordinary
        for (int i = 0; i < GuiliData.OutcomeKeys.Length; i++)
        {
            string cut = GuiliData.OutcomeKeys[i];
            if (GuiliData.KnockKey(cut) != expectKnock[i]) bad++;
            if (GuiliData.Outcomes[i, 0] == "" || GuiliData.Outcomes[i, 1] == "") bad++;
            MelonInfo probe = new MelonInfo();
            probe.cut = cut;
            probe.spotGrade = GuiliData.GradeFor(cut);
            if (!GuiliData.GradeMatchesCut(probe.spotGrade, cut)) bad++;
            int[] sc = GuiliData.Score(probe, new List<string>());
            int expectSel = cut == "ripe" ? 35 : cut == "ordinary" ? 22 : 0;
            if (sc[1] != expectSel) bad++;
        }
        Say("四类切面→音色/评分映射错误 = " + bad);
        if (bad > 0) errors.Add("四类切面 / 音色 / 评分映射存在 " + bad + " 处错误");
    }

    /* 编号固定 / 三色圆斑 ↔ 瓜皮贴图 ↔ 切瓜结果 ↔ 拍瓜声 ↔ 评分 的联动校验 */
    static void SpotCheck(string tag)
    {
        List<MelonInfo> infos = game.Infos;
        int[] count = new int[3];
        int bad = 0;
        for (int i = 0; i < infos.Count; i++)
        {
            MelonInfo m = infos[i];
            if (m.id != (i + 1).ToString("00")) bad++;
            if (m.slot != i) bad++;
            if (!GuiliData.GradeMatchesCut(m.spotGrade, m.cut)) bad++;
            if (m.knock != GuiliData.KnockKey(m.cut)) bad++;
            if (System.Array.IndexOf(GuiliData.KnockKeys, m.knock) < 0) bad++;
            count[(int)m.spotGrade]++;
            GuiliMelon gm = GuiliScene.Instance.Melons.Find(x => x.info == m);
            if (gm == null || gm.skin == null || gm.skinMat == null) bad++;
            int expectSel = m.cut == "ripe" ? 35 : m.cut == "ordinary" ? 22 : 0;
            int[] sc = GuiliData.Score(m, new List<string>());
            if (sc[1] != expectSel) bad++;
        }
        string shaderName = (GuiliScene.Instance.Melons.Count > 0 &&
                             GuiliScene.Instance.Melons[0].skinMat != null)
                            ? GuiliScene.Instance.Melons[0].skinMat.shader.name : "无";
        Say(tag + " 瓜皮贴图 shader = " + shaderName);
        Say(tag + " 瓜底三色: 白色=" + count[0] + " 奶油黄=" + count[1] + " 橙黄=" + count[2] +
            " | 编号/位置/圆斑/声音/评分联动错误=" + bad);
        for (int g = 0; g < 3; g++)
            if (count[g] == 0) errors.Add(tag + " 圆斑颜色 " + (SpotGrade)g + " 未出现");
        if (bad > 0) errors.Add(tag + " 编号/圆斑/声音/结果/评分联动错误 " + bad + " 处");
    }

    static void Diagnose(GuiliUI ui)
    {
        System.Reflection.FieldInfo stepField = typeof(GuiliGame).GetField("step",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int step = stepField == null ? -1 : (int)stepField.GetValue(game);
        GuiliMelon a = GuiliScene.Instance.Active;
        System.Reflection.FieldInfo animField = typeof(GuiliGame).GetField("anims",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        System.Collections.IList list = animField == null ? null : (System.Collections.IList)animField.GetValue(game);
        string animInfo = "anims=" + (list == null ? "?" : list.Count.ToString());
        if (list != null && list.Count > 0)
        {
            System.Reflection.FieldInfo tf = list[0].GetType().GetField("t");
            animInfo += " t0=" + (tf == null ? "?" : tf.GetValue(list[0]).ToString());
        }
        Say("诊断 frame=" + frames + " stage=" + stage + " step=" + step +
            " modal=" + ui.ModalOpen + " state=" + game.State + " " + animInfo +
            (a == null ? " active=null" : " busy=" + a.busy + " flip=" + a.flip.ToString("F2") + " target=" + a.target.ToString("F2")));
    }

    /* ================= 真实 UI 交互回归（射线命中 + 指针事件） ================= */

    static int CurrentStep()
    {
        System.Reflection.FieldInfo f = typeof(GuiliGame).GetField("step",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f == null ? -1 : (int)f.GetValue(game);
    }

    static bool ModalDonePending()
    {
        System.Reflection.FieldInfo f = typeof(GuiliUI).GetField("modalDone",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return f != null && f.GetValue(GuiliUI.Instance) != null;
    }

    static Transform ModalRoot()
    {
        System.Reflection.FieldInfo f = typeof(GuiliUI).GetField("modalGo",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        GameObject go = f == null ? null : (GameObject)f.GetValue(GuiliUI.Instance);
        return go == null ? null : go.transform;
    }

    static Vector2 ButtonCenter(Button b)
    {
        RectTransform rt = (RectTransform)b.transform;
        return RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
    }

    /* 先用真实 GraphicRaycaster 命中，再按 PointerDown → PointerUp → PointerClick 顺序派发 */
    static bool ClickButton(Button b, string label)
    {
        if (b == null) { errors.Add(label + "：按钮引用为空"); return false; }
        if (!b.gameObject.activeInHierarchy || !b.enabled || !b.interactable)
        {
            errors.Add(label + "：按钮不可交互（active=" + b.gameObject.activeInHierarchy +
                       " enabled=" + b.enabled + " interactable=" + b.interactable + "）");
            return false;
        }
        EventSystem es = EventSystem.current;
        if (es == null) { errors.Add(label + "：EventSystem 缺失"); return false; }
        Vector2 center = ButtonCenter(b);
        PointerEventData ped = new PointerEventData(es);
        ped.position = center;
        ped.button = PointerEventData.InputButton.Left;
        ped.pointerId = -1;

        List<RaycastResult> results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        if (results.Count == 0) { errors.Add(label + "：按钮中心射线无命中"); return false; }
        GameObject top = results[0].gameObject;
        GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top);
        Say(label + " 射线首命中 = " + top.name + "（共 " + results.Count + " 个），解析到 " +
            (handler == null ? "null" : handler.name));
        if (handler != b.gameObject)
        {
            errors.Add(label + "：点击目标解析错误，期望 " + b.gameObject.name);
            return false;
        }

        GameObject press = ExecuteEvents.ExecuteHierarchy(top, ped, ExecuteEvents.pointerDownHandler);
        ped.pointerPress = press;
        ped.rawPointerPress = top;
        ExecuteEvents.Execute(press, ped, ExecuteEvents.pointerUpHandler);
        if (ped.pointerPress == handler)
        {
            ped.clickCount = 1;
            ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
            Say(label + " 已派发 PointerDown / PointerUp / PointerClick");
        }
        else
        {
            errors.Add(label + "：PointerUp 目标与 PointerDown 不一致，未派发 PointerClick");
            return false;
        }
        return true;
    }

    /* 弹窗打开期间，下层可交互按钮不应还能被射线命中 */
    static void CheckModalBlocksUnderlying(string label)
    {
        Transform modal = ModalRoot();
        Button[] all = UnityEngine.Object.FindObjectsOfType<Button>();
        int reachable = 0;
        string sample = "";
        for (int i = 0; i < all.Length; i++)
        {
            Button b = all[i];
            if (!b.gameObject.activeInHierarchy || !b.IsInteractable()) continue;
            if (modal != null && b.transform.IsChildOf(modal)) continue;
            Vector2 center = ButtonCenter(b);
            PointerEventData ped = new PointerEventData(EventSystem.current);
            ped.position = center;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, results);
            if (results.Count == 0) continue;
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[0].gameObject);
            if (handler == b.gameObject)
            {
                reachable++;
                if (sample == "") sample = b.gameObject.name;
            }
        }
        Check(reachable == 0, label + "：弹窗打开时下层按钮不可点击（仍可达 " + reachable + " 个" +
                             (sample == "" ? "" : "，例如 " + sample) + "）");
    }

    /* 教学弹窗：点击「记住了」→ 弹窗关闭、modalDone 清空、教学步骤恰好推进一次 */
    static void ClickTutorialDone(string label, int expectStepAfter, GameStage expectStateAfter)
    {
        GuiliUI ui = GuiliUI.Instance;
        if (!ui.ModalOpen) { errors.Add(label + "：点击前弹窗未打开"); return; }
        int stepBefore = CurrentStep();
        GameStage stateBefore = game.State;
        Say(label + " 点击前 modal=" + ui.ModalOpen + " step=" + stepBefore +
            " state=" + stateBefore + " modalDone=" + (ModalDonePending() ? "pending" : "none"));
        if (label == "教学1弹窗") CheckModalBlocksUnderlying(label);
        if (!ClickButton(ui.ModalDoneButton, label)) return;

        Check(!ui.ModalOpen, label + "：点击「记住了」后弹窗关闭");
        Check(!ModalDonePending(), label + "：关闭后 modalDone 已清空");
        Check(CurrentStep() == stepBefore + 1, label + "：教学步骤恰好推进一次（" +
              stepBefore + " → " + CurrentStep() + "）");
        Check(game.State == expectStateAfter, label + "：点击后状态 = " + expectStateAfter +
              "（实际 " + game.State + "）");

        // 重复派发不得再次推进
        PointerEventData again = new PointerEventData(EventSystem.current);
        again.button = PointerEventData.InputButton.Left;
        again.pointerId = -1;
        again.clickCount = 2;
        ExecuteEvents.Execute(ui.ModalDoneButton.gameObject, again, ExecuteEvents.pointerClickHandler);
        Check(CurrentStep() == expectStepAfter, label + "：重复点击不再推进教学步骤（仍为 " +
              CurrentStep() + "）");
        Check(game.State == expectStateAfter, label + "：重复点击不改变状态");
    }

    static void WaitModal(string label, int expectStepAfter, GameStage expectStateAfter)
    {
        GuiliUI ui = GuiliUI.Instance;
        if (!ui.ModalOpen) { wait = 2; return; }
        Say(label + " 已弹出");
        ClickTutorialDone(label, expectStepAfter, expectStateAfter);
        stage++;
        wait = 6;
    }

    /* 没有 modalDone 的档案 / 说明弹窗：点击「记住了」只关闭弹窗 */
    static void ClickModalOnly(string label)
    {
        GuiliUI ui = GuiliUI.Instance;
        if (!ui.ModalOpen) { errors.Add(label + "：点击前弹窗未打开"); return; }
        int stepBefore = CurrentStep();
        GameStage stateBefore = game.State;
        bool doneBefore = ModalDonePending();
        Check(!doneBefore, label + "：该弹窗不应带 modalDone 回调");
        if (!ClickButton(ui.ModalDoneButton, label)) return;
        Check(!ui.ModalOpen, label + "：点击「记住了」后弹窗关闭");
        Check(game.State == stateBefore, label + "：仅关闭弹窗，不改变游戏状态（" + stateBefore + "）");
        Check(CurrentStep() == stepBefore, label + "：仅关闭弹窗，不推进教学步骤");
    }

    /* 关闭叉号：仍能关闭弹窗，且不影响游戏状态 */
    static void ClickModalCloseX(string label)
    {
        GuiliUI ui = GuiliUI.Instance;
        if (!ui.ModalOpen) { errors.Add(label + "：点击前弹窗未打开"); return; }
        int stepBefore = CurrentStep();
        GameStage stateBefore = game.State;
        if (!ClickButton(ui.ModalCloseButton, label)) return;
        Check(!ui.ModalOpen, label + "：点击关闭叉号后弹窗关闭");
        Check(game.State == stateBefore, label + "：关闭叉号不改变游戏状态");
        Check(CurrentStep() == stepBefore, label + "：关闭叉号不推进教学步骤");
    }

    static void BackupCurrency()
    {
        hadCurrency = PlayerPrefs.HasKey(WatermelonCurrencyWallet.WalletPrefKey);
        currencyBackup = PlayerPrefs.GetInt(WatermelonCurrencyWallet.WalletPrefKey, 0);
        hadMigrated = PlayerPrefs.HasKey(WatermelonCurrencyWallet.MigrationPrefKey);
        migratedBackup = PlayerPrefs.GetInt(WatermelonCurrencyWallet.MigrationPrefKey, 0);
        currencyBackupValid = true;
    }

    static void RestoreCurrency()
    {
        if (!currencyBackupValid) return;
        currencyBackupValid = false;
        if (hadCurrency) PlayerPrefs.SetInt(WatermelonCurrencyWallet.WalletPrefKey, currencyBackup);
        else PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.WalletPrefKey);
        if (hadMigrated) PlayerPrefs.SetInt(WatermelonCurrencyWallet.MigrationPrefKey, migratedBackup);
        else PlayerPrefs.DeleteKey(WatermelonCurrencyWallet.MigrationPrefKey);
        PlayerPrefs.Save();
    }

    static void Finish(string failure)
    {
        if (finished) return;
        finished = true;
        trace.AppendLine("");
        if (failure != null) trace.AppendLine("失败: " + failure);
        trace.AppendLine("运行时错误数: " + errors.Count);
        for (int i = 0; i < errors.Count && i < 30; i++) trace.AppendLine("  ! " + errors[i]);
        RestoreCurrency();
        trace.AppendLine("总帧数: " + frames);
        trace.AppendLine("结论: " + (failure == null && errors.Count == 0 && stage >= 22 ? "全流程通过" : "存在异常"));
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath()));
        File.WriteAllText(ResultPath(), trace.ToString(), new UTF8Encoding(false));
        if (File.Exists(MarkerPath())) File.Delete(MarkerPath());
        Debug.Log("[瓜里有数] 冒烟测试结束: " + (failure == null && errors.Count == 0 ? "通过" : "有异常"));
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode)
            EditorApplication.Exit(failure == null && errors.Count == 0 ? 0 : 1);
    }
}
