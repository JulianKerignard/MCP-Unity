using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DifficultySelectUI : MonoBehaviour
{
    public static DifficultySelectUI Instance { get; private set; }

    [Header("References")]
    public GameObject difficultyPanel;
    public Button easyButton;
    public Button normalButton;
    public Button hardButton;

    [Header("Wave Sets")]
    public WaveSet easyWaveSet;
    public WaveSet normalWaveSet;
    public WaveSet hardWaveSet;

    PanelAnimator panelAnim;

    void Awake()
    {
        Instance = this;

        if (easyButton != null)
            easyButton.onClick.AddListener(() => SelectDifficulty(easyWaveSet));
        if (normalButton != null)
            normalButton.onClick.AddListener(() => SelectDifficulty(normalWaveSet));
        if (hardButton != null)
            hardButton.onClick.AddListener(() => SelectDifficulty(hardWaveSet));

        if (difficultyPanel != null)
        {
            panelAnim = difficultyPanel.GetComponent<PanelAnimator>();
            if (panelAnim == null) panelAnim = difficultyPanel.AddComponent<PanelAnimator>();
            panelAnim.showAnim = PanelAnimator.AnimType.ScaleBounce;
            panelAnim.showDuration = 0.35f;
            panelAnim.HideInstant();
        }
    }

    void Start()
    {
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
        if (panelAnim != null)
            panelAnim.Hide();
    }

    public void ShowDifficultySelect()
    {
        if (panelAnim != null)
            panelAnim.Show();
    }

    void SelectDifficulty(WaveSet waveSet)
    {
        if (waveSet == null || GameManager.Instance == null) return;

        if (GameManager.Instance.gameConfig != null)
            GameManager.Instance.gameConfig.waveSet = waveSet;

        if (panelAnim != null)
            panelAnim.Hide();

        GameManager.Instance.StartGame();
    }
}
