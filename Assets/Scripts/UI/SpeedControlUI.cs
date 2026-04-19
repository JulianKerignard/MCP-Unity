using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeedControlUI : MonoBehaviour
{
    public Button speedButton;
    public TextMeshProUGUI speedText;

    readonly float[] speeds = { 1f, 2f, 3f };
    readonly string[] labels = { "x1", "x2", "x3" };
    int currentIndex;

    void Start()
    {
        currentIndex = 0;
        Time.timeScale = 1f;
        UpdateLabel();

        if (speedButton != null)
            speedButton.onClick.AddListener(CycleSpeed);

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    void CycleSpeed()
    {
        currentIndex = (currentIndex + 1) % speeds.Length;
        Time.timeScale = speeds[currentIndex];
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (speedText != null)
            speedText.text = labels[currentIndex];
    }

    void OnStateChanged(GameManager.GameState state)
    {
        // Reset to x1 on menu or game over
        if (state == GameManager.GameState.MainMenu ||
            state == GameManager.GameState.GameOver ||
            state == GameManager.GameState.Victory)
        {
            currentIndex = 0;
            Time.timeScale = 1f;
            UpdateLabel();
        }
    }
}
