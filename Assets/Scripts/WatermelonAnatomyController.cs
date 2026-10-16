using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the interactive anatomy lesson: whole fruit, cross-section, and structure notes.
/// </summary>
public class WatermelonAnatomyController : MonoBehaviour
{
    [Header("Views")]
    public GameObject wholeView;
    public GameObject crossSectionView;
    public Button wholeViewButton;
    public Button crossSectionButton;
    public GameObject wholeViewHint;
    public GameObject crossSectionHint;

    [Header("Structure Notes")]
    public Button[] structureButtons;
    public Text infoTitle;
    public Text infoBody;
    public Text selectedHint;

    private string currentViewLabel = "完整外形";
    private int selectedStructureIndex;

    private readonly string[] structureTitles = { "果皮", "果肉", "种子", "瓜瓤" };
    private readonly string[] structureBodies =
    {
        "外层坚韧的保护层，帮助减少水分散失，也能抵挡一部分外界碰撞。",
        "主要的可食部分，含有大量水分，并带来西瓜清甜的口感。",
        "种子是西瓜繁殖的重要结构。常见品种也有少籽或无籽培育类型。",
        "果肉中较浅色、纤维感更明显的部分，连接着果皮与中心区域。"
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
        if (wholeViewButton != null)
        {
            wholeViewButton.onClick.RemoveAllListeners();
            wholeViewButton.onClick.AddListener(ShowWholeView);
        }

        if (crossSectionButton != null)
        {
            crossSectionButton.onClick.RemoveAllListeners();
            crossSectionButton.onClick.AddListener(ShowCrossSection);
        }

        if (structureButtons == null) return;
        for (int i = 0; i < structureButtons.Length; i++)
        {
            Button button = structureButtons[i];
            int structureIndex = i;
            if (button == null) continue;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ShowStructure(structureIndex));
        }
    }

    public void ResetModule()
    {
        selectedStructureIndex = 0;
        ShowWholeView();
        ShowStructure(0);
    }

    public void ShowWholeView()
    {
        if (wholeView != null) wholeView.SetActive(true);
        if (crossSectionView != null) crossSectionView.SetActive(false);
        if (wholeViewHint != null) wholeViewHint.SetActive(true);
        if (crossSectionHint != null) crossSectionHint.SetActive(false);
        SetButtonState(wholeViewButton, true, 0);
        SetButtonState(crossSectionButton, false, 1);
        currentViewLabel = "完整外形";
        UpdateStatusText();
    }

    public void ShowCrossSection()
    {
        if (wholeView != null) wholeView.SetActive(false);
        if (crossSectionView != null) crossSectionView.SetActive(true);
        if (wholeViewHint != null) wholeViewHint.SetActive(false);
        if (crossSectionHint != null) crossSectionHint.SetActive(true);
        SetButtonState(wholeViewButton, false, 0);
        SetButtonState(crossSectionButton, true, 1);
        currentViewLabel = "剖面结构";
        UpdateStatusText();
    }

    public void ShowStructure(int index)
    {
        if (index < 0 || index >= structureTitles.Length) return;
        if (infoTitle != null) infoTitle.text = structureTitles[index];
        if (infoBody != null) infoBody.text = structureBodies[index];
        selectedStructureIndex = index;
        UpdateStatusText();
        if (structureButtons == null) return;
        for (int i = 0; i < structureButtons.Length; i++)
        {
            SetButtonState(structureButtons[i], i == index, i);
        }
    }

    private static void SetButtonState(Button button, bool selected, int structureIndex)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        Color baseColor = selected ? GetStructureColor(structureIndex) : new Color32(236, 240, 222, 255);
        colors.normalColor = baseColor;
        colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.15f);
        colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
    }

    private static Color GetStructureColor(int structureIndex)
    {
        switch (structureIndex)
        {
            case 0: return new Color32(53, 128, 74, 255); // rind
            case 1: return new Color32(239, 102, 87, 255); // flesh
            case 2: return new Color32(88, 65, 44, 255); // seeds
            default: return new Color32(141, 192, 81, 255); // pith
        }
    }

    private void UpdateStatusText()
    {
        if (selectedHint == null) return;
        selectedHint.text = "当前视图：" + currentViewLabel + "  ·  已选结构：" + structureTitles[selectedStructureIndex];
    }
}

