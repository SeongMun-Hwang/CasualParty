using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI bestScoreMainGameText;
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (TENSUM_GameManager.Instance != null)
        {
            TENSUM_GameManager.Instance.OnScoreChanged += UpdateScore;
            TENSUM_GameManager.Instance.OnBestScoreChanged += UpdateBestScoreMainGame;
            TENSUM_GameManager.Instance.OnGameOver += HandleGameOver;
        }
    }

    void OnDisable()
    {
        if (TENSUM_GameManager.Instance != null)
        {
            TENSUM_GameManager.Instance.OnScoreChanged -= UpdateScore;
            TENSUM_GameManager.Instance.OnBestScoreChanged -= UpdateBestScoreMainGame;
            TENSUM_GameManager.Instance.OnGameOver -= HandleGameOver;
        }
    }

    private void HandleGameOver(bool isOver) => ShowGameOverPanel(isOver, TENSUM_GameManager.Instance.GetScore());

    void Start()
    {
        OnDisable();
        OnEnable();
        if (TENSUM_GameManager.Instance == null) return;
        UpdateScore(TENSUM_GameManager.Instance.GetScore());
        UpdateBestScoreMainGame(TENSUM_GameManager.Instance.GetBestScore());
        HandleGameOver(TENSUM_GameManager.Instance.IsGameOver());
    }

    private int displayedSecond = int.MinValue;
    public void UpdateTimer(float timeInSeconds)
    {
        if (timerText == null) return;
        int second = Mathf.FloorToInt(timeInSeconds);
        if (displayedSecond == second) return;
        displayedSecond = second;

        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = score.ToString();
    }

    public void UpdateBestScoreMainGame(int bestScore)
    {
        if (bestScoreMainGameText != null) bestScoreMainGameText.text = bestScore.ToString();
    }

    public void ShowGameOverPanel(bool show, int finalScore = 0)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(show);
            if (show && finalScoreText != null)
            {
                finalScoreText.text = $"Score: {finalScore}";
            }
        }
    }
}
