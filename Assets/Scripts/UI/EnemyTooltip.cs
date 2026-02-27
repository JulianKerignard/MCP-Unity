using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class EnemyTooltip : MonoBehaviour
{
    public static EnemyTooltip Instance { get; private set; }

    [Header("References")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI healthText;

    [Header("Settings")]
    public Vector2 offset = new Vector2(20f, -20f);

    RectTransform panelRect;
    RectTransform canvasRect;
    Camera mainCam;

    void Awake()
    {
        Instance = this;
        panelRect = tooltipPanel.GetComponent<RectTransform>();
        canvasRect = GetComponent<RectTransform>();
        tooltipPanel.SetActive(false);
    }

    void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (mainCam == null || Mouse.current == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCam.ScreenPointToRay(mousePos);

        // RaycastAll to find enemies even behind ground
        var hits = Physics.RaycastAll(ray, 200f);
        Enemy found = null;
        foreach (var hit in hits)
        {
            var enemy = hit.collider.GetComponent<Enemy>();
            if (enemy == null)
                enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.data != null)
            {
                found = enemy;
                break;
            }
        }

        if (found != null)
            ShowTooltip(found, mousePos);
        else
            HideTooltip();
    }

    void ShowTooltip(Enemy enemy, Vector2 mouseScreenPos)
    {
        tooltipPanel.SetActive(true);

        string name = enemy.data.enemyName;
        if (enemy.hasShield) name += " [Bouclier]";
        if (enemy.data.armor > 0) name += " [Armure]";
        nameText.text = name;
        healthText.text = $"PV: {Mathf.CeilToInt(enemy.currentHP)} / {Mathf.CeilToInt(enemy.maxHPActual)}";

        // Position tooltip near cursor (ScreenSpaceOverlay: position = screen coords)
        panelRect.transform.position = new Vector3(mouseScreenPos.x + 20f, mouseScreenPos.y + 10f, 0f);
    }

    void HideTooltip()
    {
        if (tooltipPanel.activeSelf)
            tooltipPanel.SetActive(false);
    }
}
