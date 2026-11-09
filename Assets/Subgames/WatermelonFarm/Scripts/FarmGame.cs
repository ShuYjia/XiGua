using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WatermelonFarm
{
    // 挂载后即可运行；即使场景里没有任何预设物体，也会自动接管整个游戏
    public sealed class FarmGame : MonoBehaviour
    {
        [NonSerialized] public FarmModel Model;
        FarmUI ui;
        float flushTimer = 0f;
        bool escDisabled = false;

        // 设计基准 16:9；CanvasScaler 用 Expand，16:9 屏幕的逻辑画布恒为 1920×1080
        public const float REF_W = 1920f;
        public const float REF_H = 1080f;
        float lastW = 0f, lastH = 0f;

        // 与 CanvasScaler(ScaleWithScreenSize + Expand) 一致地推算逻辑画布尺寸
        public static void LogicalSize(out float w, out float h)
        {
            int sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            float s = Mathf.Min(sw / REF_W, sh / REF_H);
            if (s <= 0.0001f) s = 1f;
            w = sw / s;
            h = sh / s;
        }

        // 只有活动场景是 Farm 时才自启动；从主工程 SampleScene 进入时绝不创建农场
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != FarmSceneName) return;
            if (UnityEngine.Object.FindObjectOfType<FarmGame>() != null) return;
            GameObject go = new GameObject("FarmGame (Auto)");
            go.AddComponent<FarmGame>();
        }

        public const string FarmSceneName = "Farm";

        void Awake()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != FarmSceneName)
            {
                Debug.LogWarning("FarmGame 只能在 Farm 场景运行，已自动停用。");
                enabled = false;
                return;
            }

            EnsureEventSystem();
            EnsureCamera();

            GameObject canvasGO = new GameObject("FarmCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas cv = canvasGO.GetComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 10;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            WatermelonResolution.EnsureCanvasScaler(scaler);

            // 非 16:9 留白背景 + 固定 1920x1080 内容根节点；缩放只由 CanvasScaler 做一次
            WatermelonResolution.EnsureBackground(canvasGO.transform, new Color(0.95f, 0.97f, 0.91f, 1f));
            RectTransform contentRoot = WatermelonResolution.EnsureContentRoot(canvasGO.transform);

            float w = WatermelonResolution.DesignWidth;
            float h = WatermelonResolution.DesignHeight;
            lastW = w; lastH = h;

            ui = new FarmUI();
            Model = new FarmModel();
            Model.view = ui;
            ui.M = Model;
            ui.Build(contentRoot, w, h);
            ui.onReturnClassroom = ReturnToClassroom;

            // 与主课堂 / Cut 一样订阅统一钱包事件，兑换或消费后顶栏余额即时刷新
            WatermelonCurrencyHUD hud = canvasGO.AddComponent<WatermelonCurrencyHUD>();
            hud.balanceText = ui.BalanceText;
            hud.panelParent = contentRoot;
            hud.Refresh();

            Model.Init();
        }

        void ReturnToClassroom()
        {
            SubgameSceneRouter.ReturnToClassroom();
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(null, false);
        }

        void EnsureCamera()
        {
            if (Camera.main != null) return;
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            Camera c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.49f, 0.78f, 0.89f, 1f);
            c.orthographic = true;
            c.orthographicSize = 5f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (ui == null || Model == null || Model.P == null) return;

            // 内容根节点固定 1920x1080，窗口比例变化由 CanvasScaler 统一处理，无需重新排版
            ui.TickVisual(dt);
            // 田间告警 10 秒生死线用真实时间计时，独立于生长 tick 与倍速
            Model.TickDanger(dt);

            // 与网页版一致：每积累 1 秒（乘以倍速）执行 1 次 tick，单次最多补 12 tick
            Model.acc += dt * Model.P.speed;
            int n = 0;
            while (Model.acc >= 1f && n < 12)
            {
                Model.acc -= 1f;
                n++;
                Model.TickOnce();
            }
            if (n > 0)
            {
                ui.Render();
                Model.Save();
                flushTimer += dt;
                if (flushTimer >= 5f) { flushTimer = 0f; Model.Save(true); }
            }

            if (!escDisabled)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) Model.CloseTopModal();
                }
                catch (InvalidOperationException)
                {
                    // 工程启用了「仅新输入系统」时，旧 Input 不可用；弹窗仍可用 × / 点遮罩关闭
                    escDisabled = true;
                }
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Model != null) Model.Save(true);
            if (paused && ui != null) ui.ReleaseCursor();
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus && Model != null) Model.Save(true);
            if (!focus && ui != null) ui.ReleaseCursor();
        }
        void OnApplicationQuit() { if (Model != null) Model.Save(true); if (ui != null) ui.ReleaseCursor(); }
        // 退出 Play 时还原系统箭头，避免编辑器里留下隐藏的鼠标指针
        void OnDestroy() { if (ui != null) ui.ReleaseCursor(); }

        // 只重置农场进度；西瓜币存在统一钱包里，这里刻意不动它
        [ContextMenu("重置存档（清空进度，保留西瓜币）")]
        public void ResetSave()
        {
            PlayerPrefs.DeleteKey(FarmModel.SAVE_KEY);
            PlayerPrefs.Save();
            if (Model != null)
            {
                Model.P = SaveData.NewGame();
                Model.Log("sys", "存档已重置，重新开始种瓜吧！");
                Model.Changed();
            }
        }
    }
}
