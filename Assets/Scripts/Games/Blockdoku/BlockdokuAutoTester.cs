using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System.IO;

public class BlockdokuAutoTester : MonoBehaviour
{
    public static BlockdokuAutoTester Instance { get; private set; }

    [Header("Test Settings")]
    public int targetRuns = 100;
    public float moveDelay = 0.05f; 
    public bool useSmartAI = true;
    public float comboWeight = 500f; 

    [Header("Current Status")]
    public bool isTesting = false;
    public int currentRun = 0;
    public int lastScore = 0;
    public float averageScore = 0;
    private long totalScoreSum = 0;
    private Dictionary<string, int> remainingBlockCounts = new Dictionary<string, int>();

    private Coroutine testCoroutine;

    void Awake()
    {
        // Keep running even if Unity window loses focus
        Application.runInBackground = true;

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void StartTest()
    {
        if (isTesting) return;
        isTesting = true;
        currentRun = 0;
        totalScoreSum = 0;
        averageScore = 0;

        remainingBlockCounts.Clear();
        if (BlockSpawner_2D.Instance != null)
        {
            foreach (var block in BlockSpawner_2D.Instance.AllPossibleBlocks)
            {
                if (block != null && !remainingBlockCounts.ContainsKey(block.name))
                {
                    remainingBlockCounts[block.name] = 0;
                }
            }
        }

        if (AdManager.Instance != null) AdManager.Instance.EnableAds = false;
        testCoroutine = StartCoroutine(AutoPlayRoutine());
    }

    public void StopTest()
    {
        isTesting = false;
        if (testCoroutine != null)
        {
            StopCoroutine(testCoroutine);
            testCoroutine = null;
        }
        if (AdManager.Instance != null) AdManager.Instance.EnableAds = true;
    }

    public void ExportToCSV()
    {
        // Get the parent directory of Assets (the Project Root)
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string fileName = "TestingResults.csv";
        string path = Path.Combine(projectRoot, fileName);

        try
        {
            bool fileExists = File.Exists(path);

            // Open in 'append' mode (true)
            using (StreamWriter sw = new StreamWriter(path, true))
            {
                sw.WriteLine("");
                sw.WriteLine($"--- Test Session Exported at: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ---");
                
                sw.WriteLine($"TOTAL RUNS,{currentRun}");
                sw.WriteLine($"AVERAGE SCORE,{averageScore:F1}");
                sw.WriteLine("");

                // Block Remaining Analysis
                if (remainingBlockCounts.Count > 0)
                {
                    sw.WriteLine("Remaining Blocks Frequency Analysis:");
                    
                    // Headers: Block Names
                    var sortedKeys = remainingBlockCounts.Keys.OrderBy(k => k).ToList();
                    sw.WriteLine(string.Join(",", sortedKeys));
                    
                    // Data: Counts
                    var counts = sortedKeys.Select(k => remainingBlockCounts[k].ToString()).ToList();
                    sw.WriteLine(string.Join(",", counts));
                }

                sw.WriteLine("------------------------------------------");
            }

            Debug.Log($"<color=green><b>Test results appended successfully to:</b></color> {path}");
        }
        catch (IOException e)
        {
            // Specifically handle cases where the file is locked by another process (like Excel)
            Debug.LogError($"<color=red><b>Failed to write CSV:</b></color> The file might be open in Excel. Close it and try again.\nDetails: {e.Message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"<color=red><b>An unexpected error occurred:</b></color> {e.Message}");
        }
    }

    private IEnumerator AutoPlayRoutine()
    {
        while (currentRun < targetRuns && isTesting)
        {
            if (BlockSpawner_2D.Instance == null || GridManager_2D.Instance == null)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            var spawnedBlocks = BlockSpawner_2D.Instance.GetSpawnedBlocks();
            if (spawnedBlocks == null || spawnedBlocks.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            GameObject selectedBlockGO = null;
            MoveSelection bestSelection = new MoveSelection { isValid = false, score = float.MinValue };

            foreach (var blockGO in spawnedBlocks)
            {
                if (blockGO == null || !blockGO.activeInHierarchy) continue;
                
                Block_2D block = blockGO.GetComponent<Block_2D>();
                var move = EvaluateBestMove(block);

                if (move.isValid && move.score > bestSelection.score)
                {
                    bestSelection = move;
                    selectedBlockGO = blockGO;
                }
            }

            if (bestSelection.isValid && selectedBlockGO != null)
            {
                Block_2D blockScript = selectedBlockGO.GetComponent<Block_2D>();
                BlockSpawner_2D.Instance.BlockPlaced(selectedBlockGO, bestSelection.pos, blockScript.GetShape(), blockScript.blockColor);
                Destroy(selectedBlockGO);
                yield return new WaitForSeconds(moveDelay);
            }
            else
            {
                OnGameOver();
                if (currentRun < targetRuns && isTesting)
                {
                    yield return new WaitForSeconds(1.0f);
                    RestartGame();
                    yield return new WaitForSeconds(1.5f);
                }
            }
            yield return null;
        }
        
        if (currentRun >= targetRuns)
        {
            ExportToCSV();
            Debug.Log("<color=cyan><b>Target Runs completed! Auto-exported results to CSV.</b></color>");
        }

        isTesting = false;
        testCoroutine = null;
        Debug.Log("Auto Test Finished.");
    }

    private struct MoveSelection
    {
        public bool isValid;
        public Vector2Int pos;
        public float score;
    }

    private MoveSelection EvaluateBestMove(Block_2D block)
    {
        List<MoveSelection> options = new List<MoveSelection>();
        var shape = block.GetShape();

        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                Vector2Int pos = new Vector2Int(c, r);
                if (GridManager_2D.Instance.IsValidPlacement(pos, shape))
                {
                    float score = CalculateHeuristicScore(pos, shape);
                    options.Add(new MoveSelection { isValid = true, pos = pos, score = score });
                }
            }
        }

        if (options.Count == 0) return new MoveSelection { isValid = false };

        float maxScore = options.Max(o => o.score);
        var bestOptions = options.Where(o => Mathf.Approximately(o.score, maxScore)).ToList();
        return bestOptions[Random.Range(0, bestOptions.Count)];
    }

    private float CalculateHeuristicScore(Vector2Int pos, List<Vector2Int> shape)
    {
        float score = 0;
        int currentCombo = GameManager_2D.Instance != null ? GameManager_2D.Instance.combo : 0;

        // 1. Clears (High Priority)
        int clearCells = GridManager_2D.Instance.GetPotentialClearedCells(pos, shape).Count;
        score += clearCells * 200f;

        // 1.1 Combo Weight
        if (clearCells > 0)
        {
            // Bonus for maintaining or starting a combo
            // The higher the current combo, the more we want to keep it going
            score += (currentCombo + 1) * comboWeight;
        }
        else if (currentCombo > 0)
        {
            // Penalty for losing the current combo
            score -= currentCombo * comboWeight * 0.5f;
        }

        // 2. Edge/Corner Bonus (Keep center open)
        float distFromCenter = Vector2.Distance(new Vector2(pos.x, pos.y), new Vector2(4, 4));
        score += distFromCenter * 20f;

        // 3. Simple Neighbor Heuristic (Count adjacent cells already occupied)
        // We look for existing neighbors around the landing spots
        foreach (var offset in shape)
        {
            int r = pos.y - offset.y;
            int c = pos.x + offset.x;
            if (r == 0 || r == 8 || c == 0 || c == 8) score += 10f; // Edge bonus per cell
        }
        
        return score;
    }

    private void OnGameOver()
    {
        if (GameManager_2D.Instance != null)
        {
            int finalScore = GameManager_2D.Instance.GetScore();
            lastScore = finalScore;
            totalScoreSum += finalScore;
            currentRun++;
            averageScore = (float)totalScoreSum / currentRun;

            // Track remaining blocks
            if (BlockSpawner_2D.Instance != null)
            {
                var remaining = BlockSpawner_2D.Instance.GetSpawnedBlocks();
                foreach (var blockGO in remaining)
                {
                    if (blockGO != null)
                    {
                        Block_2D block = blockGO.GetComponent<Block_2D>();
                        if (block != null && block.BlockData != null)
                        {
                            string blockName = block.BlockData.name;
                            if (remainingBlockCounts.ContainsKey(blockName))
                            {
                                remainingBlockCounts[blockName]++;
                            }
                            else
                            {
                                remainingBlockCounts[blockName] = 1;
                            }
                        }
                    }
                }
            }

            Debug.Log($"Run {currentRun} Finished. Score: {finalScore} | Avg: {averageScore:F1}");
        }
    }

    private void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(BlockdokuAutoTester))]
public class BlockdokuAutoTesterEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        BlockdokuAutoTester tester = (BlockdokuAutoTester)target;

        GUILayout.Space(15);
        GUILayout.Label("Simulation Controls", UnityEditor.EditorStyles.boldLabel);
        
        GUI.backgroundColor = tester.isTesting ? Color.red : Color.green;
        string startBtnLabel = tester.isTesting ? "Testing in Progress..." : "Start Auto Test";
        
        if (GUILayout.Button(startBtnLabel, GUILayout.Height(40)))
        {
            if (!tester.isTesting) tester.StartTest();
            else tester.StopTest();
        }

        GUI.backgroundColor = Color.white;
        GUILayout.Space(10);

        if (GUILayout.Button("Export Results to CSV (Excel)", GUILayout.Height(30)))
        {
            tester.ExportToCSV();
        }

        if (tester.isTesting)
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
    }
}
#endif
