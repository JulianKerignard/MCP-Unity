using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject gameOverPanel;
    public GameObject mainMenuPanel;

    [Header("Game Over")]
    public TextMeshProUGUI resultText;
    public Button replayButton;
    public Button playButton;

    PanelAnimator gameOverAnim;
    PanelAnimator mainMenuAnim;

    void Start()
    {
        if (replayButton != null)
            replayButton.onClick.AddListener(Replay);
        if (playButton != null)
            playButton.onClick.AddListener(Play);

        // Setup animators
        if (gameOverPanel != null)
        {
            gameOverAnim = gameOverPanel.GetComponent<PanelAnimator>();
            if (gameOverAnim == null) gameOverAnim = gameOverPanel.AddComponent<PanelAnimator>();
            gameOverAnim.showAnim = PanelAnimator.AnimType.ScaleBounce;
            gameOverAnim.showDuration = 0.4f;
            gameOverAnim.HideInstant();
        }

        if (mainMenuPanel != null)
        {
            mainMenuAnim = mainMenuPanel.GetComponent<PanelAnimator>();
            if (mainMenuAnim == null) mainMenuAnim = mainMenuPanel.AddComponent<PanelAnimator>();
            mainMenuAnim.showAnim = PanelAnimator.AnimType.Fade;
            mainMenuAnim.ShowInstant();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameManager.GameState state)
    {
        // Main menu
        if (mainMenuAnim != null)
        {
            if (state == GameManager.GameState.MainMenu)
                mainMenuAnim.Show();
            else
                mainMenuAnim.Hide();
        }

        // Game over / victory
        if (gameOverAnim != null)
        {
            bool showGameOver = state == GameManager.GameState.GameOver || state == GameManager.GameState.Victory;

            if (showGameOver)
            {
                if (resultText != null)
                {
                    string scoreStr = "";
                    if (ScoreManager.Instance != null)
                    {
                        int finalScore = ScoreManager.Instance.CalculateFinalScore();
                        scoreStr = $"\nScore: {finalScore}";
                    }

                    if (state == GameManager.GameState.Victory)
                    {
                        resultText.text = "VICTOIRE !" + scoreStr;
                        resultText.color = new Color(0.66f, 0.85f, 0.53f);
                    }
                    else
                    {
                        resultText.text = "DEFAITE..." + scoreStr;
                        resultText.color = new Color(0.91f, 0.55f, 0.55f);
                    }
                }
                gameOverAnim.Show();
            }
            else
            {
                gameOverAnim.Hide();
            }
        }
    }

    void Play()
    {
        if (DifficultySelectUI.Instance != null)
            DifficultySelectUI.Instance.ShowDifficultySelect();
        else if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    void Replay()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
