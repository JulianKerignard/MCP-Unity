using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("References")]
    public Transform spawnPoint;
    public Transform endPoint;

    [Header("Endless Mode")]
    public EnemyData[] allEnemyTypes;

    WaveData[] waves;
    int currentWaveIndex;
    List<Enemy> activeEnemies = new List<Enemy>();
    bool waveInProgress;
    float currentHealthMultiplier = 1f;
    bool isEndless;
    int endlessWaveNumber;

    public int CurrentWave => isEndless ? waves.Length + endlessWaveNumber : currentWaveIndex + 1;
    public int TotalWaves => isEndless ? -1 : (waves != null ? waves.Length : 0);
    public bool IsEndless => isEndless;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += HandleEnemyDied;
        Enemy.OnEnemyReachedEnd += HandleEnemyReachedEnd;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= HandleEnemyDied;
        Enemy.OnEnemyReachedEnd -= HandleEnemyReachedEnd;
    }

    public void Initialize(WaveData[] waveData)
    {
        waves = waveData;
        currentWaveIndex = 0;
        activeEnemies.Clear();
        waveInProgress = false;
        isEndless = false;
        endlessWaveNumber = 0;
    }

    public void StartNextWave()
    {
        if (waveInProgress) return;

        if (!isEndless && waves != null && currentWaveIndex < waves.Length)
        {
            StartCoroutine(SpawnWave(waves[currentWaveIndex]));
        }
        else if (isEndless)
        {
            endlessWaveNumber++;
            var wave = GenerateEndlessWave(endlessWaveNumber);
            StartCoroutine(SpawnWave(wave));
        }
    }

    public void StartEndlessMode()
    {
        isEndless = true;
        endlessWaveNumber = 0;
    }

    WaveData GenerateEndlessWave(int waveNum)
    {
        var wave = new WaveData();
        wave.healthMultiplier = 2f + waveNum * 0.5f;
        wave.timeBetweenSpawns = Mathf.Max(0.2f, 0.8f - waveNum * 0.03f);
        wave.bonusGold = 50 + waveNum * 10;

        var entries = new List<EnemySpawnEntry>();

        if (allEnemyTypes == null || allEnemyTypes.Length == 0)
        {
            // Fallback: use last wave's enemy types
            if (waves != null && waves.Length > 0)
            {
                var lastWave = waves[waves.Length - 1];
                foreach (var e in lastWave.entries)
                {
                    entries.Add(new EnemySpawnEntry
                    {
                        enemyType = e.enemyType,
                        count = e.count + waveNum * 2
                    });
                }
            }
        }
        else
        {
            // Use allEnemyTypes array
            int baseCount = 5 + waveNum * 3;
            for (int i = 0; i < allEnemyTypes.Length; i++)
            {
                if (allEnemyTypes[i] == null) continue;

                int count;
                bool isBoss = allEnemyTypes[i].bossAbility != BossAbility.None;

                if (isBoss)
                    count = Mathf.Max(1, waveNum / 3);
                else
                    count = baseCount / (allEnemyTypes.Length - 1) + Random.Range(0, waveNum);

                entries.Add(new EnemySpawnEntry
                {
                    enemyType = allEnemyTypes[i],
                    count = count
                });
            }
        }

        wave.entries = entries.ToArray();
        return wave;
    }

    IEnumerator SpawnWave(WaveData wave)
    {
        waveInProgress = true;
        currentHealthMultiplier = wave.healthMultiplier;

        foreach (var entry in wave.entries)
        {
            for (int i = 0; i < entry.count; i++)
            {
                SpawnEnemy(entry.enemyType);
                yield return new WaitForSeconds(wave.timeBetweenSpawns);
            }
        }
    }

    void SpawnEnemy(EnemyData data)
    {
        if (data.prefab == null) return;

        var go = Instantiate(data.prefab, spawnPoint.position, Quaternion.identity);
        go.layer = LayerMask.NameToLayer("Enemy");
        go.tag = "Enemy";
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.Initialize(data, endPoint, currentHealthMultiplier);
            activeEnemies.Add(enemy);
        }
    }

    void HandleEnemyDied(Enemy enemy)
    {
        activeEnemies.Remove(enemy);

        if (EconomyManager.Instance != null && enemy.data != null)
            EconomyManager.Instance.Earn(enemy.data.goldReward);

        CheckWaveComplete();
    }

    void HandleEnemyReachedEnd(Enemy enemy)
    {
        activeEnemies.Remove(enemy);

        if (GameManager.Instance != null)
            GameManager.Instance.LoseLife();

        CheckWaveComplete();
    }

    public WaveData GetCurrentWaveData()
    {
        if (isEndless) return null;
        if (waves == null || currentWaveIndex >= waves.Length) return null;
        return waves[currentWaveIndex];
    }

    public EnemyData GetBasicEnemyData()
    {
        if (allEnemyTypes != null && allEnemyTypes.Length > 0)
            return allEnemyTypes[0];
        if (waves == null || currentWaveIndex <= 0 || currentWaveIndex > waves.Length) return null;
        int idx = Mathf.Min(currentWaveIndex, waves.Length) - 1;
        var wave = waves[idx];
        if (wave.entries != null && wave.entries.Length > 0)
            return wave.entries[0].enemyType;
        return null;
    }

    public void RegisterEnemy(Enemy enemy)
    {
        if (!activeEnemies.Contains(enemy))
            activeEnemies.Add(enemy);
    }

    void CheckWaveComplete()
    {
        if (!waveInProgress) return;
        if (activeEnemies.Count > 0) return;

        // Don't override GameOver state
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver)
        {
            waveInProgress = false;
            return;
        }

        waveInProgress = false;

        if (!isEndless)
        {
            currentWaveIndex++;
            bool wasLast = currentWaveIndex >= waves.Length;

            if (GameManager.Instance != null)
                GameManager.Instance.OnWaveComplete(wasLast);
        }
        else
        {
            // Endless: always continue
            if (GameManager.Instance != null)
                GameManager.Instance.OnWaveComplete(false);
        }
    }
}
