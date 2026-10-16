using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the four-stage watermelon growth explorer (ModulePanel_2).
/// Stage navigation, prev/next boundaries, stage illustration switching and a
/// lightweight fade-in. Resets to the first stage whenever the module is
/// (re)opened.
/// </summary>
public class WatermelonGrowthController : MonoBehaviour
{
    private static readonly Color SelectedFill = new Color32(239, 107, 94, 255);
    private static readonly Color IdleFill = new Color32(226, 245, 208, 255);
    private static readonly Color DeepGreen = new Color32(24, 91, 62, 255);
    private static readonly Color White = new Color32(255, 255, 255, 255);
    private static readonly Color DimmedText = new Color32(255, 255, 255, 110);

    private static readonly string[] Titles =
    {
        "种子萌发",
        "幼苗生长",
        "开花授粉",
        "果实发育与成熟"
    };

    private static readonly string[] Bodies =
    {
        "种子吸水后开始萌发，胚根先向下生长，随后长出幼芽。适宜的温度和水分是萌发的重要条件。",
        "幼苗展开叶片，根系逐渐扩大，叶片通过光合作用制造有机物，为后续生长积累能量。",
        "西瓜花通常需要传粉才能结果。蜜蜂等昆虫在花朵之间传递花粉，帮助完成授粉。",
        "受精后的子房逐渐膨大，果肉和糖分持续积累，外皮花纹与果梗状态也会随成熟发生变化。"
    };

    private const float FadeDuration = 0.22f;

    [Header("Stage Navigation")]
    public Button[] stageButtons;
    public Button prevStageButton;
    public Button nextStageButton;

    [Header("Stage Display")]
    public Image stageImage;
    public CanvasGroup stageImageGroup;
    public Sprite[] stageSprites;
    public Text stageNumber;
    public Text stageTitle;
    public Text stageBody;
    public Text progressText;
    public Text selectedHint;

    private int currentStage = -1;
    private Coroutine fadeRoutine;

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
        if (stageButtons != null)
        {
            for (int i = 0; i < stageButtons.Length; i++)
            {
                Button button = stageButtons[i];
                int stageIndex = i;
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => ShowStage(stageIndex));
            }
        }

        if (prevStageButton != null)
        {
            prevStageButton.onClick.RemoveAllListeners();
            prevStageButton.onClick.AddListener(ShowPrevStage);
        }

        if (nextStageButton != null)
        {
            nextStageButton.onClick.RemoveAllListeners();
            nextStageButton.onClick.AddListener(ShowNextStage);
        }
    }

    public void ResetModule()
    {
        ShowStage(0);
    }

    public void ShowNextStage()
    {
        if (currentStage < Titles.Length - 1) ShowStage(currentStage + 1);
    }

    public void ShowPrevStage()
    {
        if (currentStage > 0) ShowStage(currentStage - 1);
    }

    public void ShowStage(int index)
    {
        if (index < 0 || index >= Titles.Length) return;
        currentStage = index;

        if (stageNumber != null) stageNumber.text = "0" + (index + 1);
        if (stageTitle != null) stageTitle.text = Titles[index];
        if (stageBody != null) stageBody.text = Bodies[index];
        if (progressText != null) progressText.text = "阶段 " + (index + 1) + " / " + Titles.Length;
        if (selectedHint != null) selectedHint.text = "已选择阶段：" + Titles[index];

        if (stageImage != null && stageSprites != null && index < stageSprites.Length)
        {
            stageImage.sprite = stageSprites[index];
            stageImage.preserveAspect = true;
        }

        if (stageButtons != null)
        {
            for (int i = 0; i < stageButtons.Length; i++) SetButtonState(stageButtons[i], i == index);
        }

        if (prevStageButton != null)
        {
            prevStageButton.interactable = index > 0;
            SetEdgeLabel(prevStageButton, index > 0);
        }

        if (nextStageButton != null)
        {
            nextStageButton.interactable = index < Titles.Length - 1;
            SetEdgeLabel(nextStageButton, index < Titles.Length - 1);
        }

        PlayFadeIn();
    }

    private static void SetButtonState(Button button, bool selected)
    {
        if (button == null) return;
        Color baseColor = selected ? SelectedFill : IdleFill;
        ColorBlock colors = button.colors;
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, White, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.color = selected ? White : DeepGreen;
    }

    private static void SetEdgeLabel(Button button, bool enabled)
    {
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.color = enabled ? White : DimmedText;
    }

    private void PlayFadeIn()
    {
        if (stageImageGroup == null) return;
        if (!isActiveAndEnabled)
        {
            // Opening the module plays the fade from OnEnable; while inactive
            // just make sure the image is fully visible.
            stageImageGroup.alpha = 1f;
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
        fadeRoutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        stageImageGroup.alpha = 0f;
        float t = 0f;
        while (t < FadeDuration)
        {
            t += Time.deltaTime;
            stageImageGroup.alpha = Mathf.Clamp01(t / FadeDuration);
            yield return null;
        }
        stageImageGroup.alpha = 1f;
        fadeRoutine = null;
    }
}
