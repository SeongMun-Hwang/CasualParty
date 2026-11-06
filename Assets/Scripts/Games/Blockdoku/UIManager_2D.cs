using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class UIManager_2D : MonoBehaviour
{
    [Header("Top UI")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI bestScoreText;

    [Header("Game Play UI")]
    public FloatingScore floatingScore;

    [Header("Panels")]
    public GameObject gameOverPanel;
    public GameObject gameResetPanel;
    public GameObject settingPanel;

    [Header("GameOver Panel")]
    public TextMeshProUGUI finalScoreText;
    public GameObject newBestObj;
    public Button GameOverPanelYesButton;

    [Header("Reset Panel")]
    public Button ResetPanelYesButton;

    [Header("Buttons")]
    public Button restartButton;
    public Button titleButton;
    public Button settingsButton;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameResetPanel != null) gameResetPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);

        if (GetComponent<GameMenu>() == null)
        {
            if (restartButton != null) restartButton.onClick.AddListener(ShowResetPanel);
            if (titleButton != null) titleButton.onClick.AddListener(() => GameManager_2D.Instance.GoToTitle());
            if (settingsButton != null) settingsButton.onClick.AddListener(ToggleSettingPanel);
        }
        if (ResetPanelYesButton != null) ResetPanelYesButton.onClick.AddListener(ResetPanelYes);
        if (GameOverPanelYesButton != null) GameOverPanelYesButton.onClick.AddListener(() => UI_Functions.Instance.BacktoTitleOnClicked());
    }

    void OnEnable()
    {
        if (GameManager_2D.Instance != null)
        {
            GameManager_2D.Instance.OnScoreChanged += UpdateScore;
            GameManager_2D.Instance.OnBestScoreChanged += UpdateBestScore;
            GameManager_2D.Instance.OnGameOver += HandleGameOver;
        }
    }

    void OnDisable()
    {
        if (GameManager_2D.Instance != null)
        {
            GameManager_2D.Instance.OnScoreChanged -= UpdateScore;
            GameManager_2D.Instance.OnBestScoreChanged -= UpdateBestScore;
            GameManager_2D.Instance.OnGameOver -= HandleGameOver;
        }
    }

    void Start()
    {
        // Awake/OnEnable ordering between scene objects is not guaranteed.
        OnDisable();
        OnEnable();
        if (GameManager_2D.Instance == null) return;
        UpdateScore(GameManager_2D.Instance.GetScore());
        UpdateBestScore(GameManager_2D.Instance.GetBestScore());
        HandleGameOver(GameManager_2D.Instance.IsGameOver());
    }

    private void HandleGameOver(bool isOver)
    {
        if (isOver)
        {
            int finalScore = GameManager_2D.Instance.GetScore();
            int bestScore = 0;
            if (SaveManager.Exists("personal.json"))
            {
                bestScore = SaveManager.LoadData<PersonalData>("personal.json").bestScore;
            }
            ShowGameOverPanel(true, finalScore, bestScore);
        }
        else
        {
            ShowGameOverPanel(false);
        }
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = $"{score}";
    }

    public void UpdateBestScore(int bestScore)
    {
        if (bestScoreText != null) bestScoreText.text = $"{bestScore}";
    }

    public void ShowFloatingScore(int score, int combo, string specialMessage = "")
    {
        if (floatingScore != null && score != 0)
        {
            floatingScore.Show(score, combo, specialMessage);
        }
    }

    public void ShowGameOverPanel(bool show, int finalScore = 0, int bestScore = 0)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(show);
            if (show)
            {
                if (finalScoreText != null) finalScoreText.text = $"Score: {finalScore}";
                if (newBestObj != null) newBestObj.SetActive(GameManager_2D.Instance.IsNewBest);
                Vibrate();
            }
        }
    }

    public void ToggleSettingPanel()
    {
        if (settingPanel != null) settingPanel.SetActive(!settingPanel.activeSelf);
    }

    public void ShowResetPanel()
    {
        if (gameResetPanel != null) gameResetPanel.SetActive(!gameResetPanel.activeSelf);
    }

    public void ResetPanelYes()
    {
        GameManager_2D.Instance.RestartGame();
    }

    public void Vibrate()
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.Vibrate();
    }
}
