using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerInfoPanel : MonoBehaviour
{
    public static TowerInfoPanel Instance { get; private set; }

    [Header("References")]
    public TextMeshProUGUI towerNameText;
    public TextMeshProUGUI damageText;
    public TextMeshProUGUI rangeText;
    public TextMeshProUGUI fireRateText;
    public TextMeshProUGUI specialText;
    public TextMeshProUGUI levelText;
    public Button upgradeButton;
    public TextMeshProUGUI upgradeButtonText;
    public Button sellButton;
    public TextMeshProUGUI sellButtonText;

    Tower currentTower;
    PanelAnimator panelAnim;

    void Awake()
    {
        Instance = this;
        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClick);
        if (sellButton != null)
            sellButton.onClick.AddListener(OnSellClick);

        panelAnim = GetComponent<PanelAnimator>();
        if (panelAnim == null) panelAnim = gameObject.AddComponent<PanelAnimator>();
        panelAnim.showAnim = PanelAnimator.AnimType.SlideUp;
        panelAnim.showDuration = 0.25f;
        panelAnim.hideDuration = 0.15f;
        panelAnim.HideInstant();
    }

    public void Show(Tower tower)
    {
        if (tower == null || tower.data == null) return;

        currentTower = tower;
        panelAnim.Show();
        Refresh();
    }

    public void Hide()
    {
        currentTower = null;
        panelAnim.Hide();
    }

    void Refresh()
    {
        if (currentTower == null || currentTower.data == null) return;

        var d = currentTower.data;

        towerNameText.text = d.towerName;
        damageText.text = $"Degats: {currentTower.Damage:F0}";
        rangeText.text = $"Portee: {currentTower.Range:F0}";
        fireRateText.text = $"Cadence: {d.fireRate:F1}/s";
        levelText.text = $"Niveau {currentTower.level}/3";

        if (d.isAoE)
            specialText.text = $"Zone: rayon {d.aoERadius:F0}";
        else if (d.isSlow)
            specialText.text = $"Ralenti: {d.slowDuration:F0}s x{d.slowFactor:F1}";
        else
            specialText.text = "";

        // Upgrade button
        if (currentTower.CanUpgrade())
        {
            upgradeButton.gameObject.SetActive(true);
            int cost = currentTower.GetUpgradeCost();
            bool canAfford = EconomyManager.Instance != null && EconomyManager.Instance.CanAfford(cost);
            upgradeButton.interactable = canAfford;
            upgradeButtonText.text = $"Ameliorer ({cost}g)";
        }
        else
        {
            upgradeButton.gameObject.SetActive(false);
        }

        // Sell button
        if (sellButton != null)
        {
            sellButtonText.text = $"Vendre ({currentTower.SellValue}g)";
        }
    }

    public void OnUpgradeClick()
    {
        if (currentTower == null || !currentTower.CanUpgrade()) return;

        int cost = currentTower.GetUpgradeCost();
        if (EconomyManager.Instance == null || !EconomyManager.Instance.CanAfford(cost)) return;

        EconomyManager.Instance.Spend(cost);
        currentTower.Upgrade();
        Refresh();

        RangeIndicator.Show(currentTower.transform.position, currentTower.Range);
    }

    public void OnSellClick()
    {
        if (currentTower == null) return;

        int refund = currentTower.SellValue;
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.Earn(refund);

        Destroy(currentTower.gameObject);
        Hide();
        RangeIndicator.Hide();
    }
}
