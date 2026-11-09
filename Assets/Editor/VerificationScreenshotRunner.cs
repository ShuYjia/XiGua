using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/* ============================================================
 * VerificationScreenshotRunner.cs —— 验收截图（仅 Editor）
 * 菜单：Watermelon Science/Capture Verification Screenshots
 * 自动进入 Play Mode，在 1440×900 与 1920×1080 下分别截取：
 *   主首页、模块2（农场入口）、模块4（挑瓜入口）、Farm 玩法、Cut 首页
 * 截图保存到 Logs/VerificationScreenshots/，并记录 Screen 实际尺寸。
 * 不修改 Build Settings、PlayerSettings 或任何游戏状态。
 * ============================================================ */

[InitializeOnLoad]
public static class VerificationScreenshotRunner
{
    const string ShotFolderSuffix = "Logs/VerificationScreenshots";
    const string MarkerSuffix = "Logs/ScreenshotMarker";
    const string ResultSuffix = "Logs/ScreenshotResult.txt";
    const string OriginalIndexPref = "WatermelonScreenshot.OriginalIndex";
    const string RestoreIndexPref = "WatermelonScreenshot.RestoreIndex";

    static readonly List<string> errors = new List<string>();
    static readonly List<string> shots = new List<string>();
    static readonly StringBuilder trace = new StringBuilder();
    static int stage, wait, frames, timeout;
    static bool finished;

    static string ProjectPath(string suffix) { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", suffix)); }
    static string MarkerPath() { return ProjectPath(MarkerSuffix); }
    static string ResultPath() { return ProjectPath(ResultSuffix); }
    static string ShotFolder() { return ProjectPath(ShotFolderSuffix); }

    static VerificationScreenshotRunner()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            if (!msg.Contains("Licensing") && !msg.Contains("abort_threads")) errors.Add(msg);
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        int restore = EditorPrefs.GetInt(RestoreIndexPref, -1);
        if (restore >= 0)
        {
            SetGameViewSizeByIndex(restore);
            EditorPrefs.DeleteKey(RestoreIndexPref);
        }
        EditorPrefs.DeleteKey(OriginalIndexPref);
    }

    [MenuItem("Watermelon Science/Capture Verification Screenshots")]
    public static void Start()
    {
        Directory.CreateDirectory(ShotFolder());
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath()));
        if (File.Exists(ResultPath())) File.Delete(ResultPath());
        errors.Clear();
        shots.Clear();

        int original = GetCurrentGameViewIndex();
        EditorPrefs.SetInt(OriginalIndexPref, original);
        if (!SetGameViewSize(1440, 900))
            errors.Add("无法将 Game View 切换到 1440×900");
        File.WriteAllText(MarkerPath(), "run", new UTF8Encoding(false));
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Say(string s) { trace.AppendLine("  " + s); }
    static void Check(bool ok, string what)
    {
        Say((ok ? "通过 · " : "失败 · ") + what);
        if (!ok) errors.Add(what);
    }

    static string ActiveScene() { return SceneManager.GetActiveScene().name; }
    static ModuleNavigationController Nav() { return UnityEngine.Object.FindObjectOfType<ModuleNavigationController>(); }
    static SubgameSceneRouter Router() { return UnityEngine.Object.FindObjectOfType<SubgameSceneRouter>(); }
    static Button FindButton(string name)
    {
        GameObject go = GameObject.Find(name);
        return go == null ? null : go.GetComponent<Button>();
    }

    static void Shot(string name)
    {
        string path = Path.Combine(ShotFolder(), name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        shots.Add(path);
        Say("截图 " + name + " · Game View " + Screen.width + "×" + Screen.height);
    }

    static void Tick()
    {
        if (finished) return;
        if (!File.Exists(MarkerPath())) return;
        if (!EditorApplication.isPlaying) return;
        if (frames == 0)
        {
            trace.Length = 0;
            trace.AppendLine("西瓜小课堂 · 验收截图");
            trace.AppendLine("Unity " + Application.unityVersion);
        }
        frames++;
        if (++timeout > 20000) { Finish("超时"); return; }
        if (wait > 0) { wait--; return; }

        switch (stage)
        {
            case 0:
            {
                if (Nav() == null || Router() == null) { wait = 2; return; }
                Check(Screen.width == 1440 && Screen.height == 900,
                    "1440×900 Game View（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("01_home_1440x900");
                stage = 1; wait = 10; break;
            }
            case 1:
            {
                ModuleNavigationController nav = Nav();
                if (nav == null) { wait = 2; return; }
                nav.homeModuleButtons[1].onClick.Invoke();
                Check(nav.GetCurrentModuleIndex() == 1, "进入模块2 生长过程");
                Shot("02_module2_1440x900");
                stage = 2; wait = 10; break;
            }
            case 2:
            {
                SubgameSceneRouter router = Router();
                if (router == null) { wait = 2; return; }
                router.farmEntryButton.onClick.Invoke();
                stage = 3; wait = 10; break;
            }
            case 3:
            {
                if (ActiveScene() != "Farm" || UnityEngine.Object.FindObjectOfType<WatermelonFarm.FarmGame>() == null) { wait = 2; return; }
                Check(Screen.width == 1440 && Screen.height == 900,
                    "Farm 1440×900（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("03_farm_1440x900");
                Button back = FindButton("ReturnClassroomBtn");
                Check(back != null, "Farm 返回课堂按钮可见");
                if (back != null) back.onClick.Invoke();
                stage = 4; wait = 10; break;
            }
            case 4:
            {
                ModuleNavigationController nav = Nav();
                if (ActiveScene() != "SampleScene" || nav == null) { wait = 2; return; }
                Check(nav.GetCurrentModuleIndex() == 1, "返回后恢复模块2");
                nav.ShowHome();
                nav.homeModuleButtons[3].onClick.Invoke();
                Check(nav.GetCurrentModuleIndex() == 3, "进入模块4 挑选西瓜");
                Shot("04_module4_1440x900");
                stage = 5; wait = 10; break;
            }
            case 5:
            {
                SubgameSceneRouter router = Router();
                if (router == null) { wait = 2; return; }
                router.cutEntryButton.onClick.Invoke();
                stage = 6; wait = 10; break;
            }
            case 6:
            {
                if (ActiveScene() != "Guayoulishu" || GuiliUI.Instance == null) { wait = 2; return; }
                Check(Screen.width == 1440 && Screen.height == 900,
                    "Cut 1440×900（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("05_cut_home_1440x900");
                Check(FindButton("返回课堂") != null, "Cut 返回课堂入口可见");
                if (GuiliUI.Instance.Game != null) GuiliUI.Instance.Game.SkipToPractice();
                stage = 7; wait = 20; break;
            }
            case 7:
            {
                if (GuiliUI.Instance == null || GuiliUI.Instance.Game == null ||
                    GuiliUI.Instance.Game.State != GameStage.Practice) { wait = 2; return; }
                Check(Screen.width == 1440 && Screen.height == 900,
                    "Cut 玩法 1440×900（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("06_cut_play_1440x900");
                if (!SetGameViewSize(1920, 1080)) errors.Add("无法将 Game View 切换到 1920×1080");
                stage = 8; wait = 20; break;
            }
            case 8:
            {
                Check(Screen.width == 1920 && Screen.height == 1080,
                    "Cut 玩法 1920×1080（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("07_cut_play_1920x1080");
                Button back = FindButton("返回课堂");
                if (back != null) back.onClick.Invoke();
                stage = 9; wait = 10; break;
            }
            case 9:
            {
                ModuleNavigationController nav = Nav();
                if (ActiveScene() != "SampleScene" || nav == null) { wait = 2; return; }
                Check(nav.GetCurrentModuleIndex() == 3, "Cut 返回后恢复模块4");
                nav.ShowHome();
                Check(Screen.width == 1920 && Screen.height == 1080,
                    "首页 1920×1080（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("08_home_1920x1080");
                stage = 10; wait = 10; break;
            }
            case 10:
            {
                ModuleNavigationController nav = Nav();
                if (nav == null) { wait = 2; return; }
                nav.homeModuleButtons[1].onClick.Invoke();
                SubgameSceneRouter router = Router();
                if (router == null) { wait = 2; return; }
                router.farmEntryButton.onClick.Invoke();
                stage = 11; wait = 10; break;
            }
            case 11:
            {
                if (ActiveScene() != "Farm" || UnityEngine.Object.FindObjectOfType<WatermelonFarm.FarmGame>() == null) { wait = 2; return; }
                Check(Screen.width == 1920 && Screen.height == 1080,
                    "Farm 1920×1080（实际 " + Screen.width + "×" + Screen.height + "）");
                Shot("09_farm_1920x1080");
                Button back = FindButton("ReturnClassroomBtn");
                if (back != null) back.onClick.Invoke();
                stage = 12; wait = 10; break;
            }
            case 12:
            {
                ModuleNavigationController nav = Nav();
                if (ActiveScene() != "SampleScene" || nav == null) { wait = 2; return; }
                nav.homeModuleButtons[0].onClick.Invoke();
                Check(nav.GetCurrentModuleIndex() == 0, "模块1 仍可进入");
                nav.homeModuleButtons[2].onClick.Invoke();
                Check(nav.GetCurrentModuleIndex() == 2, "模块3 仍可进入");
                Finish(null);
                break;
            }
        }
    }

    static void Finish(string failure)
    {
        if (finished) return;
        finished = true;
        for (int i = 0; i < shots.Count; i++)
            Say("输出 " + shots[i] + " · " + (File.Exists(shots[i]) ? "已生成" : "缺失"));
        trace.AppendLine("");
        if (failure != null) trace.AppendLine("失败: " + failure);
        trace.AppendLine("运行时错误数: " + errors.Count);
        for (int i = 0; i < errors.Count && i < 30; i++) trace.AppendLine("  ! " + errors[i]);
        trace.AppendLine("结论: " + (failure == null && errors.Count == 0 ? "截图流程通过" : "存在异常"));
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath()));
        File.WriteAllText(ResultPath(), trace.ToString(), new UTF8Encoding(false));
        if (File.Exists(MarkerPath())) File.Delete(MarkerPath());
        int restore = EditorPrefs.GetInt(OriginalIndexPref, -1);
        if (restore >= 0) EditorPrefs.SetInt(RestoreIndexPref, restore);
        Debug.Log("[西瓜小课堂] 验收截图结束: " + (failure == null && errors.Count == 0 ? "通过" : "有异常"));
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
    }

    /* ---------------- Game View 尺寸反射 ---------------- */

    static int GetCurrentGameViewIndex()
    {
        try
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
            SerializedObject so = new SerializedObject(gameView);
            SerializedProperty sizes = so.FindProperty("m_SelectedSizes");
            if (sizes == null || sizes.arraySize == 0) return -1;
            // m_SelectedSizes[0] 对应 Standalone 组
            return sizes.GetArrayElementAtIndex(0).intValue;
        }
        catch (Exception e) { Debug.LogWarning("读取 Game View 尺寸失败: " + e.Message); return -1; }
    }

    static bool SetGameViewSizeByIndex(int index)
    {
        try
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
            SerializedObject so = new SerializedObject(gameView);
            SerializedProperty sizes = so.FindProperty("m_SelectedSizes");
            if (sizes == null || sizes.arraySize == 0) return false;
            sizes.GetArrayElementAtIndex(0).intValue = index;
            so.ApplyModifiedProperties();
            gameView.Repaint();
            return true;
        }
        catch (Exception e) { Debug.LogWarning("恢复 Game View 尺寸失败: " + e.Message); return false; }
    }

    static bool SetGameViewSize(int width, int height)
    {
        try
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            Type sizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            Type singletonType = Type.GetType("UnityEditor.ScriptableSingleton`1,UnityEditor");
            Type sizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
            Type sizeEnumType = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
            Type groupEnumType = Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor");
            if (gameViewType == null || sizesType == null || singletonType == null || sizeType == null ||
                sizeEnumType == null || groupEnumType == null)
                return false;

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "Game", false);
            PropertyInfo instanceProp = singletonType.MakeGenericType(sizesType).GetProperty(
                "instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            object sizes = instanceProp.GetValue(null);
            // GetCurrentGroup() 在部分编辑器状态下会返回 null；Standalone 组始终可用
            object standalone = Enum.Parse(groupEnumType, "Standalone");
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.Instance)
                                     .Invoke(sizes, new object[] { standalone });
            if (group == null) { Debug.LogWarning("无法获取 Standalone GameViewSizeGroup"); return false; }

            MethodInfo getCount = group.GetType().GetMethod("GetTotalCount");
            MethodInfo getSize = group.GetType().GetMethod("GetGameViewSize");
            MethodInfo indexOf = group.GetType().GetMethod("IndexOf");
            MethodInfo addSize = group.GetType().GetMethod("AddCustomSize");

            int count = (int)getCount.Invoke(group, null);
            int found = -1;
            for (int i = 0; i < count; i++)
            {
                object s = getSize.Invoke(group, new object[] { i });
                int w = (int)s.GetType().GetProperty("width").GetValue(s);
                int h = (int)s.GetType().GetProperty("height").GetValue(s);
                if (w == width && h == height) { found = i; break; }
            }
            if (found < 0)
            {
                object fixedType = Enum.Parse(sizeEnumType, "FixedResolution");
                ConstructorInfo ctor = sizeType.GetConstructor(new Type[] { sizeEnumType, typeof(int), typeof(int), typeof(string) });
                object newSize = ctor.Invoke(new object[] { fixedType, width, height, width + "x" + height });
                addSize.Invoke(group, new object[] { newSize });
                found = (int)indexOf.Invoke(group, new object[] { newSize });
            }

            SerializedObject so = new SerializedObject(gameView);
            SerializedProperty selectedSizes = so.FindProperty("m_SelectedSizes");
            if (selectedSizes == null || selectedSizes.arraySize == 0)
            {
                Debug.LogWarning("GameView 缺少 m_SelectedSizes 序列化字段");
                return false;
            }
            // m_SelectedSizes[0] 对应 Standalone 组
            selectedSizes.GetArrayElementAtIndex(0).intValue = found;
            so.ApplyModifiedProperties();
            gameView.Repaint();
            Debug.Log("GameView 已切换到 " + width + "×" + height + "（索引 " + found + "）");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("设置 Game View 尺寸失败: " + e);
            return false;
        }
    }
}
