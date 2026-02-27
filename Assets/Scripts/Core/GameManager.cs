using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Setup, WaitingForWave, WaveInProgress, GameOver, Victory }

    public event Action<GameState> OnStateChanged;
    public event Action<float> OnCountdownChanged;

    [Header("Config")]
    public GameConfig gameConfig;
    public float timeBetweenWaves = 15f;

    [Header("Runtime")]
    public GameState currentState = GameState.MainMenu;
    public int currentLives;

    bool firstWaveLaunched;
    float waveCountdown;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        SetState(GameState.MainMenu);
    }

    public void StartGame()
    {
        if (gameConfig == null) return;

        currentLives = gameConfig.startingLives;
        firstWaveLaunched = false;
        waveCountdown = 0f;

        if (EconomyManager.Instance != null)
            EconomyManager.Instance.Initialize(gameConfig.startingGold);

        if (WaveManager.Instance != null && gameConfig.waveSet != null)
            WaveManager.Instance.Initialize(gameConfig.waveSet.waves);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.StartTracking();

        SetState(GameState.WaitingForWave);
    }

    public void SetState(GameState newState)
    {
        currentState = newState;

        if (newState == GameState.WaitingForWave)
            waveCountdown = firstWaveLaunched ? timeBetweenWaves : 10f;

        OnStateChanged?.Invoke(newState);
    }

    public void LoseLife()
    {
        currentLives--;
        if (currentLives <= 0)
        {
            currentLives = 0;
            SetState(GameState.GameOver);
        }
    }

    public void OnWaveComplete(bool wasLastWave)
    {
        if (wasLastWave && WaveManager.Instance != null && !WaveManager.Instance.IsEndless)
        {
            // Start endless mode after last defined wave
            WaveManager.Instance.StartEndlessMode();
            SetState(GameState.WaitingForWave);
        }
        else
        {
            SetState(GameState.WaitingForWave);
        }
    }

    void Update()
    {
        if (currentState != GameState.WaitingForWave) return;

        bool spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        // All waves: countdown + skip with Space
        waveCountdown -= Time.deltaTime;
        OnCountdownChanged?.Invoke(waveCountdown);

        if (spacePressed || waveCountdown <= 0f)
            LaunchNextWave();
    }

    void LaunchNextWave()
    {
        firstWaveLaunched = true;
        waveCountdown = 0f;
        OnCountdownChanged?.Invoke(0f);

        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.StartNextWave();
            SetState(GameState.WaveInProgress);
        }
    }
}
