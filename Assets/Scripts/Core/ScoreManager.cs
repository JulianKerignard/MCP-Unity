using UnityEngine;
using System;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public event Action<int> OnScoreChanged;

    public int Score { get; private set; }
    public int EnemiesKilled { get; private set; }

    int speedBonusPoints;
    float gameStartTime;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        Enemy.OnEnemyDied += OnEnemyKilled;
    }

    void OnDisable()
    {
        Enemy.OnEnemyDied -= OnEnemyKilled;
    }

    public void StartTracking()
    {
        Score = 0;
        EnemiesKilled = 0;
        speedBonusPoints = 0;
        gameStartTime = Time.time;
        OnScoreChanged?.Invoke(Score);
    }

    void OnEnemyKilled(Enemy enemy)
    {
        if (enemy == null || enemy.data == null) return;

        EnemiesKilled++;

        // Base points from gold reward
        int points = enemy.data.goldReward;

        // Armor bonus: armored enemies worth more
        if (enemy.data.armor > 0) points += 5;

        // Boss bonus
        if (enemy.data.bossAbility != BossAbility.None) points += 20;

        // Speed bonus: playing at 2x gives +25%, 3x gives +50%
        if (Time.timeScale >= 3f) points = Mathf.RoundToInt(points * 1.5f);
        else if (Time.timeScale >= 2f) points = Mathf.RoundToInt(points * 1.25f);

        Score += points;
        OnScoreChanged?.Invoke(Score);
    }

    public int CalculateFinalScore()
    {
        int final_score = Score;

        // Lives bonus: 50 points per remaining life
        if (GameManager.Instance != null)
            final_score += GameManager.Instance.currentLives * 50;

        // Gold bonus: 1 point per 2 gold remaining
        if (EconomyManager.Instance != null)
            final_score += EconomyManager.Instance.Gold / 2;

        // Time bonus: faster = more points (max 500 for finishing under 5 min)
        float elapsed = Time.time - gameStartTime;
        if (elapsed < 300f)
            final_score += Mathf.RoundToInt((300f - elapsed) * 1.67f);

        return final_score;
    }
}
