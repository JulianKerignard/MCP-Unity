using UnityEngine;

public enum DamageType { Physical, Magic }
public enum TowerFXType { Basic, Sniper, AoE, Slow }

[CreateAssetMenu(fileName = "New Tower", menuName = "TD/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Info")]
    public string towerName;
    public string description;

    [Header("Stats")]
    public float damage = 10f;
    public float range = 8f;
    public float fireRate = 0.5f;
    public int cost = 50;
    public int upgradeCost = 40;

    [Header("Upgrade (Level 2)")]
    public float upgradeDamageMultiplier = 1.5f;
    public float upgradeRangeMultiplier = 1.2f;
    public int upgradeCost2 = 80;

    [Header("Upgrade (Level 3)")]
    public float upgradeDamageMultiplier3 = 2.2f;
    public float upgradeRangeMultiplier3 = 1.4f;

    [Header("Sell")]
    public float sellRefundPercent = 0.6f;

    [Header("Damage Type")]
    public DamageType damageType = DamageType.Physical;

    [Header("Special")]
    public bool isAoE;
    public float aoERadius = 3f;
    public bool isSlow;
    public float slowFactor = 0.5f;
    public float slowDuration = 2f;

    [Header("Visuals")]
    public Color towerColor = Color.white;
    public TowerFXType fxType = TowerFXType.Basic;
    public GameObject prefab;
    public GameObject projectilePrefab;
}
