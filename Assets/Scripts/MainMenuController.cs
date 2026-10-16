using UnityEngine;

/// <summary>
/// Presentation layer for the home screen. Navigation itself lives in ModuleNavigationController.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public ModuleNavigationController navigation;
    public RectTransform decorativeWatermelon;
    public float bobAmplitude = 7f;
    public float bobSpeed = 1.3f;

    private Vector2 decorativeStartPosition;

    private void Awake()
    {
        if (navigation == null) navigation = GetComponent<ModuleNavigationController>();
        if (decorativeWatermelon != null) decorativeStartPosition = decorativeWatermelon.anchoredPosition;
    }

    private void Update()
    {
        if (decorativeWatermelon == null || navigation == null || navigation.GetCurrentModuleIndex() >= 0) return;
        Vector2 offset = Vector2.up * (Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude);
        decorativeWatermelon.anchoredPosition = decorativeStartPosition + offset;
    }

    public void OpenModule(int moduleIndex)
    {
        if (navigation != null) navigation.ShowModule(moduleIndex);
    }

    public void ReturnHome()
    {
        if (navigation != null) navigation.ShowHome();
    }
}
