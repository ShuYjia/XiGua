using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 常驻余额 HUD：主课堂（编辑器期生成）与 Farm / Cut（运行期生成）共用同一个组件。
/// 订阅统一钱包的 BalanceChanged，保证三处余额始终一致。
/// 独立成文件便于 Unity 解析组件引用；主课堂由 WatermelonProjectSetup 在编辑器期挂载并接线，
/// Farm / Cut 则在运行期 AddComponent。
/// </summary>
public class WatermelonCurrencyHUD : MonoBehaviour
{
    public Text balanceText;
    public Button giftButton;
    public Transform panelParent;
    bool bound;

    void Awake()
    {
        WatermelonCurrencyWallet.EnsureInitialized();
        Refresh();
    }

    void OnEnable()
    {
        WatermelonCurrencyWallet.BalanceChanged += OnBalanceChanged;
        Refresh();
    }

    void OnDisable()
    {
        WatermelonCurrencyWallet.BalanceChanged -= OnBalanceChanged;
    }

    void OnDestroy()
    {
        WatermelonCurrencyWallet.BalanceChanged -= OnBalanceChanged;
        if (giftButton != null) giftButton.onClick.RemoveListener(OpenGiftPanel);
    }

    void OnBalanceChanged(int balance) { Refresh(); }

    public void Refresh()
    {
        // 运行期构建的 UI 可能在 AddComponent 之后才接线，这里做幂等绑定
        if (!bound && giftButton != null)
        {
            giftButton.onClick.RemoveListener(OpenGiftPanel);
            giftButton.onClick.AddListener(OpenGiftPanel);
            bound = true;
        }
        if (balanceText != null)
            balanceText.text = WatermelonCurrencyWallet.CurrencyName + " " + WatermelonCurrencyWallet.GetBalance();
    }

    public void OpenGiftPanel()
    {
        WatermelonGiftPanel.Open(panelParent != null ? panelParent : transform);
    }
}
