using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the nutrition cards and a simple per-100 g comparison chart.
/// </summary>
public class WatermelonNutritionController : MonoBehaviour
{
    public Button[] nutrientButtons;
    public Text nutrientTitle;
    public Text nutrientValue;
    public Text nutrientUnit;
    public Text nutrientBody;
    public Text selectedHint;
    public RectTransform highlightedBar;
    public Text sourceText;

    private readonly string[] titles = { "水分", "糖类", "能量" };
    private readonly string[] values = { "约 91.5", "约 6.2", "约 30" };
    private readonly string[] units = { "%", "g / 100 g", "kcal / 100 g" };
    private readonly string[] bodies =
    {
        "西瓜果肉的主要成分是水分。炎热天气里，适量食用可以为饮食补充水分。",
        "天然糖类带来清甜口感，也是西瓜能量的主要来源。甜度会受到品种和成熟度影响。",
        "每 100 g 西瓜的能量相对较低，但实际摄入量仍取决于食用份量。"
    };
    private readonly float[] barWidths = { 1f, 0.42f, 0.3f };

    private void Awake()
    {
        BindEvents();
        ResetModule();
    }

    private void OnEnable()
    {
        ResetModule();
    }

    public void BindEvents()
    {
        if (nutrientButtons == null) return;
        for (int i = 0; i < nutrientButtons.Length; i++)
        {
            Button button = nutrientButtons[i];
            int nutrientIndex = i;
            if (button == null) continue;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowNutrient(nutrientIndex));
        }
    }

    public void ResetModule()
    {
        ShowNutrient(0);
    }

    public void ShowNutrient(int index)
    {
        if (index < 0 || index >= titles.Length) return;
        if (nutrientTitle != null) nutrientTitle.text = titles[index];
        if (nutrientValue != null) nutrientValue.text = values[index];
        if (nutrientUnit != null) nutrientUnit.text = units[index];
        if (nutrientBody != null) nutrientBody.text = bodies[index];
        if (selectedHint != null) selectedHint.text = "当前主题：" + titles[index];
        if (sourceText != null) sourceText.text = "参考：常见生鲜营养成分表（每 100 g 可食部，教学近似值）";
        if (highlightedBar != null)
        {
            Vector2 size = highlightedBar.sizeDelta;
            size.x = 320f * barWidths[index];
            highlightedBar.sizeDelta = size;
        }
        if (nutrientButtons != null)
        {
            for (int i = 0; i < nutrientButtons.Length; i++) SetButtonState(nutrientButtons[i], i == index);
        }
    }

    private static void SetButtonState(Button button, bool selected)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        Color baseColor = selected ? new Color32(239, 107, 94, 255) : new Color32(255, 231, 180, 255);
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
        button.colors = colors;
    }
}
