[System.Serializable]
public class WaveData
{
    public EnemySpawnEntry[] entries;
    public float timeBetweenSpawns = 1f;
    public int bonusGold = 50;
    public float healthMultiplier = 1f;
}

[System.Serializable]
public class EnemySpawnEntry
{
    public EnemyData enemyType;
    public int count;
}
