
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System;

public class TENSUM_GameManager : MonoBehaviour, IGameManager
{
    public static TENSUM_GameManager Instance { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnBestScoreChanged;
    public event Action<bool> OnGameOver;

    public UIManager uiManager;
    public GridManager gridManager;

    private int score = 0;
    private int bestScore = 0;
    private float timer;
    private bool isGameOver = false;

    private const float GAME_DURATION = 120f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadBestScore();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (isGameOver || GameNavigation.IsTransitioning || RankingOverlay.IsRankingOpen) return;

        timer -= Time.deltaTime;
        uiManager.UpdateTimer(timer);

        if (timer <= 0)
        {
            uiManager.UpdateTimer(0);
            EndGame();
        }
    }

    public void StartGame()
    {
        score = 0;
        timer = GAME_DURATION;
        isGameOver = false;
        
        OnScoreChanged?.Invoke(score);
        OnBestScoreChanged?.Invoke(bestScore);
        OnGameOver?.Invoke(false);

        gridManager.InitializeGrid();
        uiManager.UpdateBestScoreMainGame(bestScore);
    }

    public void AddScore(int amount)
    {
        if (isGameOver || GameNavigation.IsTransitioning) return;
        score += amount;
        OnScoreChanged?.Invoke(score);
        if (uiManager != null) uiManager.UpdateScore(score);
    }

    public int GetScore() => score;
    public int GetBestScore() => bestScore;

    public void EndGame()
    {
        if (isGameOver) return;
        isGameOver = true;
        GameMenu.TryClose();
        
        if (score > bestScore)
        {
            bestScore = score;
            OnBestScoreChanged?.Invoke(bestScore);
            SaveBestScore();
        }

        OnGameOver?.Invoke(true);
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.SubmitScore(LeaderboardKind.TenSum, score);
        if (uiManager != null) uiManager.ShowGameOverPanel(true, score);

        AdEventBus.TriggerGamePlayEnded(MinigameType.TenSum, null);
    }

    private void SaveBestScore()
    {
        TenSumData data = new TenSumData { bestScore = bestScore };
        SaveManager.SaveData("TenSum.json", data);
    }

    private void LoadBestScore()
    {
        if (SaveManager.Exists("TenSum.json"))
        {
            TenSumData data = SaveManager.LoadData<TenSumData>("TenSum.json");
            bestScore = data.bestScore;
        }
    }

    public void RestartGame()
    {
        GameNavigation.Load(SceneManager.GetActiveScene().name, null,
            isGameOver ? (MinigameType?)null : MinigameType.TenSum);
    }

    public void GoToTitle()
    {
        GameNavigation.Load("Title", null, isGameOver ? (MinigameType?)null : MinigameType.TenSum);
    }

    public bool IsGameOver()
    {
        return isGameOver;
    }
}
