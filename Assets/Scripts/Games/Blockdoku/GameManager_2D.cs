using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using static SavePaths;

public class GameManager_2D : MonoBehaviour, IGameManager
{
    public static GameManager_2D Instance { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnBestScoreChanged;
    public event Action<bool> OnGameOver;

    public UIManager_2D uiManager; 
    public GridManager_2D gridManager;
    [SerializeField] public BlockSpawner_2D blockSpawner;
    [SerializeField] public AudioManager_2D audioManager;

    private int score = 0;
    private int bestScore = 0;
    public int combo = 0;
    private bool isGameOver = false;
    private bool boardReady;
    public bool IsNewBest { get; private set; }

    // ... (rest of methods)

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadPersonalData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("GameManager_2D Start called.");
        Debug.Log($"BoardDataPath exists: {File.Exists(BoardDataPath)}");
        Debug.Log($"GridManager is null in Start: {gridManager == null}");
        Debug.Log($"BlockSpawner is null in Start: {blockSpawner == null}"); // Added this line

        if (File.Exists(BoardDataPath))
        {
            try { LoadGameData(); }
            catch (InvalidDataException e)
            {
                Debug.LogWarning(e.Message);
                StartGame();
            }
        }
        else
        {
            StartGame();
        }
    }

    void Update()
    {
        if (isGameOver) return;

        // In Blockdoku, game over is not determined by a timer.
        // It's determined by whether the player can place any of the current blocks.
        // This logic will be implemented later.
    }

    public void StartGame()
    {
        Debug.Log("GameManager_2D StartGame called.");
        score = 0;
        combo = 0;
        isGameOver = false;
        IsNewBest = false;

        OnScoreChanged?.Invoke(score);
        OnBestScoreChanged?.Invoke(bestScore);
        OnGameOver?.Invoke(false);

        gridManager.InitializeGrid();
        boardReady = true;
        RemoveGameData(); 
        
        if (blockSpawner != null)
        {
            blockSpawner.SpawnBlocks();
        }
    }

    private int batchScore = 0;
    private int batchMaxCombo = 0;
    private List<string> batchMessages = new List<string>();
    private bool isBatchScoring = false;
    private bool batchHasClearedAny = false;

    public void StartBatchScoring()
    {
        batchScore = 0;
        batchMaxCombo = combo;
        batchMessages.Clear();
        isBatchScoring = true;
        batchHasClearedAny = false;
    }

    public void EndBatchScoring()
    {
        isBatchScoring = false;
        if (batchScore > 0)
        {
            score += batchScore;
            OnScoreChanged?.Invoke(score);
            
            string combinedMsg = string.Join(" ", batchMessages);
            
            // Only show combo if something was cleared or a special message (which increments combo) was triggered this turn
            int displayCombo = (batchHasClearedAny || batchMessages.Count > 0) ? combo : 0;
            uiManager.ShowFloatingScore(batchScore, displayCombo, combinedMsg);

            uiManager.VibratePlacement(displayCombo);
            
            // Only shake the grid if lines were actually cleared
            if (batchHasClearedAny)
            {
                gridManager.ShakeGrid(combo);
            }
        }
    }

    public void AddScore(int amount)
    {
        AddScoreWithPlacement(amount, 0);
    }

    public void AddScoreWithPlacement(int amount, int placementScore)
    {
        int multiplier = Mathf.Max(1, combo);
        int clearScore = amount * 2 * multiplier;
        int total = clearScore + placementScore;

        if (isBatchScoring)
        {
            batchScore += total;
            batchMaxCombo = Mathf.Max(batchMaxCombo, combo);
            if (amount > 0) batchHasClearedAny = true;
        }
        else
        {
            if (total > 0)
            {
                score += total;
                OnScoreChanged?.Invoke(score);
                uiManager.ShowFloatingScore(total, (amount > 0) ? combo : 0);
                uiManager.VibratePlacement(combo);
                
                // Only shake if lines were cleared (amount > 0)
                if (amount > 0)
                {
                    gridManager.ShakeGrid(combo);
                }
            }
        }
    }

    public void AddPlacementScore(int amount)
    {
        if (isBatchScoring)
        {
            batchScore += amount;
        }
        else
        {
            if (amount > 0)
            {
                score += amount;
                OnScoreChanged?.Invoke(score);
                uiManager.ShowFloatingScore(amount, 0); 
            }
        }
    }

    public void AddSpecialScore(int amount, string message)
    {
        int multiplier = Mathf.Max(1, combo);
        int addedScore = amount * multiplier;
        
        if (isBatchScoring)
        {
            batchScore += addedScore;
            batchMaxCombo = Mathf.Max(batchMaxCombo, combo);
            if (!string.IsNullOrEmpty(message)) batchMessages.Add(message);
        }
        else
        {
            if (addedScore > 0)
            {
                score += addedScore;
                OnScoreChanged?.Invoke(score);
                uiManager.ShowFloatingScore(addedScore, combo, message);
            }
        }
    }


    public int GetCombo()
    {
        return combo;
    }
    public int GetScore()
    {
        return score;
    }
    public int GetBestScore() => bestScore;
    public void EndGame()
    {
        if (isGameOver) return;
        isGameOver = true;

        IsNewBest = score > bestScore;
        if (IsNewBest)
        {
            bestScore = score;
            OnBestScoreChanged?.Invoke(bestScore);
        }
        SavePersonalData();
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.SubmitScore(LeaderboardKind.Blockdoku, score);
        RemoveGameData(); 

        OnGameOver?.Invoke(true);

        AdEventBus.TriggerGamePlayEnded(MinigameType.Blockdoku, () =>
        {
            Debug.Log("Ad finished after GameOver.");
        });
    }

    public void GoToTitle()
    {
        if (!isGameOver) SaveGameData();
        GameNavigation.Load("Title");
    }

    public void RestartGame()
    {
        GameNavigation.Load(SceneManager.GetActiveScene().name, RemoveGameData,
            isGameOver ? (MinigameType?)null : MinigameType.Blockdoku);
    }

    public bool IsGameOver()
    {
        return isGameOver;
    }

    public void CheckGameOver()
    {
        if (isGameOver) return;
        List<GameObject> remainingBlocks = blockSpawner.GetSpawnedBlocks();
        if (remainingBlocks.Count == 0) return;

        bool canPlaceAnyBlock = false;
        foreach (GameObject blockGO in remainingBlocks)
        {
            Block_2D block = blockGO.GetComponent<Block_2D>();
            if (gridManager.IsValidPlacementForAll(block.GetShape()))
            {
                canPlaceAnyBlock = true;
                break;
            }
        }

        if (!canPlaceAnyBlock) EndGame();
    }

    public bool CanBlockBePlaced(Block_2D block)
    {
        return gridManager.IsValidPlacementForAll(block.GetShape());
    }

    public void SaveGameData()
    {
        if (isGameOver || !boardReady) return;
        gridManager.SaveBoardData_2D(score, combo, blockSpawner.CaptureBlocks());
        Debug.Log("2D Game data saved!");
    }

    public void LoadGameData()
    {
        (int loadedScore, int loadedCombo) = gridManager.LoadBoardData_2D();
        score = loadedScore;
        combo = loadedCombo;
        boardReady = true;
        OnScoreChanged?.Invoke(score);
        OnBestScoreChanged?.Invoke(bestScore);
        blockSpawner.LoadBlockData_2D(gridManager.LoadedBlocks);
        CheckGameOver();
        Debug.Log("2D Game data loaded!");
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && boardReady && !isGameOver && gridManager != null && blockSpawner != null) SaveGameData();
    }

    public void SavePersonalData()
    {
        PersonalData data = new PersonalData { bestScore = this.bestScore };
        SaveManager.SaveData("personal.json", data);
    }

    public void LoadPersonalData()
    {
        if (SaveManager.Exists("personal.json"))
        {
            PersonalData data = SaveManager.LoadData<PersonalData>("personal.json");
            bestScore = data.bestScore;
        }
    }

    public void RemoveGameData()
    {
        Debug.Log($"Attempting to remove game data. BoardDataPath exists before: {File.Exists(BoardDataPath)}");
        if (File.Exists(BoardDataPath))
        {
            File.Delete(BoardDataPath);
            Debug.Log($"2D Board Save file deleted. BoardDataPath exists after: {File.Exists(BoardDataPath)}");
        }
        else
        {
            Debug.Log("BoardDataPath did not exist, no need to delete.");
        }

        Debug.Log($"Attempting to remove game data. BlockDataPath exists before: {File.Exists(BlockDataPath)}");
        if (File.Exists(BlockDataPath))
        {
            File.Delete(BlockDataPath);
            Debug.Log($"2D Block Save file deleted. BlockDataPath exists after: {File.Exists(BlockDataPath)}");
        }
        else
        {
            Debug.Log("BlockDataPath did not exist, no need to delete.");
        }
    }
}

