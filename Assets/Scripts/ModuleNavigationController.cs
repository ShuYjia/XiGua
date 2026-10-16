using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the single-scene navigation state for the Watermelon Science classroom.
/// </summary>
public class ModuleNavigationController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject homePanel;
    public GameObject[] modulePanels;

    [Header("Navigation Controls")]
    public Button[] homeModuleButtons;
    public Button exitButton;
    public Button[] backButtons;

    private int currentModule = -1;

    private void Awake()
    {
        BindEvents();
        // Preserve an intentionally selected panel in editor previews while keeping the saved default on home.
        if (homePanel == null || homePanel.activeSelf) ShowHome();
    }

    public void BindEvents()
    {
        if (homeModuleButtons != null)
        {
            for (int i = 0; i < homeModuleButtons.Length; i++)
            {
                Button button = homeModuleButtons[i];
                int moduleIndex = i;
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowModule(moduleIndex));
            }
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(QuitApplication);
        }

        if (backButtons != null)
        {
            foreach (Button button in backButtons)
            {
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(ShowHome);
            }
        }
    }

    public void ShowHome()
    {
        currentModule = -1;
        if (homePanel != null) homePanel.SetActive(true);
        SetModulesActive(false);
    }

    public void ShowModule(int moduleIndex)
    {
        if (modulePanels == null || moduleIndex < 0 || moduleIndex >= modulePanels.Length)
        {
            Debug.LogWarning($"Watermelon Science: module index {moduleIndex} is not configured.");
            ShowHome();
            return;
        }

        currentModule = moduleIndex;
        if (homePanel != null) homePanel.SetActive(false);
        SetModulesActive(false);
        if (modulePanels[moduleIndex] != null) modulePanels[moduleIndex].SetActive(true);
    }

    public int GetCurrentModuleIndex()
    {
        return currentModule;
    }

    private void SetModulesActive(bool active)
    {
        if (modulePanels == null) return;
        foreach (GameObject panel in modulePanels)
        {
            if (panel != null) panel.SetActive(active);
        }
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
