using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/* ============================================================
 * ClassroomRouteSmokeTest.cs —— 主工程场景路由的运行时冒烟测试（仅 Editor）
 * 菜单：Watermelon Science/Run Classroom Route Smoke Test
 * 自动打开 SampleScene 并进入 Play Mode：
 *   首页 → 模块2 → 瓜田农场 → 返回课堂（恢复模块2）
 *        → 首页 → 模块4 → 挑瓜 → 返回课堂（恢复模块4）
 * 并检查切场后没有残留总控 / 重复 EventSystem / 丢失相机。
 * 结果写入 Logs/RouteSmokeResult.txt。
 * ============================================================ */

[InitializeOnLoad]
public static class ClassroomRouteSmokeTest
{
    static readonly List<string> errors = new List<string>();
    static readonly StringBuilder trace = new StringBuilder();
    static int stage, wait, frames, timeout;
    static bool finished;

    static string MarkerPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "RouteSmokeMarker")); }
    static string ResultPath() { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "RouteSmokeResult.txt")); }

    static ClassroomRouteSmokeTest()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            if (!msg.Contains("Licensing") && !msg.Contains("abort_threads")) errors.Add(msg);
    }

    [MenuItem("Watermelon Science/Run Classroom Route Smoke Test")]
    public static void Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MarkerPath()));
        File.WriteAllText(MarkerPath(), "run", new UTF8Encoding(false));
        if (File.Exists(ResultPath())) File.Delete(ResultPath());
        errors.Clear();
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    static void Say(string s) { trace.AppendLine("  " + s); }

    static void Check(bool ok, string what)
    {
        Say((ok ? "通过 · " : "失败 · ") + what);
        if (!ok) errors.Add(what);
    }

    static ModuleNavigationController Nav()
    {
        return UnityEngine.Object.FindObjectOfType<ModuleNavigationController>();
    }

    static SubgameSceneRouter Router()
    {
        return UnityEngine.Object.FindObjectOfType<SubgameSceneRouter>();
    }

    static string ActiveScene()
    {
        return SceneManager.GetActiveScene().name;
    }

    static Button FindButton(string name)
    {
        GameObject go = GameObject.Find(name);
        return go == null ? null : go.GetComponent<Button>();
    }

    static void Tick()
    {
        if (finished) return;
        if (!File.Exists(MarkerPath())) return;
        if (!EditorApplication.isPlaying) return;
        if (frames == 0)
        {
            trace.Length = 0;
            trace.AppendLine("西瓜小课堂 · 场景路由运行时冒烟测试");
            trace.AppendLine("Unity " + Application.unityVersion);
        }
        frames++;
        if (++timeout > 20000) { Finish("超时"); return; }
        if (wait > 0) { wait--; return; }

        switch (stage)
        {
            case 0:
            {
                ModuleNavigationController nav = Nav();
                SubgameSceneRouter router = Router();
                Check(nav != null && router != null, "SampleScene 中找到导航与场景路由");
                Check(nav != null && nav.homePanel != null && nav.homePanel.activeSelf, "冷启动显示主首页");
                Check(nav != null && nav.homeModuleButtons != null && nav.homeModuleButtons.Length == 4,
                    "首页仍是四个主题入口");
                Check(router != null && router.farmEntryButton != null && router.cutEntryButton != null,
                    "路由显式引用农场 / 挑瓜入口按钮");

                // 原有模块1、模块3仍可正常切换
                if (nav != null)
                {
                    nav.homeModuleButtons[0].onClick.Invoke();
                    Check(nav.GetCurrentModuleIndex() == 0, "首页 → 模块1 认识西瓜");
                    nav.homeModuleButtons[2].onClick.Invoke();
                    Check(nav.GetCurrentModuleIndex() == 2, "首页 → 模块3 营养知识");
                    nav.ShowHome();
                }

                if (nav != null && router != null)
                {
                    nav.homeModuleButtons[1].onClick.Invoke();
                    Check(nav.GetCurrentModuleIndex() == 1 && nav.modulePanels[1].activeSelf,
                        "首页 → 模块2 生长过程，原四阶段面板仍在");
                    router.farmEntryButton.onClick.Invoke();
                }
                stage = 1; wait = 2;
                break;
            }
            case 1:
            {
                if (ActiveScene() != "Farm") { wait = 2; return; }
                if (UnityEngine.Object.FindObjectOfType<WatermelonFarm.FarmGame>() == null) { wait = 2; return; }
                Check(UnityEngine.Object.FindObjectsOfType<WatermelonFarm.FarmGame>().Length == 1,
                    "进入 Farm 后只有一个农场总控");
                Check(UnityEngine.Object.FindObjectsOfType<GuiliGame>().Length == 0, "Farm 场景没有切瓜总控");
                Check(UnityEngine.Object.FindObjectsOfType<SubgameSceneRouter>().Length == 0,
                    "进入 Farm 后主工程路由对象已随场景卸载");
                Check(GameObject.Find("WatermelonClassroom") == null, "Farm 场景没有残留主课堂 Canvas");
                Check(Camera.main != null, "Farm 场景有可用主相机");
                Check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length <= 1,
                    "Farm 场景没有重复 EventSystem");
                Check(GameObject.Find("ReturnClassroomBtn") != null, "Farm 顶栏存在“返回课堂”按钮");

                Button back = FindButton("ReturnClassroomBtn");
                Check(back != null, "Farm“返回课堂”按钮可用");
                if (back != null) back.onClick.Invoke();
                stage = 2; wait = 2;
                break;
            }
            case 2:
            {
                if (ActiveScene() != "SampleScene") { wait = 2; return; }
                ModuleNavigationController nav = Nav();
                if (nav == null) { wait = 2; return; }
                Check(nav.GetCurrentModuleIndex() == 1 && nav.modulePanels[1].activeSelf,
                    "Farm 返回课堂后恢复到模块2 生长过程");
                Check(nav.homePanel != null && !nav.homePanel.activeSelf, "返回后没有错误回到首页");

                // 首页 → 模块4 → 挑瓜
                nav.ShowHome();
                nav.homeModuleButtons[3].onClick.Invoke();
                Check(nav.GetCurrentModuleIndex() == 3 && nav.modulePanels[3].activeSelf,
                    "首页 → 模块4 挑选西瓜，显示“开始挑瓜”入口");
                SubgameSceneRouter router = Router();
                Check(router != null && router.cutEntryButton != null, "模块4 路由引用仍有效");
                if (router != null && router.cutEntryButton != null) router.cutEntryButton.onClick.Invoke();
                stage = 3; wait = 2;
                break;
            }
            case 3:
            {
                if (ActiveScene() != "Guayoulishu") { wait = 2; return; }
                if (UnityEngine.Object.FindObjectOfType<GuiliGame>() == null || GuiliUI.Instance == null) { wait = 2; return; }
                Check(UnityEngine.Object.FindObjectsOfType<GuiliGame>().Length == 1, "进入切瓜场景后只有一个切瓜总控");
                Check(UnityEngine.Object.FindObjectsOfType<WatermelonFarm.FarmGame>().Length == 0, "切瓜场景没有农场总控");
                Check(UnityEngine.Object.FindObjectsOfType<SubgameSceneRouter>().Length == 0,
                    "进入切瓜后主工程路由对象已随场景卸载");
                Check(GameObject.Find("WatermelonClassroom") == null, "切瓜场景没有残留主课堂 Canvas");
                Check(UnityEngine.Object.FindObjectsOfType<Camera>().Length >= 1, "切瓜场景有可用相机");
                Check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length <= 1,
                    "切瓜场景没有重复 EventSystem");
                Check(GameObject.Find("返回课堂") != null, "切瓜顶栏存在“返回课堂”入口");

                Button back = FindButton("返回课堂");
                Check(back != null, "切瓜“返回课堂”入口可用");
                if (back != null) back.onClick.Invoke();
                stage = 4; wait = 2;
                break;
            }
            case 4:
            {
                if (ActiveScene() != "SampleScene") { wait = 2; return; }
                ModuleNavigationController nav = Nav();
                if (nav == null) { wait = 2; return; }
                Check(nav.GetCurrentModuleIndex() == 3 && nav.modulePanels[3].activeSelf,
                    "切瓜返回课堂后恢复到模块4 挑选西瓜");
                Check(nav.homePanel != null && !nav.homePanel.activeSelf, "返回后没有错误回到首页");
                Check(UnityEngine.Object.FindObjectsOfType<GuiliGame>().Length == 0, "返回后无 DontDestroyOnLoad 残留切瓜总控");
                Check(UnityEngine.Object.FindObjectsOfType<WatermelonFarm.FarmGame>().Length == 0, "返回后无残留农场总控");
                Check(Camera.main != null, "返回后主相机完好");
                Check(UnityEngine.Object.FindObjectsOfType<UnityEngine.EventSystems.EventSystem>().Length == 1,
                    "返回后 EventSystem 只有一个");
                Finish(null);
                break;
            }
        }
    }

    static void Finish(string failure)
    {
        if (finished) return;
        finished = true;
        trace.AppendLine("");
        if (failure != null) trace.AppendLine("失败: " + failure);
        trace.AppendLine("运行时错误数: " + errors.Count);
        for (int i = 0; i < errors.Count && i < 30; i++) trace.AppendLine("  ! " + errors[i]);
        trace.AppendLine("总帧数: " + frames);
        trace.AppendLine("结论: " + (failure == null && errors.Count == 0 && stage >= 4 ? "全流程通过" : "存在异常"));
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath()));
        File.WriteAllText(ResultPath(), trace.ToString(), new UTF8Encoding(false));
        if (File.Exists(MarkerPath())) File.Delete(MarkerPath());
        Debug.Log("[西瓜小课堂] 路由冒烟测试结束: " + (failure == null && errors.Count == 0 ? "通过" : "有异常"));
        Time.timeScale = 1f;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode)
            EditorApplication.Exit(failure == null && errors.Count == 0 ? 0 : 1);
    }
}
