using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/* ============================================================
 * GuiliGame.cs —— 总控与完整状态机
 * 对应 js/atelier-game.js 的 Game 模块（逐行等价移植）：
 *   home → tutorial(四步) → practice(九瓜自由观察) → challenge(选择+依据)
 *        → cut(剖切动画) → result(评分) → retry
 *   包含：随机重排与签名去重、五项观察记录、翻底保持、瓜柄近观、
 *   轻叩合成音、生长档案弹窗、评分 45/35/20、结果与称号。
 * 采用运行时自举：场景只需存在，全部 UI / 3D / 音频由本脚本构建，
 * 因此任意 Unity 场景打开后按 Play 即可运行。
 * ============================================================ */

public enum GameStage { Home, Tutorial, Practice, Challenge, Cut, Result }

public class GuiliGame : MonoBehaviour
{
    class Anim
    {
        public float t;
        public Action<float> update;
        public Action done;
    }

    public const string CutSceneName = "Guayoulishu";

    // 只有活动场景是 Guayoulishu 时才创建切瓜总控；主工程 SampleScene 不会触发
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != CutSceneName) return;
        if (FindObjectOfType<GuiliGame>() != null) return;
        GameObject go = new GameObject("瓜里有数 · 总控");
        go.AddComponent<GuiliGame>();
    }

    public GameStage State = GameStage.Home;
    GuiliUI ui;
    GuiliScene scene;
    readonly List<MelonInfo> infos = new List<MelonInfo>();
    readonly List<GuiliMelon> melons = new List<GuiliMelon>();
    readonly List<Texture2D> thumbs = new List<Texture2D>();
    readonly List<Anim> anims = new List<Anim>();
    readonly List<string> reasons = new List<string>();
    GuiliMelon tutorial, active;
    int step, round;
    string previous = "", chosen;
    bool orbitMode, follow = true;
    Coroutine cutRoutine;
    System.Random rnd;

    /* 供验证脚本读取：本轮完整随机签名、第几篮、固定编号下九颗瓜的排列。
       编号 01~09 与位置永久固定；CatalogOrder 记录的是「每个号码上是哪一颗瓜」，
       同一篮内不变（保证观察与选择指的是同一颗瓜），换一篮必变。 */
    public string RoundSignature { get; private set; }
    public string CatalogOrder { get; private set; }
    public int RoundNumber { get { return round; } }
    public List<MelonInfo> Infos { get { return infos; } }

    void Start()
    {
        /* 只在切瓜场景内清掉场景自带相机与灯光，全部由 guili 场景控制器重建；
           不常驻、不触碰其他场景的相机或灯光 */
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != CutSceneName)
        {
            enabled = false;
            return;
        }
        Camera[] cams = FindObjectsOfType<Camera>();
        for (int i = 0; i < cams.Length; i++) Destroy(cams[i].gameObject);
        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++) Destroy(lights[i].gameObject);
        if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        rnd = new System.Random(unchecked((int)DateTime.Now.Ticks));
        gameObject.AddComponent<GuiliAudio>();
        ui = gameObject.AddComponent<GuiliUI>();
        ui.Game = this;
        ui.Build();
        scene = gameObject.AddComponent<GuiliScene>();
        scene.Init(ui.PlayViewport);
        ui.Scene = scene;
        scene.OnDragRotate = Rotate;
        scene.OnMelonClick = Observe;
        scene.SetFollow(follow);
        scene.SetOrbitMode(orbitMode);
        GoHome();
    }

    void Update()
    {
        for (int i = anims.Count - 1; i >= 0; i--)
        {
            Anim a = anims[i];
            a.t += Time.deltaTime / .65f;
            float k = Mathf.Min(a.t, 1f);
            if (a.update != null) a.update(k);
            if (k >= 1f)
            {
                anims.RemoveAt(i);
                if (a.done != null) a.done();
            }
        }
        if (ui != null) ui.UpdateLabels(melons, State == GameStage.Practice && active == null && !ui.ModalOpen);
    }

    /* ---------------- 阶段与面板 ---------------- */
    void SetState(GameStage s)
    {
        State = s;
        ui.SetStage((int)s - 1);
        scene.Interactive = s == GameStage.Tutorial || s == GameStage.Practice;
        /* 网页 canZoom() 只在 practice 阶段允许滚轮/双击缩放 */
        scene.ZoomEnabled = s == GameStage.Practice;
    }
    void HideSheets() { ui.HideAll(); }

    public void GoHome()
    {
        anims.Clear();
        if (cutRoutine != null) { StopCoroutine(cutRoutine); cutRoutine = null; }
        ui.CloseModal(true);
        HideSheets();
        ui.ShowHome();
        SetState(GameStage.Home);
        active = null;
        scene.Active = null;
        scene.SetRenderEnabled(false);
        for (int i = 0; i < melons.Count; i++) melons[i].group.SetActive(false);
        if (tutorial != null) tutorial.group.SetActive(false);
        ui.UpdateLabels(melons, false);
    }

    /* 对应 newRound()，按用户要求改为「编号固定、西瓜随机」：
       3x3 槽位恒为 0..8（01 永远在左上角、09 永远在右下角），
       每轮只把九颗西瓜（档案/瓜底/音色/外观）洗牌到这些固定编号上，
       并做签名去重，保证相邻两篮每个号码上的瓜都不同。 */
    void NewRound()
    {
        rewardGrantedForRound = false;   // 每局只发放一次西瓜币评分奖励
        anims.Clear();
        active = null;
        chosen = null;
        reasons.Clear();
        for (int i = 0; i < melons.Count; i++) scene.Remove(melons[i]);
        melons.Clear();
        infos.Clear();
        thumbs.Clear();

        string sig = "";
        int[] slots = new int[9];
        for (int i = 0; i < 9; i++) slots[i] = i;   /* 位置固定，不洗牌 */
        for (int tries = 0; tries < 20; tries++)
        {
            infos.Clear();
            GuiliData.AssignProfiles(infos, slots, rnd, out sig);
            if (sig != previous) break;
        }
        previous = sig;
        RoundSignature = sig;
        /* 固定编号下的「西瓜排列」，用于验证记号码/记位置无效 */
        string arr = "";
        for (int i = 0; i < infos.Count; i++)
            arr += (i == 0 ? "" : ",") + infos[i].id + "=" +
                   GuiliData.GradeKey(infos[i].spotGrade) + "/" + infos[i].cut + "/" + infos[i].knock;
        CatalogOrder = arr;
        round++;
        for (int i = 0; i < infos.Count; i++)
        {
            MelonInfo info = infos[i];
            float radius = .45f + UnityEngine.Random.value * .045f;
            GuiliMelon m = scene.AddMelon(info, radius, info.home);
            m.Reset();
            melons.Add(m);
            thumbs.Add(GuiliTextures.MakeThumbnail(m.skin));
        }
    }

    /* ---------------- 卷一 · 教学四步 ---------------- */
    public void StartTutorial()
    {
        GuiliAudio.Unlock();
        if (tutorial == null)
            tutorial = scene.AddMelon(GuiliData.TutorialMelon(), .5f, new Vector3(0, 1, 0));
        tutorial.Reset();
        tutorial.group.SetActive(true);
        for (int i = 0; i < melons.Count; i++) melons[i].group.SetActive(false);
        HideSheets();
        ui.ShowPlay();
        SetState(GameStage.Tutorial);
        scene.SetRenderEnabled(true);
        scene.SetViewport(ui.PlayViewport);
        active = tutorial;
        scene.Active = tutorial;
        step = 0;
        scene.GoFocus(tutorial);
        ui.SetPlayPanelsVisible(true);
        ui.SetViewControlsVisible(true);
        ui.SetViewControls(orbitMode, follow);
        ShowStep();
    }

    void ShowStep()
    {
        ui.SetJournal(GuiliData.ChapterTutorial, GuiliData.StepTitles[step], GuiliData.StepHints[step]);
        ui.SetKnowledge(GuiliData.TutorialKnowledge);
        ui.SetTutorialMode(true, step);
        ui.SetNext("", false);
        ui.SetCaption(GuiliData.CaptionDefault);
        RenderTools();
    }

    /* ---------------- 卷二 · 九瓜辨识 ---------------- */
    public void SkipToPractice() { GuiliAudio.Unlock(); NewRound(); Practice(); }

    void Practice()
    {
        HideSheets();
        ui.ShowPlay();
        SetState(GameStage.Practice);
        scene.SetRenderEnabled(true);
        scene.SetViewport(ui.PlayViewport);
        if (tutorial != null) tutorial.group.SetActive(false);
        active = null;
        scene.Active = null;
        for (int i = 0; i < melons.Count; i++)
        {
            melons[i].group.SetActive(true);
            melons[i].group.transform.position = melons[i].home;
        }
        scene.GoOverview();
        ui.SetJournal(GuiliData.ChapterPractice, GuiliData.PracticeTask, GuiliData.PracticeHint);
        ui.SetKnowledge(GuiliData.PracticeKnowledge);
        ui.SetCaption(GuiliData.PracticeCaption);
        ui.SetTutorialMode(false, 0);
        ui.SetPlayPanelsVisible(true);
        ui.SetViewControlsVisible(true);
        ui.SetViewControls(orbitMode, follow);
        Ledger();
        RenderTools();
        ui.SetNext(GuiliData.NextWithoutActive, true);
    }

    void Ledger()
    {
        if (State != GameStage.Practice) return;
        string activeId = active != null && active.info != null ? active.info.id : null;
        ui.SetLedger(infos, activeId, round);
    }

    void RenderTools()
    {
        if (active == null) { ui.SetTools(null, null, false); return; }
        string[] allowed = State == GameStage.Tutorial ? new string[] { GuiliData.StepKeys[step] } : GuiliData.Keys;
        ui.SetTools(allowed, active.info, State == GameStage.Practice);
    }

    public void LedgerClick(string id)
    {
        GuiliMelon m = FindMelon(id);
        if (m != null) Observe(m);
    }

    GuiliMelon FindMelon(string id)
    {
        for (int i = 0; i < melons.Count; i++) if (melons[i].info.id == id) return melons[i];
        return null;
    }

    void Observe(GuiliMelon m)
    {
        if (State != GameStage.Practice) return;
        active = m;
        scene.Active = m;
        for (int i = 0; i < melons.Count; i++) melons[i].group.SetActive(melons[i] == m);
        m.group.transform.position = new Vector3(0, 1, 0);
        scene.GoFocus(m);
        ui.SetJournal(GuiliData.ChapterPractice, m.info.label, GuiliData.ObserveHint);
        ui.SetKnowledge(m.info.appearance + GuiliData.ObserveKnowledgeTail);
        ui.SetCaption(m.target != 0 ? GuiliData.CaptionBody : GuiliData.CaptionSkin);
        Ledger();
        RenderTools();
        ui.SetNext(GuiliData.NextWithActive, true);
    }

    void Mark(string key)
    {
        if (active == null || active.info == null) return;
        int idx = Array.IndexOf(GuiliData.Keys, key);
        if (idx < 0) return;
        active.info.observed[idx] = true;
        if (State == GameStage.Tutorial)
        {
            if (step < 4 && GuiliData.StepKeys[step] == key && !ui.ModalOpen)
            {
                int s = step;
                ui.ShowModal(GuiliData.StepTitles[s], GuiliData.StepKnowledge[s], null, () =>
                {
                    step++;
                    if (step < 4) ShowStep();
                    else { NewRound(); Practice(); }
                });
            }
        }
        else { Ledger(); RenderTools(); }
    }

    /* ---------------- 交互动作 ---------------- */
    void Rotate(GuiliMelon m, float dx, float dy)
    {
        if (active == null || m != active || m.busy || ui.ModalOpen) return;
        m.yaw += dx * .012f;
        m.tilt = Mathf.Clamp(m.tilt + dy * .003f, -.22f, .22f);
        m.ApplyPose();
        m.rotated += Mathf.Abs(dx) * .012f;
        if (m.rotated > .9f) Mark("skin");
    }

    void Turn(GuiliMelon m, float targetValue, Action done)
    {
        if (m.busy) return;
        m.target = targetValue;
        m.busy = true;
        m.tilt = 0;
        float from = m.flip;
        anims.Add(new Anim
        {
            update = k =>
            {
                float s = k * k * (3 - 2 * k);
                m.flip = from + (targetValue - from) * s;
                m.ApplyPose();
            },
            done = () => { m.busy = false; if (done != null) done(); }
        });
    }

    public void Tool(string key)
    {
        GuiliMelon m = active;
        if (m == null || (State != GameStage.Tutorial && State != GameStage.Practice)) return;
        GuiliAudio.Unlock();
        if (key == "back") { Practice(); return; }
        if (key == "skin")
        {
            Rotate(m, 95f, 0);
            ui.SetKnowledge(m.info.appearance + GuiliData.SkinTail);
        }
        else if (key == "spot")
        {
            float t = m.target == 0 ? -Mathf.PI : 0f;
            Turn(m, t, () =>
            {
                Mark("spot");
                /* 三档地斑的判读，与模型颜色、切瓜结果、评分一一对应 */
                ui.SetKnowledge(GuiliData.SpotReading(m.info));
                ui.SetCaption(t != 0 ? GuiliData.CaptionAfterSpot : GuiliData.CaptionAfterSpotBack);
            });
        }
        else if (key == "stem")
        {
            Turn(m, 0, () =>
            {
                scene.GoStemClose(m);
                Mark("stem");
                ui.SetKnowledge(m.info.stem + GuiliData.StemTail);
                ui.SetCaption(GuiliData.CaptionStem);
            });
        }
        else if (key == "sound")
        {
            /* 音色由切面结果唯一决定：清脆/略闷/沉闷/浑浊 ↔ 四类结果 */
            GuiliAudio.Tap(m.info.knock, m.info.profileSeed);
            ui.Wave(m.info.knock);
            Mark("sound");
            ui.SetKnowledge(GuiliData.KnockReading(m.info));
        }
        else if (key == "record")
        {
            Mark("record");
            ui.ShowArchive(m.info, null);
        }
    }

    public void ViewTool(int which)
    {
        if (which == 0)
        {
            orbitMode = !orbitMode;
            scene.SetOrbitMode(orbitMode);
        }
        else if (which == 1) scene.ResetView();
        else if (which == 2)
        {
            follow = !follow;
            scene.SetFollow(follow);
        }
        ui.SetViewControls(orbitMode, follow);
    }

    /* ---------------- 卷三 · 独立挑瓜 ---------------- */
    public void Next() { Challenge(); }

    void Challenge()
    {
        if (State != GameStage.Practice) return;
        if (active != null) chosen = active.info.id;
        SetState(GameStage.Challenge);
        active = null;
        scene.Active = null;
        scene.SetRenderEnabled(false);
        ui.ShowChallenge();

        /* 编号 01~09 固定显示在图录的固定格子里，和瓜摊、ledger 完全一致；
           随机的是「每个号码上放着哪一颗西瓜」，且每换一篮都重新洗牌。
           因此不洗牌图录：玩家看到的编号与在瓜摊里观察过的那颗瓜始终对应，
           但靠「好瓜是几号 / 在第几格」的记忆完全无效。 */
        ui.SetChallenge(infos, thumbs, chosen, reasons);
    }

    public void Choose(string id)
    {
        chosen = id;
        ui.SetChoiceSelected(id);
    }

    public void ToggleReason(string key)
    {
        ui.ToggleReasonUI(key);
        reasons.Clear();
        reasons.AddRange(ui.GetReasons());
    }

    public void ObserveAgain()
    {
        reasons.Clear();
        reasons.AddRange(ui.GetReasons());
        Practice();
    }

    /* ---------------- 卷四 · 切瓜求证 ---------------- */
    public void Submit()
    {
        reasons.Clear();
        reasons.AddRange(ui.GetReasons());
        if (chosen == null) { ui.SetWarn(GuiliData.WarnNoChoice); return; }
        if (reasons.Count < 2) { ui.SetWarn(GuiliData.WarnFewReasons); return; }
        GuiliMelon m = FindMelon(chosen);
        if (m == null) return;

        HideSheets();
        ui.ShowCutStage();
        SetState(GameStage.Cut);
        scene.SetRenderEnabled(true);
        scene.SetViewport(ui.CutViewport);
        active = null;
        scene.Active = null;
        for (int i = 0; i < melons.Count; i++) melons[i].group.SetActive(melons[i] == m);
        m.group.transform.position = new Vector3(0, 1, 0);
        m.ResetPose();
        scene.GoCut();
        ui.SetCutCard(false);
        ui.SetCutCaption(GuiliData.CutCaptionBefore);
        if (cutRoutine != null) StopCoroutine(cutRoutine);
        cutRoutine = StartCoroutine(CutSequence(m));
    }

    IEnumerator CutSequence(GuiliMelon m)
    {
        /* 与网页 atelier-game.js submit() 一致：350ms 后落刀
           → 整瓜隐去、剖面出现 → 0.65s 两半分离 → 动画结束才显示切瓜求证卡 */
        yield return new WaitForSeconds(.35f);
        GuiliAudio.Cut();
        ui.PlayKnife();
        m.ShowCut(true);
        m.halfL.transform.localPosition = Vector3.zero;
        m.halfR.transform.localPosition = Vector3.zero;
        m.halfL.transform.localRotation = Quaternion.identity;
        m.halfR.transform.localRotation = Quaternion.identity;
        anims.Add(new Anim
        {
            update = k =>
            {
                /* smoothstep 缓动，让「剖开」这个动作肉眼可辨 */
                float s = k * k * (3f - 2f * k);
                m.halfL.transform.localPosition = new Vector3(-.62f * s, 0, 0);
                m.halfR.transform.localPosition = new Vector3(.62f * s, 0, 0);
                m.halfL.transform.localRotation = Quaternion.Euler(0, -90f * s, 0);
                m.halfR.transform.localRotation = Quaternion.Euler(0, 90f * s, 0);
                m.Support();
            },
            done = () =>
            {
                int idx = GuiliData.OutcomeIndex(m.info.cut);
                string evidence = reasons.Contains("stripe") ? GuiliData.CutEvidenceStripe : GuiliData.CutEvidenceNormal;
                ui.SetCut(m.info.label, GuiliData.Outcomes[idx, 0], GuiliData.Outcomes[idx, 1], evidence);
                ui.SetCutCard(true);
                ui.SetCutCaption(GuiliData.CutCaptionAfter);
            }
        });
        cutRoutine = null;
    }

    /* ---------------- 卷五 · 瓜乡结业笺 ---------------- */
    bool rewardGrantedForRound;

    public void ResultButton() { Result(); }

    void Result()
    {
        if (State != GameStage.Cut || !ui.CutVisible) return;
        SetState(GameStage.Result);
        ui.ShowResult();
        scene.SetRenderEnabled(false);
        GuiliMelon m = FindMelon(chosen);
        if (m == null) return;
        int[] parts = GuiliData.Score(m.info, reasons);
        int total = parts[0] + parts[1] + parts[2];
        int reward = total;
        if (!rewardGrantedForRound)
        {
            WatermelonCurrencyWallet.Credit(reward, "cut_score_reward");
            rewardGrantedForRound = true;
        }
        ui.SetResult(total, GuiliData.Rank(total), parts,
                     GuiliData.ResultMessage(infos, m.info),
                     reward, WatermelonCurrencyWallet.GetBalance());
    }

    public void Retry() { GuiliAudio.Unlock(); NewRound(); Practice(); }

    /* ---------------- 弹窗 ---------------- */
    public void About() { ui.ShowAbout(null); }
    public void Settings() { ui.ShowSettings(follow, null); }
}
