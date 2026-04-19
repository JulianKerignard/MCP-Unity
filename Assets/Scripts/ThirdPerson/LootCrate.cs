using UnityEngine;

public class LootCrate : MonoBehaviour
{
    public float interactRange = 6f;
    public string lootName = "Objet";
    public bool isLooted;

    static Transform player;

    void Start()
    {
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
        }
    }

    public bool IsPlayerInRange()
    {
        if (player == null || isLooted) return false;
        return Vector3.Distance(transform.position, player.position) <= interactRange;
    }

    public void Loot()
    {
        if (isLooted) return;
        isLooted = true;

        // Open the lid (rotate top part if exists)
        var lid = transform.Find("Lid");
        if (lid != null)
            lid.localRotation = Quaternion.Euler(-110f, 0f, 0f);

        // Disable after delay
        Invoke(nameof(DisableCrate), 2f);
    }

    void DisableCrate()
    {
        gameObject.SetActive(false);
    }

    // Find closest lootable crate to player
    public static LootCrate GetClosestLootable()
    {
        if (player == null)
        {
            // Retry finding player in case Start() hasn't run yet
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
            else { Debug.Log("[Loot] Player reference is NULL"); return null; }
        }

        LootCrate best = null;
        float bestDist = float.MaxValue;

        foreach (var crate in FindObjectsByType<LootCrate>(FindObjectsSortMode.None))
        {
            if (crate.isLooted) continue;
            float dist = Vector3.Distance(crate.transform.position, player.position);
            //Debug.Log($"[Loot] {crate.name} dist={dist:F2} range={crate.interactRange}");
            if (dist <= crate.interactRange && dist < bestDist)
            {
                bestDist = dist;
                best = crate;
            }
        }
        return best;
    }
}
