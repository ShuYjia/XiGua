using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the four-stage watermelon growth timeline.
/// </summary>
public class WatermelonGrowthController : MonoBehaviour
{
    public Button[] stageButtons;
    public GameObject[] stageVisuals;
    public Text stageNumber;
    public Text stageTitle;
    public Text stageBody;
    public Text selectedHint;

    private readonly string[] titles = { "种子萌发", "幼苗生长", "开花授粉", "果实发育与成熟" };
    private readonly string[] bodies =
    {
        "种子吸水后开始萌发，胚根先向下生长，随后长出幼芽。适宜的温度和水分是萌发的重要条件。",
        "幼苗展开叶片，根系逐渐扩大，叶片通过光合作用制造有机物，为后续生长积累能量。",
        "西瓜花通常需要传粉才能结果。蜜蜂等昆虫在花朵之间传递花粉，帮助完成授粉。",
        "受精后的子房逐渐膨大，果肉和糖分持续积累，外皮花纹与果梗状态也会随成熟发生变化。"
    };

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
        if (stageButtons == null) return;
        for (int i = 0; i < stageButtons.Length; i++)
        {
            Button button = stageButtons[i];
            int stageIndex = i;
            if (button == null) continue;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowStage(stageIndex));
        }
    }

    public void ResetModule()
    {
        ShowStage(0);
    }

    public void ShowStage(int index)
    {
        if (index < 0 || index >= titles.Length) return;
        if (stageNumber != null) stageNumber.text = "0" + (index + 1);
        if (stageTitle != null) stageTitle.text = titles[index];
        if (stageBody != null) stageBody.text = bodies[index];
        if (selectedHint != null) selectedHint.text = "已选择阶段：" + titles[index];

        if (stageVisuals != null)
        {
            for (int i = 0; i < stageVisuals.Length; i++)
            {
                if (stageVisuals[i] != null) stageVisuals[i].SetActive(i == index);
            }
        }

        if (stageButtons != null)
        {
            for (int i = 0; i < stageButtons.Length; i++) SetButtonState(stageButtons[i], i == index);
        }
    }

    private static void SetButtonState(Button button, bool selected)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        Color baseColor = selected ? new Color32(239, 107, 94, 255) : new Color32(226, 245, 208, 255);
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
        button.colors = colors;
    }
}
