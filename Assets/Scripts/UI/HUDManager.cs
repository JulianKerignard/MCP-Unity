using UnityEngine;
using TMPro;

public class HUDManager : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI livesText;
    public TextMeshProUGUI waveText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI wavePreviewText;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += OnStateChanged;
            GameManager.Instance.OnCountdownChanged += OnCountdown;
        }
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnGoldChanged += UpdateGold;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged += UpdateScore;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= OnStateChanged;
            GameManager.Instance.OnCountdownChanged -= OnCountdown;
        }
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnGoldChanged -= UpdateGold;
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= UpdateScore;
    }

    void Update()
    {
        if (GameManager.Instance != null)
        {
            livesText.text = $"Vies: {GameManager.Instance.currentLives}";
        }
    }

    void UpdateGold(int amount)
    {
        goldText.text = $"Or: {amount}";
    }

    void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    void OnCountdown(float timeLeft)
    {
        if (WaveManager.Instance == null) return;

        string waveLabel = WaveManager.Instance.IsEndless
            ? $"Endless {WaveManager.Instance.CurrentWave}"
            : $"Vague {WaveManager.Instance.CurrentWave}/{WaveManager.Instance.TotalWaves}";

        if (timeLeft > 0f)
            waveText.text = $"{waveLabel} dans {Mathf.CeilToInt(timeLeft)}s (Espace pour skip)";
        else
            waveText.text = waveLabel;
    }

    void OnStateChanged(GameManager.GameState state)
    {
        gameObject.SetActive(state != GameManager.GameState.MainMenu);

        if (state == GameManager.GameState.WaitingForWave && WaveManager.Instance != null)
        {
            string label = WaveManager.Instance.IsEndless
                ? $"Endless {WaveManager.Instance.CurrentWave}"
                : $"Vague: {WaveManager.Instance.CurrentWave}/{WaveManager.Instance.TotalWaves}";
            waveText.text = label;
            UpdateWavePreview();
        }
        else if (state == GameManager.GameState.WaveInProgress && WaveManager.Instance != null)
        {
            string label = WaveManager.Instance.IsEndless
                ? $"Endless {WaveManager.Instance.CurrentWave}"
                : $"Vague: {WaveManager.Instance.CurrentWave}/{WaveManager.Instance.TotalWaves}";
            if (wavePreviewText != null) wavePreviewText.text = "";
        }
    }

    void UpdateWavePreview()
    {
        if (wavePreviewText == null || WaveManager.Instance == null) return;

        var nextWave = WaveManager.Instance.GetCurrentWaveData();
        if (nextWave == null)
        {
            wavePreviewText.text = "";
            return;
        }

        string preview = "Prochaine: ";
        foreach (var entry in nextWave.entries)
        {
            if (entry.enemyType != null)
                preview += $"{entry.count}x {entry.enemyType.enemyName}  ";
        }
        wavePreviewText.text = preview;
    }
}
