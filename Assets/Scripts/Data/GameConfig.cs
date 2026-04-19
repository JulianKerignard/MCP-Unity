using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "TD/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("Economy")]
    public int startingGold = 200;

    [Header("Lives")]
    public int startingLives = 20;

    [Header("Waves")]
    public float timeBetweenWaves = 15f;
    public WaveSet waveSet;

    [Header("Tower Data")]
    public TowerData[] availableTowers;
}
