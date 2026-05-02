using UnityEngine;
using System.Collections.Generic;
using System.Collections; 
using System.Linq; 
using UnityEngine.UI;
using System.IO;
using DG.Tweening;

using static SavePaths;

public class GridManager_2D : MonoBehaviour
{
    public static GridManager_2D Instance { get; private set; }

    [Header("Prefabs & Parents")]
    public GameObject cellPrefab;
    public Transform gridParent;
    
    [Header("Visual Settings")]
    public Color previewColor = new Color(0f, 1f, 0f, 0.5f);
    public Color clearBlinkColor = Color.cyan;
    public float clearBlinkInterval = 0.3f;
    public float clearAnimationSequentialDelay = 0.05f;
    public Sprite defaultEmptyCellSprite;
    public Sprite defaultOccupiedCellSprite;

    [Header("Symmetry Effect")]
    public GameObject symmetryEffectPrefab;
    public RectTransform symmetryEffectContainer;
    public float symmetryEffectDuration = 0.8f;
    public float ghostStepDistance = 6f;
    public float trailExist = 0.4f;

    [Header("Shake Effect")]
    [Range(0f, 1f)] public float shakeDuration = 0.15f;
    [Range(0f, 100f)] public float shakeMagnitude = 10f;

    private const int GRID_SIZE = 9;
    private Cell_2D[,] grid = new Cell_2D[GRID_SIZE, GRID_SIZE];
    private GridLayoutGroup gridLayoutGroup;
    private Vector3 originalGridPos;
    private Coroutine shakeCoroutine;
    private List<Cell_2D> previewCells = new List<Cell_2D>();
    private HashSet<Cell_2D> currentlyBlinkingClearCells = new HashSet<Cell_2D>();
    private Dictionary<Cell_2D, Color> storedOriginalClearPredictColors = new Dictionary<Cell_2D, Color>();
    private Coroutine clearPredictBlinkCoroutine;

    // Optimization: High-Performance Pooling
    private struct PoolItem
    {
        public GameObject go;
        public RectTransform rt;
        public Image img;
        public Vector3 initialScale;
    }
    private Stack<PoolItem> symmetryPool = new Stack<PoolItem>();
    private List<Color> _tempColorList = new List<Color>();
    private List<Cell_2D> _tempCellList = new List<Cell_2D>();
    private WaitForSeconds cachedSequentialClearWait;

    public Color subgridBorderColor = Color.black;
    public float subgridBorderWidth = 5f;

    [System.Serializable]
    public class SaveData_2D
    {
        public bool[] cellOccupiedStates = new bool[GRID_SIZE * GRID_SIZE];
        public List<SerializableColor> cellColors = new List<SerializableColor>(GRID_SIZE * GRID_SIZE);
        public int score;
        public int combo;
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        gridLayoutGroup = gridParent.GetComponent<GridLayoutGroup>();
        originalGridPos = gridParent.localPosition;
        cachedSequentialClearWait = new WaitForSeconds(clearAnimationSequentialDelay);
    }

    private PoolItem GetSymmetryObject()
    {
        if (symmetryPool.Count > 0)
        {
            PoolItem item = symmetryPool.Pop();
            if (item.img != null) item.img.enabled = true;
            item.rt.localScale = item.initialScale;
            return item;
        }
        else
        {
            GameObject go = Instantiate(symmetryEffectPrefab, symmetryEffectContainer);
            RectTransform rt = go.GetComponent<RectTransform>();
            Image img = go.GetComponent<Image>();
            return new PoolItem { go = go, rt = rt, img = img, initialScale = rt.localScale };
        }
    }

    private void ReturnSymmetryObject(PoolItem item)
    {
        if (item.go == null) return;
        
        // 정적 Kill 메서드를 사용하여 안전하게 트윈 중지
        DOTween.Kill(item.rt);
        DOTween.Kill(item.img);
        DOTween.Kill(item.go);

        if (item.img != null) item.img.enabled = false;
        
        // 화면 밖으로 이동하여 UI Rebuild 방지
        item.rt.localPosition = new Vector3(10000, 10000, 0); 
        symmetryPool.Push(item);
    }

    public void InitializeGrid()
    {
        // Use standard Destroy instead of PoolManager
        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        for (int r = 0; r < GRID_SIZE; r++)
        {
            for (int c = 0; c < GRID_SIZE; c++)
            {
                GameObject cellGO = Instantiate(cellPrefab, gridParent);
                cellGO.name = $"Cell_{r}_{c}";
                grid[r, c] = cellGO.GetComponent<Cell_2D>() ?? cellGO.AddComponent<Cell_2D>();
                grid[r, c].Initialize(r, c, true);
                CreateSubgridBorders(cellGO, r, c);
            }
        }
    }

    private void CreateSubgridBorders(GameObject cellGO, int r, int c)
    {
        if (r % 3 == 2 && r < GRID_SIZE - 1)
            CreateBorder(cellGO.transform, "HorizontalBorder", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, subgridBorderWidth));
        if (c % 3 == 2 && c < GRID_SIZE - 1)
            CreateBorder(cellGO.transform, "VerticalBorder", new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(subgridBorderWidth, 0));
    }

    private void CreateBorder(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
    {
        GameObject borderGO = new GameObject(name);
        borderGO.transform.SetParent(parent, false);
        Image borderImage = borderGO.AddComponent<Image>();
        borderImage.color = subgridBorderColor;
        RectTransform rt = borderGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = sizeDelta;
    }

    public bool IsValidPlacementForAll(List<Vector2Int> blockShape)
    {
        for (int r = 0; r < GRID_SIZE; r++)
            for (int c = 0; c < GRID_SIZE; c++)
                if (IsValidPlacement(new Vector2Int(c, r), blockShape)) return true;
        return false;
    }

    public bool IsValidPlacement(Vector2Int gridPosition, List<Vector2Int> blockShape)
    {
        foreach (var pos in blockShape)
        {
            int r = gridPosition.y - pos.y;
            int c = gridPosition.x + pos.x;
            if (r < 0 || r >= GRID_SIZE || c < 0 || c >= GRID_SIZE) return false;
            if (!grid[r, c].IsEmpty) return false;
        }
        return true;
    }

    public int PlaceBlock(Vector2Int gridPosition, List<Vector2Int> blockShape, Color blockColor)
    {
        StopClearPredictBlink();

        if (GameManager_2D.Instance != null) GameManager_2D.Instance.StartBatchScoring();

        foreach (var pos in blockShape)
        {
            int r = gridPosition.y - pos.y;
            int c = gridPosition.x + pos.x;
            grid[r, c].SetOccupied(blockColor);
        }

        // Pass block size as placementScore to combine it with clear score if lines are cleared
        int clearCount = CheckForCompletedLines(blockShape.Count);
        
        if (GameManager_2D.Instance != null)
        {
            GameManager_2D.Instance.EndBatchScoring();
            GameManager_2D.Instance.SaveGameData();
        }
        return clearCount;
    }

    public void ShowPreview(Vector2Int gridPosition, List<Vector2Int> blockShape)
    {
        ClearPreview();
        if (IsValidPlacement(gridPosition, blockShape))
        {
            foreach (var pos in blockShape)
            {
                int r = gridPosition.y - pos.y;
                int c = gridPosition.x + pos.x;
                if (r >= 0 && r < GRID_SIZE && c >= 0 && c < GRID_SIZE)
                {
                    grid[r, c].SetPreview(previewColor);
                    previewCells.Add(grid[r, c]);
                }
            }

            HashSet<Cell_2D> potentialClearedCells = GetPotentialClearedCells(gridPosition, blockShape);
            if (potentialClearedCells.Count > 0)
            {
                currentlyBlinkingClearCells = potentialClearedCells;
                clearPredictBlinkCoroutine = StartCoroutine(ClearPredictBlink(currentlyBlinkingClearCells, clearBlinkColor, clearBlinkInterval));
            }
        }
    }

    public void ClearPreview()
    {
        foreach (var cell in previewCells) cell.ClearPreview();
        previewCells.Clear();
        StopClearPredictBlink();
    }

    private void StopClearPredictBlink()
    {
        if (clearPredictBlinkCoroutine != null) { StopCoroutine(clearPredictBlinkCoroutine); clearPredictBlinkCoroutine = null; }
        foreach (var cell in currentlyBlinkingClearCells)
            if (cell != null && storedOriginalClearPredictColors.ContainsKey(cell))
                cell.cellImage.color = storedOriginalClearPredictColors[cell];
        currentlyBlinkingClearCells.Clear();
        storedOriginalClearPredictColors.Clear();
    }

    private HashSet<Vector2Int> GetCompletedCellPositions(bool[,] occupiedState)
    {
        HashSet<Vector2Int> completed = new HashSet<Vector2Int>();
        List<int> completedRows = new List<int>();
        List<int> completedCols = new List<int>();

        for (int i = 0; i < GRID_SIZE; i++)
        {
            bool rowComplete = true, colComplete = true;
            for (int j = 0; j < GRID_SIZE; j++)
            {
                if (!occupiedState[i, j]) rowComplete = false;
                if (!occupiedState[j, i]) colComplete = false;
            }
            if (rowComplete) completedRows.Add(i);
            if (colComplete) completedCols.Add(i);
        }

        for (int r = 0; r < GRID_SIZE; r += 3)
            for (int c = 0; c < GRID_SIZE; c += 3)
            {
                bool squareComplete = true;
                for (int i = r; i < r + 3; i++)
                    for (int j = c; j < c + 3; j++)
                        if (!occupiedState[i, j]) squareComplete = false;
                if (squareComplete)
                    for (int i = r; i < r + 3; i++)
                        for (int j = c; j < c + 3; j++) completed.Add(new Vector2Int(j, i));
            }

        foreach (var row in completedRows) for (int c = 0; c < GRID_SIZE; c++) completed.Add(new Vector2Int(c, row));
        foreach (var col in completedCols) for (int r = 0; r < GRID_SIZE; r++) completed.Add(new Vector2Int(col, r));

        return completed;
    }

    public HashSet<Cell_2D> GetPotentialClearedCells(Vector2Int gridPosition, List<Vector2Int> blockShape)
    {
        bool[,] tempGrid = new bool[GRID_SIZE, GRID_SIZE];
        for (int r = 0; r < GRID_SIZE; r++)
            for (int c = 0; c < GRID_SIZE; c++) tempGrid[r, c] = !grid[r, c].IsEmpty;

        foreach (var pos in blockShape)
        {
            int r = gridPosition.y - pos.y;
            int c = gridPosition.x + pos.x;
            if (r >= 0 && r < GRID_SIZE && c >= 0 && c < GRID_SIZE) tempGrid[r, c] = true;
        }

        HashSet<Vector2Int> positions = GetCompletedCellPositions(tempGrid);
        HashSet<Cell_2D> cells = new HashSet<Cell_2D>();
        foreach (var pos in positions) cells.Add(grid[pos.y, pos.x]);
        return cells;
    }

    private int CalculateLinesClearedCount(bool[,] occupiedState)
    {
        int count = 0;
        for (int i = 0; i < GRID_SIZE; i++)
        {
            bool row = true, col = true;
            for (int j = 0; j < GRID_SIZE; j++) { if (!occupiedState[i, j]) row = false; if (!occupiedState[j, i]) col = false; }
            if (row) count++; if (col) count++;
        }
        for (int r = 0; r < GRID_SIZE; r += 3)
            for (int c = 0; c < GRID_SIZE; c += 3)
            {
                bool sq = true;
                for (int i = r; i < r + 3; i++)
                    for (int j = c; j < c + 3; j++) if (!occupiedState[i, j]) sq = false;
                if (sq) count++;
            }
        return count;
    }

    public int CheckForCompletedLines(int placementScore = 0)
    {
        bool[,] currentOccupied = new bool[GRID_SIZE, GRID_SIZE];
        for (int r = 0; r < GRID_SIZE; r++)
            for (int c = 0; c < GRID_SIZE; c++) currentOccupied[r, c] = !grid[r, c].IsEmpty;

        HashSet<Vector2Int> completedPositions = GetCompletedCellPositions(currentOccupied);
        if (completedPositions.Count == 0)
        {
            if (!CheckSymmetry()) GameManager_2D.Instance.combo = 0;
            
            // If nothing cleared, still add the placement score
            if (placementScore > 0)
            {
                GameManager_2D.Instance.AddPlacementScore(placementScore);
            }
            return 0;
        }

        HashSet<Cell_2D> cellsToClear = new HashSet<Cell_2D>();
        foreach (var pos in completedPositions)
        {
            Cell_2D cell = grid[pos.y, pos.x];
            cellsToClear.Add(cell);
            cell.ClearLogically(); // Mark logically empty IMMEDIATELY
        }

        List<Color> clearColors = cellsToClear.Select(c => c.BlockColor).Distinct().ToList();
        int linesClearedCount = CalculateLinesClearedCount(currentOccupied);
        
        int totalOccupiedBefore = 0;
        for (int r = 0; r < GRID_SIZE; r++)
            for (int c = 0; c < GRID_SIZE; c++) if (!grid[r, c].IsEmpty) totalOccupiedBefore++;

        bool isFullClear = totalOccupiedBefore == cellsToClear.Count;

        StartCoroutine(SequentialClear(cellsToClear));
        GameManager_2D.Instance.combo += linesClearedCount;

        if (isFullClear)
        {
            GameManager_2D.Instance.combo++;
            GameManager_2D.Instance.AddSpecialScore(100, "FULL CLEAR");
            PlayFullClearAnimation(clearColors);
        }

        // Pass both cleared cell count and placement score to be combined
        GameManager_2D.Instance.AddScoreWithPlacement(cellsToClear.Count, placementScore);
        
        if (AudioManager_2D.Instance != null) AudioManager_2D.Instance.PlayBlockDestroyAudio(GameManager_2D.Instance.combo);

        if (!isFullClear) CheckSymmetry();
        return cellsToClear.Count;
    }

    private bool CheckSymmetry()
    {
        bool hSym = true, vSym = true, d1Sym = true, d2Sym = true;
        int occupiedCount = 0;

        for (int r = 0; r < GRID_SIZE; r++)
        {
            for (int c = 0; c < GRID_SIZE; c++)
            {
                if (grid[r, c].IsEmpty) continue;
                occupiedCount++;
                if (grid[r, 8 - c].IsEmpty) hSym = false;
                if (grid[8 - r, c].IsEmpty) vSym = false;
                if (grid[c, r].IsEmpty) d1Sym = false;
                if (grid[8 - c, 8 - r].IsEmpty) d2Sym = false;
            }
        }

        bool symmetryAchieved = hSym || vSym || d1Sym || d2Sym;
        if (occupiedCount > 0 && symmetryAchieved)
        {
            GameManager_2D.Instance.combo++;
            int bonus = 30 + (occupiedCount * 3);
            if (hSym) { GameManager_2D.Instance.AddSpecialScore(bonus, "H-SYMMETRY"); PlaySymmetryAnimation("H"); }
            else if (vSym) { GameManager_2D.Instance.AddSpecialScore(bonus, "V-SYMMETRY"); PlaySymmetryAnimation("V"); }
            else if (d1Sym || d2Sym) { GameManager_2D.Instance.AddSpecialScore(bonus, "DIAG-SYMMETRY"); PlaySymmetryAnimation(d1Sym ? "D1" : "D2"); }
        }
        return symmetryAchieved;
    }

    public void PlayFullClearAnimation(List<Color> sourceColors = null)
    {
        if (symmetryEffectPrefab == null || symmetryEffectContainer == null) return;
        if (GameManager_2D.Instance != null && GameManager_2D.Instance.uiManager != null) GameManager_2D.Instance.uiManager.Vibrate();
        ShakeGrid(GameManager_2D.Instance.combo + 5);

        List<Color> colors = GetRandomActiveColors(2);
        if (sourceColors != null && sourceColors.Count > 0)
        {
            colors[0] = sourceColors[Random.Range(0, sourceColors.Count)];
            colors[1] = sourceColors[Random.Range(0, sourceColors.Count)];
        }

        float w = symmetryEffectContainer.rect.width / 2f, h = symmetryEffectContainer.rect.height / 2f;
        Vector3[] points = { new Vector3(-w, h), new Vector3(0, h), new Vector3(w, h), new Vector3(w, 0), new Vector3(w, -h), new Vector3(0, -h), new Vector3(-w, -h), new Vector3(-w, 0) };

        int start1 = Random.Range(0, 8), start2 = (start1 + 4) % 8;
        bool isCW = Random.value > 0.5f;

        Vector3[] path1 = new Vector3[9], path2 = new Vector3[9];
        for (int i = 0; i <= 8; i++)
        {
            path1[i] = points[isCW ? (start1 + i) % 8 : (start1 - i + 8) % 8];
            path2[i] = points[isCW ? (start2 + i) % 8 : (start2 - i + 8) % 8];
        }
        LaunchSymmetryEffect(path1, colors[0], symmetryEffectDuration * 2.5f, true);
        LaunchSymmetryEffect(path2, colors[1], symmetryEffectDuration * 2.5f, true);
    }

    public void PlaySymmetryAnimation(string type)
    {
        if (symmetryEffectPrefab == null || symmetryEffectContainer == null) return;
        if (GameManager_2D.Instance != null && GameManager_2D.Instance.uiManager != null) GameManager_2D.Instance.uiManager.Vibrate();
        var colors = GetRandomActiveColors(2);
        bool isReverse = Random.value > 0.5f;
        Vector3[] p1, p2;
        GetSymmetryPaths(type, isReverse, out p1, out p2);
        LaunchSymmetryEffect(p1, colors[0]);
        LaunchSymmetryEffect(p2, colors[1]);
    }

    private void LaunchSymmetryEffect(Vector3[] path, Color color, float? overrideDuration = null, bool isBlinking = false)   
    {
        PoolItem item = GetSymmetryObject();
        RectTransform rt = item.rt;
        Image img = item.img;

        if (img != null) img.color = color;
        rt.localPosition = path[0];

        float totalDist = 0f;
        for (int i = 0; i < path.Length - 1; i++) totalDist += Vector3.Distance(path[i], path[i + 1]);

        float duration = overrideDuration ?? symmetryEffectDuration;
        Sequence seq = DOTween.Sequence();
        seq.SetTarget(item.go); 
        Vector2 lastGhostPos = path[0];

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 end = path[i + 1];
            float segDist = Vector3.Distance(path[i], end);
            seq.Append(rt.DOLocalMove(end, (segDist / totalDist) * duration).SetEase(Ease.Linear).OnUpdate(() => {
                Vector2 currentPos = rt.localPosition;
                float distanceMoved = Vector2.Distance(currentPos, lastGhostPos);
                if (distanceMoved >= ghostStepDistance)
                {
                    int segments = Mathf.FloorToInt(distanceMoved / ghostStepDistance);
                    for (int j = 1; j <= segments; j++) CreateTrailGhost(Vector2.Lerp(lastGhostPos, currentPos, (float)j / segments), color);
                    lastGhostPos = currentPos;
                }
            }));
        }
        seq.SetEase(Ease.OutQuad).OnComplete(() => {
            rt.DOScale(0f, 0.2f).OnComplete(() => ReturnSymmetryObject(item));
        });
    }

    private void CreateTrailGhost(Vector2 pos, Color color)
    {
        PoolItem item = GetSymmetryObject();
        RectTransform rt = item.rt;
        Image img = item.img;

        rt.localPosition = pos;
        if (img != null) 
        {
            Color c = color;
            c.a = 1f;
            img.color = c;
            img.DOFade(0f, trailExist);
        }
        rt.DOScale(0f, trailExist).SetEase(Ease.InQuad).OnComplete(() => ReturnSymmetryObject(item));
    }

    private List<Color> GetRandomActiveColors(int count)
    {
        _tempColorList.Clear();
        for (int r = 0; r < GRID_SIZE; r++)
        {
            for (int c = 0; c < GRID_SIZE; c++)
            {
                if (!grid[r, c].IsEmpty)
                {
                    Color col = grid[r, c].BlockColor;
                    if (!_tempColorList.Contains(col)) _tempColorList.Add(col);
                }
            }
        }

        List<Color> result = new List<Color>();
        if (_tempColorList.Count == 0) { for (int i = 0; i < count; i++) result.Add(Color.white); return result; }
        for (int i = 0; i < count; i++) result.Add(_tempColorList[Random.Range(0, _tempColorList.Count)]);
        return result;
    }

    private void GetSymmetryPaths(string type, bool isReverse, out Vector3[] p1, out Vector3[] p2)
    {
        float w = symmetryEffectContainer.rect.width / 2f, h = symmetryEffectContainer.rect.height / 2f;
        Vector3 TL = new Vector3(-w, h), TR = new Vector3(w, h), BL = new Vector3(-w, -h), BR = new Vector3(w, -h);
        Vector3 TC = new Vector3(0, h), BC = new Vector3(0, -h), LC = new Vector3(-w, 0), RC = new Vector3(w, 0);

        if (!isReverse)
        {
            if (type == "H") { p1 = new[] { TC, TR, BR, BC }; p2 = new[] { TC, TL, BL, BC }; }
            else if (type == "V") { p1 = new[] { LC, TL, TR, RC }; p2 = new[] { LC, BL, BR, RC }; }
            else if (type == "D1") { p1 = new[] { TL, TR, BR }; p2 = new[] { TL, BL, BR }; }
            else { p1 = new[] { TR, TL, BL }; p2 = new[] { TR, BR, BL }; }
        }
        else
        {
            if (type == "H") { p1 = new[] { BC, BR, TR, TC }; p2 = new[] { BC, BL, TL, TC }; }
            else if (type == "V") { p1 = new[] { RC, TR, TL, LC }; p2 = new[] { RC, BR, BL, LC }; }
            else if (type == "D1") { p1 = new[] { BR, TR, TL }; p2 = new[] { BR, BL, TL }; }
            else { p1 = new[] { BL, TL, TR }; p2 = new[] { BL, BR, TR }; }
        }
    }

    public void ShakeGrid(int combo)
    {
        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        float dynamicMagnitude = shakeMagnitude + (combo * 0.5f);
        shakeCoroutine = StartCoroutine(ShakeCoroutine(dynamicMagnitude));
    }

    private IEnumerator ShakeCoroutine(float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            gridParent.localPosition = originalGridPos + (Vector3)Random.insideUnitCircle * magnitude;
            elapsed += Time.deltaTime; yield return null;
        }
        gridParent.localPosition = originalGridPos;
        shakeCoroutine = null;
    }

    private IEnumerator SequentialClear(HashSet<Cell_2D> cells)
    {
        _tempCellList.Clear();
        foreach (var cell in cells) _tempCellList.Add(cell);

        int dir = Random.Range(0, 4);
        if (dir == 0) _tempCellList.Sort((a, b) => a.gridPosition.y == b.gridPosition.y ? a.gridPosition.x.CompareTo(b.gridPosition.x) : a.gridPosition.y.CompareTo(b.gridPosition.y));
        else if (dir == 1) _tempCellList.Sort((a, b) => a.gridPosition.y == b.gridPosition.y ? a.gridPosition.x.CompareTo(b.gridPosition.x) : b.gridPosition.y.CompareTo(a.gridPosition.y));
        else if (dir == 2) _tempCellList.Sort((a, b) => a.gridPosition.x == b.gridPosition.x ? a.gridPosition.y.CompareTo(b.gridPosition.y) : a.gridPosition.x.CompareTo(b.gridPosition.x));
        else _tempCellList.Sort((a, b) => a.gridPosition.x == b.gridPosition.x ? a.gridPosition.y.CompareTo(b.gridPosition.y) : b.gridPosition.x.CompareTo(a.gridPosition.x));

        for (int i = 0; i < _tempCellList.Count; i++)
        {
            _tempCellList[i].TriggerClearAnimation();
            yield return cachedSequentialClearWait;
        }
    }

    public Vector2 GetCellPitch() => gridLayoutGroup != null ? gridLayoutGroup.cellSize + gridLayoutGroup.spacing : Vector2.one * 50f;
    public Vector2 GetCellSize() => gridLayoutGroup != null ? gridLayoutGroup.cellSize : Vector2.one * 50f;

    public Vector2Int GetGridPosition(Vector2 worldPosition)
    {
        Vector3 localPos = gridParent.InverseTransformPoint(worldPosition);
        Vector3 originLocalPos = gridParent.InverseTransformPoint(grid[0, 0].transform.position);
        Vector2 offset = (Vector2)(localPos - originLocalPos);
        Vector2 pitch = GetCellPitch();
        return new Vector2Int(Mathf.RoundToInt(offset.x / pitch.x), Mathf.RoundToInt(-offset.y / pitch.y));
    }

    public Vector2Int GetNearestValidPosition(Vector2 worldPosition, List<Vector2Int> blockShape)
    {
        Vector2Int basePos = GetGridPosition(worldPosition);
        if (IsValidPlacement(basePos, blockShape)) return basePos;
        Vector2Int nearest = new Vector2Int(-1, -1);
        float minDist = float.MaxValue;
        float snapThreshold = GetCellPitch().x * gridParent.lossyScale.x * 1.5f;
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                Vector2Int cand = new Vector2Int(basePos.x + dc, basePos.y + dr);
                if (cand.x >= 0 && cand.x < GRID_SIZE && cand.y >= 0 && cand.y < GRID_SIZE && IsValidPlacement(cand, blockShape))
                {
                    float dist = Vector2.Distance(worldPosition, grid[cand.y, cand.x].transform.position);
                    if (dist < minDist && dist < snapThreshold) { minDist = dist; nearest = cand; }
                }
            }
        return nearest;
    }

    private IEnumerator ClearPredictBlink(HashSet<Cell_2D> cells, Color blinkColor, float interval)
    {
        storedOriginalClearPredictColors.Clear();
        foreach (var cell in cells) 
            if (cell != null && !previewCells.Contains(cell) && cell.cellImage != null) 
                storedOriginalClearPredictColors[cell] = cell.cellImage.color;

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float t = (Mathf.Sin(elapsed * Mathf.PI / interval) + 1f) * 0.5f;
            foreach (var cell in cells)
                if (cell != null && !previewCells.Contains(cell) && storedOriginalClearPredictColors.TryGetValue(cell, out Color orig))
                    cell.cellImage.color = Color.Lerp(orig, blinkColor, t);
            yield return null;
        }
    }

    public void SaveBoardData_2D(int score, int combo)
    {
        SaveData_2D data = new SaveData_2D { score = score, combo = combo };
        for (int i = 0; i < GRID_SIZE * GRID_SIZE; i++) data.cellColors.Add(new SerializableColor());
        for (int r = 0; r < GRID_SIZE; r++)
            for (int c = 0; c < GRID_SIZE; c++)
            {
                int idx = r * GRID_SIZE + c;
                data.cellOccupiedStates[idx] = !grid[r, c].IsEmpty;
                data.cellColors[idx] = grid[r, c].BlockColor;
            }
        File.WriteAllText(BoardDataPath, JsonUtility.ToJson(data));
    }

    public (int score, int combo) LoadBoardData_2D()
    {
        if (!File.Exists(BoardDataPath)) return (0, 0);
        SaveData_2D data = JsonUtility.FromJson<SaveData_2D>(File.ReadAllText(BoardDataPath));
        InitializeGrid();
        while (data.cellColors.Count < GRID_SIZE * GRID_SIZE) data.cellColors.Add(new SerializableColor());
        for (int r = 0; r < GRID_SIZE; r++)
            for (int c = 0; c < GRID_SIZE; c++)
            {
                int idx = r * GRID_SIZE + c;
                if (data.cellOccupiedStates[idx]) grid[r, c].SetOccupied(data.cellColors[idx]);
                else grid[r, c].SetEmpty();
            }
        return (data.score, data.combo);
    }
}
