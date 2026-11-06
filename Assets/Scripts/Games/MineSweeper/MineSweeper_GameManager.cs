using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System;

public enum MineSweeperDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

public class MineSweeper_GameManager : MonoBehaviour, IGameManager
{
    public static MineSweeper_GameManager Instance { get; private set; }

    public MineSweeper_UIManager uiManager;
    public MineSweeper_GridManager gridManager;

    public bool IsGameOver { get; private set; }
    public bool IsGameStarted { get; private set; }
    private float timer;
    private int remainingMines;
    private MineSweeperDifficulty currentDifficulty;
    public MineSweeperDifficulty CurrentDifficulty => currentDifficulty;
    public bool HasSelectedDifficulty { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnBestScoreChanged;
    public event Action<bool> OnGameOver;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        ShowDifficultySelection();
    }

    void Update()
    {
        if (IsGameStarted && !IsGameOver && !GameNavigation.IsTransitioning && !RankingOverlay.IsOpen)
        {
            timer += Time.deltaTime;
            uiManager.UpdateTimer(timer);
        }
    }

    public void ShowDifficultySelection()
    {
        HasSelectedDifficulty = false;
        LoadBestTimes();
        uiManager.ShowDifficultySelection(true);
        uiManager.ShowGameOverPanel(false);
    }

    public void StartGame()
    {
        // Default call for IGameManager compatibility
        StartGame(MineSweeperDifficulty.Beginner);
    }

    public void StartGame(MineSweeperDifficulty difficulty)
    {
        HasSelectedDifficulty = true;
        currentDifficulty = difficulty;
        IsGameOver = false;
        IsGameStarted = false;
        timer = 0;
        
        OnGameOver?.Invoke(false);

        int rows, cols, mines;

        switch (difficulty)
        {
            case MineSweeperDifficulty.Beginner:
                rows = 9; cols = 9; mines = 10;
                break;
            case MineSweeperDifficulty.Intermediate:
                rows = 16; cols = 16; mines = 40;
                break;
            case MineSweeperDifficulty.Advanced:
                rows = 30; cols = 16; mines = 99;
                break;
            default:
                rows = 9; cols = 9; mines = 10;
                break;
        }

        remainingMines = mines;
        uiManager.UpdateHeaderBestTime(GetBestTimeForCurrentDifficulty());
        uiManager.UpdateMineCount(remainingMines);
        uiManager.UpdateTimer(timer);
        uiManager.ShowDifficultySelection(false);
        gridManager.InitializeGrid(rows, cols, mines);
    }

    public void OnGameStarted()
    {
        IsGameStarted = true;
    }

    public void OnFlagToggled(bool isAdded)
    {
        if (isAdded) remainingMines--;
        else remainingMines++;
        uiManager.UpdateMineCount(remainingMines);
    }

    public void AddScore(int amount)
    {
        // MineSweeper doesn't have a score in the same way, but we implement the interface.
        OnScoreChanged?.Invoke(amount);
    }

    public void EndGame()
    {
        // This would be called if the game ends from outside, but MS ends via GameOver(win)
        GameOver(false);
    }

    public void GameOver(bool win)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        if (win)
        {
            SaveBestTime();
            if (PlayGamesService.Instance != null) PlayGamesService.Instance.SubmitTime(currentDifficulty, timer);
        }

        OnGameOver?.Invoke(true);

        // Show game over panel immediately so user doesn't wait for ad
        uiManager.ShowGameOverPanel(true, win, timer, GetBestTimeForCurrentDifficulty());

        // Trigger ad in the background (will cover the UI when ready)
        AdEventBus.TriggerGamePlayEnded(MinigameType.MineSweeper, null);
    }

    private void LoadBestTimes()
    {
        MineSweeperData data = new MineSweeperData();
        if (SaveManager.Exists("MineSweeper.json"))
        {
            data = SaveManager.LoadData<MineSweeperData>("MineSweeper.json");
        }
        uiManager.UpdateBestTimes(data);
    }

    private void SaveBestTime()
    {
        MineSweeperData data = new MineSweeperData();
        if (SaveManager.Exists("MineSweeper.json"))
        {
            data = SaveManager.LoadData<MineSweeperData>("MineSweeper.json");
        }

        bool newBest = false;
        switch (currentDifficulty)
        {
            case MineSweeperDifficulty.Beginner:
                if (timer < data.bestTimeBeginner) { data.bestTimeBeginner = timer; newBest = true; }
                break;
            case MineSweeperDifficulty.Intermediate:
                if (timer < data.bestTimeIntermediate) { data.bestTimeIntermediate = timer; newBest = true; }
                break;
            case MineSweeperDifficulty.Advanced:
                if (timer < data.bestTimeAdvanced) { data.bestTimeAdvanced = timer; newBest = true; }
                break;
        }

        if (newBest)
        {
            SaveManager.SaveData("MineSweeper.json", data);
            OnBestScoreChanged?.Invoke((int)timer); // Using time as "best score" for MS
            uiManager.UpdateHeaderBestTime(timer);
        }
    }

    private float GetBestTimeForCurrentDifficulty()
    {
        if (SaveManager.Exists("MineSweeper.json"))
        {
            MineSweeperData data = SaveManager.LoadData<MineSweeperData>("MineSweeper.json");
            switch (currentDifficulty)
            {
                case MineSweeperDifficulty.Beginner: return data.bestTimeBeginner;
                case MineSweeperDifficulty.Intermediate: return data.bestTimeIntermediate;
                case MineSweeperDifficulty.Advanced: return data.bestTimeAdvanced;
            }
        }
        return float.MaxValue;
    }

    public void RestartGame()
    {
        GameNavigation.Load(SceneManager.GetActiveScene().name, null,
            IsGameOver ? (MinigameType?)null : MinigameType.MineSweeper);
    }

    public void GoToTitle()
    {
        GameNavigation.Load("Title", null,
            IsGameOver ? (MinigameType?)null : MinigameType.MineSweeper);
    }
}
