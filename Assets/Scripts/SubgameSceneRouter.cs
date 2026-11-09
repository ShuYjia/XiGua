using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主工程与两个独立子游戏场景之间的轻量路由。
/// 只在运行期保存待恢复的模块索引，不做永久存档；重启应用后回到首页。
/// </summary>
public class SubgameSceneRouter : MonoBehaviour
{
    public const string ClassroomSceneName = "SampleScene";
    public const string FarmSceneName = "Farm";
    public const string CutSceneName = "Guayoulishu";

    // 0-based 模块索引：1 = 生长过程；3 = 挑选西瓜
    public const int FarmReturnModule = 1;
    public const int CutReturnModule = 3;

    public ModuleNavigationController navigation;
    public Button farmEntryButton;
    public Button cutEntryButton;

    static int pendingModule = -1;
    static bool loading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneLoaded()
    {
        pendingModule = -1;
        loading = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        loading = false;
    }

    private void Awake()
    {
        // 运行时绑定按钮事件：序列化只保存显式引用，避免依赖编辑器期添加的非持久监听
        if (farmEntryButton != null)
        {
            farmEntryButton.onClick.RemoveAllListeners();
            farmEntryButton.onClick.AddListener(OpenFarm);
        }
        if (cutEntryButton != null)
        {
            cutEntryButton.onClick.RemoveAllListeners();
            cutEntryButton.onClick.AddListener(OpenCut);
        }
    }

    private void Start()
    {
        // 从子游戏返回时恢复发起跳转的模块；冷启动没有待恢复索引，保持首页
        if (navigation == null) return;
        if (pendingModule < 0) return;
        int index = pendingModule;
        pendingModule = -1;
        navigation.ShowModule(index);
    }

    public void OpenFarm()
    {
        OpenSubgame(FarmSceneName, FarmReturnModule);
    }

    public void OpenCut()
    {
        OpenSubgame(CutSceneName, CutReturnModule);
    }

    private void OpenSubgame(string sceneName, int returnModule)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("Watermelon Science: 子游戏场景未加入 Build Settings -> " + sceneName);
            return;
        }

        loading = true;
        pendingModule = returnModule;
        SetEntryButtonsInteractable(false);
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }

    private void SetEntryButtonsInteractable(bool interactable)
    {
        if (farmEntryButton != null) farmEntryButton.interactable = interactable;
        if (cutEntryButton != null) cutEntryButton.interactable = interactable;
    }

    /// <summary>子游戏「返回课堂」入口调用；回到发起跳转的模块。</summary>
    public static void ReturnToClassroom()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(ClassroomSceneName))
        {
            Debug.LogError("Watermelon Science: 主场景未加入 Build Settings -> " + ClassroomSceneName);
            return;
        }

        loading = true;
        SceneManager.LoadSceneAsync(ClassroomSceneName, LoadSceneMode.Single);
    }
}
