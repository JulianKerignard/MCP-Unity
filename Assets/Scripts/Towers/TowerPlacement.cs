using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections;

public class TowerPlacement : MonoBehaviour
{
    public static TowerPlacement Instance { get; private set; }

    [Header("Config")]
    public LayerMask groundLayer;
    public LayerMask towerLayer;
    public LayerMask blockedLayer;
    public Material previewValidMat;
    public Material previewInvalidMat;

    [Header("Tower Data")]
    public TowerData[] towerOptions;

    TowerData selectedTower;
    GameObject previewObj;
    bool isValidPlacement;
    Tower selectedPlacedTower;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        HandleTowerSelection();

        if (selectedTower != null)
        {
            UpdatePreview();

            // Show range during placement
            if (previewObj != null)
                RangeIndicator.Show(previewObj.transform.position, selectedTower.range);

            // Place tower: left click on valid ground (ignore clicks on UI)
            if (WasLeftClickPressed() && isValidPlacement && !IsPointerOverUI())
            {
                PlaceTower();
            }

            // Cancel: right click or Escape
            if (WasRightClickPressed() || WasKeyPressed(Key.Escape))
            {
                CancelSelection();
            }
        }
        else
        {
            // Click on placed tower to show range
            if (WasLeftClickPressed() && !IsPointerOverUI())
            {
                TrySelectPlacedTower();
            }

            // Right click or Escape to deselect placed tower
            if (selectedPlacedTower != null && (WasRightClickPressed() || WasKeyPressed(Key.Escape)))
            {
                DeselectPlacedTower();
            }
        }
    }

    void HandleTowerSelection()
    {
        if (WasKeyPressed(Key.Digit1) && towerOptions.Length > 0)
            SelectTower(0);
        if (WasKeyPressed(Key.Digit2) && towerOptions.Length > 1)
            SelectTower(1);
        if (WasKeyPressed(Key.Digit3) && towerOptions.Length > 2)
            SelectTower(2);
        if (WasKeyPressed(Key.Digit4) && towerOptions.Length > 3)
            SelectTower(3);
    }

    public void SelectTower(int index)
    {
        if (towerOptions == null || index < 0 || index >= towerOptions.Length) return;

        var td = towerOptions[index];
        if (td == null) return;

        if (EconomyManager.Instance != null && !EconomyManager.Instance.CanAfford(td.cost))
            return;

        DeselectPlacedTower();
        selectedTower = td;
        if (previewObj != null) Destroy(previewObj);

        if (selectedTower.prefab != null)
        {
            previewObj = Instantiate(selectedTower.prefab);
            SetPreviewRenderers(previewObj, true);
            DisableComponents(previewObj);
        }
    }

    void UpdatePreview()
    {
        var mousePos = GetMousePosition();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, groundLayer))
        {
            Vector3 pos = hit.point;
            pos.y = 0.5f;
            if (previewObj != null)
                previewObj.transform.position = pos;

            bool towerBlocking = Physics.CheckSphere(pos, 1f, towerLayer);
            bool pathBlocking = Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, 20f, blockedLayer);
            isValidPlacement = !towerBlocking && !pathBlocking;
            SetPreviewMaterial(isValidPlacement);
        }
        else
        {
            isValidPlacement = false;
        }
    }

    void PlaceTower()
    {
        if (selectedTower.prefab == null) return;
        if (EconomyManager.Instance != null && !EconomyManager.Instance.CanAfford(selectedTower.cost))
            return;

        Vector3 pos = previewObj.transform.position;
        var tower = Instantiate(selectedTower.prefab, pos, Quaternion.identity);
        tower.layer = LayerMask.NameToLayer("Tower");
        tower.tag = "Tower";

        var towerComp = tower.GetComponent<Tower>();
        if (towerComp != null)
            towerComp.data = selectedTower;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.Spend(selectedTower.cost);

        // Placement animation: tower grows from ground
        StartCoroutine(PlaceAnimation(tower));

        Debug.Log($"[TD] Tower placed: {selectedTower.towerName} at {pos}");
        CancelSelection();
    }

    void CancelSelection()
    {
        selectedTower = null;
        if (previewObj != null)
        {
            Destroy(previewObj);
            previewObj = null;
        }
        RangeIndicator.Hide();
    }

    void TrySelectPlacedTower()
    {
        var mousePos = GetMousePosition();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, towerLayer))
        {
            var tower = hit.collider.GetComponent<Tower>();
            if (tower == null) tower = hit.collider.GetComponentInParent<Tower>();
            if (tower != null && tower.data != null)
            {
                selectedPlacedTower = tower;
                RangeIndicator.Show(tower.transform.position, tower.Range);
                if (TowerInfoPanel.Instance != null)
                    TowerInfoPanel.Instance.Show(tower);
                return;
            }
        }
        // Clicked ground/nothing: deselect
        if (selectedPlacedTower != null)
            DeselectPlacedTower();
    }

    void DeselectPlacedTower()
    {
        selectedPlacedTower = null;
        RangeIndicator.Hide();
        if (TowerInfoPanel.Instance != null)
            TowerInfoPanel.Instance.Hide();
    }

    void SetPreviewRenderers(GameObject obj, bool transparent)
    {
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
            r.material = previewValidMat;
    }

    void SetPreviewMaterial(bool valid)
    {
        if (previewObj == null) return;
        var mat = valid ? previewValidMat : previewInvalidMat;
        foreach (var r in previewObj.GetComponentsInChildren<Renderer>())
            r.material = mat;
    }

    void DisableComponents(GameObject obj)
    {
        foreach (var c in obj.GetComponentsInChildren<Collider>())
            c.enabled = false;
        foreach (var c in obj.GetComponentsInChildren<Tower>())
            c.enabled = false;
    }

    // --- New Input System helpers ---

    static Vector2 GetMousePosition()
    {
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
        return Input.mousePosition;
    }

    static bool WasLeftClickPressed()
    {
        if (Mouse.current != null)
            return Mouse.current.leftButton.wasPressedThisFrame;
        return Input.GetMouseButtonDown(0);
    }

    static bool WasRightClickPressed()
    {
        if (Mouse.current != null)
            return Mouse.current.rightButton.wasPressedThisFrame;
        return Input.GetMouseButtonDown(1);
    }

    static bool WasKeyPressed(Key key)
    {
        if (Keyboard.current != null)
            return Keyboard.current[key].wasPressedThisFrame;
        return false;
    }

    static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    IEnumerator PlaceAnimation(GameObject tower)
    {
        Vector3 targetScale = tower.transform.localScale;
        tower.transform.localScale = Vector3.zero;
        float duration = 0.525f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Overshoot bounce: goes to 1.15 then back to 1
            float scale = t < 0.7f
                ? Mathf.Lerp(0f, 1.15f, t / 0.7f)
                : Mathf.Lerp(1.15f, 1f, (t - 0.7f) / 0.3f);
            tower.transform.localScale = targetScale * scale;
            yield return null;
        }
        tower.transform.localScale = targetScale;
    }
}
