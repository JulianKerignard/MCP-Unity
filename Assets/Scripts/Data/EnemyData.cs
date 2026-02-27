using UnityEngine;

public enum BossAbility { None, Regen, Shield, Summon }

[CreateAssetMenu(fileName = "New Enemy", menuName = "TD/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Info")]
    public string enemyName;

    [Header("Stats")]
    public float maxHP = 30f;
    public float speed = 3.5f;
    public int goldReward = 10;
    public float armor = 0f;

    [Header("Boss Ability")]
    public BossAbility bossAbility = BossAbility.None;
    public float abilityValue = 0f;

    [Header("Visuals")]
    public Color enemyColor = Color.red;
    public GameObject prefab;
}
