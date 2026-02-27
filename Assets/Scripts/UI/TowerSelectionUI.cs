using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerSelectionUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button[] towerButtons;
    public TextMeshProUGUI[] towerLabels;
    public Image[] towerButtonImages;

    [Header("Config")]
    public TowerData[] towerData;

    [Header("Colors")]
    public Color affordableColor = new Color(0.49f, 0.72f, 0.85f);
    public Color unaffordableColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    void Start()
    {
        Debug.Log($"[TD] TowerSelectionUI.Start() — towerButtons={(towerButtons != null ? towerButtons.Length.ToString() : "NULL")}, towerData={(towerData != null ? towerData.Length.ToString() : "NULL")}");

        if (towerButtons == null || towerData == null)
        {
            Debug.LogError("[TD] TowerSelectionUI: towerButtons or towerData is NULL!");
            return;
        }

        for (int i = 0; i < towerButtons.Length && i < towerData.Length; i++)
        {
            int index = i;
            Debug.Log($"[TD] TowerSelectionUI: Wiring button {i}, towerData[{i}]={(towerData[i] != null ? towerData[i].towerName : "NULL")}");
            towerButtons[i].onClick.AddListener(() => OnTowerButtonClick(index));
            if (towerLabels.Length > i && towerData[i] != null)
                towerLabels[i].text = $"{towerData[i].towerName}\n{towerData[i].cost}g";
        }
    }

    void Update()
    {
        if (EconomyManager.Instance == null) return;

        for (int i = 0; i < towerButtons.Length && i < towerData.Length; i++)
        {
            bool canAfford = EconomyManager.Instance.CanAfford(towerData[i].cost);
            towerButtons[i].interactable = canAfford;
            if (towerButtonImages.Length > i)
                towerButtonImages[i].color = canAfford ? affordableColor : unaffordableColor;
        }
    }

    void OnTowerButtonClick(int index)
    {
        Debug.Log($"[TD] TowerSelectionUI.OnTowerButtonClick({index}) — TowerPlacement.Instance={(TowerPlacement.Instance != null)}");
        if (TowerPlacement.Instance != null)
            TowerPlacement.Instance.SelectTower(index);
        else
            Debug.LogError("[TD] TowerPlacement.Instance is NULL!");
    }
}
