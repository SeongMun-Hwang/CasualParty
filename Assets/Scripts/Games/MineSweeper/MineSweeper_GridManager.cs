using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MineSweeper_GridManager : MonoBehaviour
{
    public GameObject cellPrefab;
    public RectTransform gridParent;
    public Sprite hiddenSprite;
    public Sprite revealedSprite;
    public Sprite mineSprite;
    public Sprite flagSprite;
    public Color cellHighlightColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public float longPressDuration = 0.5f;

    private int rows;
    private int cols;
    private int mineCount;
    private MineSweeper_Cell[,] cells;
    private bool minesGenerated = false;
    private int revealedCellCount = 0;

    private GridLayoutGroup gridLayout;

    void Awake()
    {
        if (gridParent == null) gridParent = GetComponent<RectTransform>();
        
        if (gridParent != null)
        {
            gridLayout = gridParent.GetComponent<GridLayoutGroup>();
        }
    }

    public void InitializeGrid(int r, int c, int m)
    {
        rows = r;
        cols = c;
        mineCount = m;
        minesGenerated = false;
        revealedCellCount = 0;

        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        cells = new MineSweeper_Cell[rows, cols];
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = cols;

        AdjustCellSize();

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                GameObject cellGO = Instantiate(cellPrefab, gridParent);
                MineSweeper_Cell cell = cellGO.GetComponent<MineSweeper_Cell>();
                cell.Initialize(row, col, this, hiddenSprite, revealedSprite, mineSprite, flagSprite, cellHighlightColor, longPressDuration);
                cells[row, col] = cell;
            }
        }
    }

    private void AdjustCellSize()
    {
        // Force update to get correct rect dimensions if layout just changed
        Canvas.ForceUpdateCanvases();

        float parentWidth = gridParent.rect.width;
        float parentHeight = gridParent.rect.height;

        float availableWidth = parentWidth - gridLayout.padding.left - gridLayout.padding.right - (gridLayout.spacing.x * (cols - 1));
        float availableHeight = parentHeight - gridLayout.padding.top - gridLayout.padding.bottom - (gridLayout.spacing.y * (rows - 1));

        float cellW = availableWidth / cols;
        float cellH = availableHeight / rows;

        // Use the smaller value to ensure cells are square and fit within both dimensions
        float size = Mathf.Min(cellW, cellH);
        
        // Ensure minimum size for playability
        size = Mathf.Max(size, 20f); 

        gridLayout.cellSize = new Vector2(size, size);
    }

    public void OnCellClicked(int row, int col)
    {
        if (MineSweeper_GameManager.Instance.IsGameOver || GameNavigation.IsTransitioning) return;
        if (!minesGenerated)
        {
            GenerateMines(row, col);
            minesGenerated = true;
            MineSweeper_GameManager.Instance.OnGameStarted();
        }

        RevealCell(row, col);
    }

    public void OnFlagToggled(bool isFlagged)
    {
        MineSweeper_GameManager.Instance.OnFlagToggled(isFlagged);
    }

    public void HighlightNeighbors(int row, int col, bool highlight)
    {
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = row + dr;
                int nc = col + dc;
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                {
                    cells[nr, nc].SetHighlight(highlight);
                }
            }
        }
    }

    public void AttemptChord(int row, int col)
    {
        if (MineSweeper_GameManager.Instance.IsGameOver || GameNavigation.IsTransitioning) return;
        MineSweeper_Cell cell = cells[row, col];
        if (!cell.IsRevealed || cell.AdjacentMines == 0) return;

        int flagCount = 0;
        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = row + dr;
                int nc = col + dc;
                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && cells[nr, nc].IsFlagged)
                {
                    flagCount++;
                }
            }
        }

        if (flagCount == cell.AdjacentMines)
        {
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = row + dr;
                    int nc = col + dc;
                    if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    {
                        if (!cells[nr, nc].IsRevealed && !cells[nr, nc].IsFlagged)
                        {
                            RevealCell(nr, nc);
                        }
                    }
                }
            }
        }
    }

    private void GenerateMines(int startRow, int startCol)
    {
        int minesToPlace = mineCount;
        while (minesToPlace > 0)
        {
            int r = Random.Range(0, rows);
            int c = Random.Range(0, cols);

            // Don't place mine at start location or if already a mine
            if ((r == startRow && c == startCol) || cells[r, c].IsMine) continue;
            
            // Also avoid placing mines directly adjacent to the first click for better experience
            if (Mathf.Abs(r - startRow) <= 1 && Mathf.Abs(c - startCol) <= 1) continue;

            cells[r, c].SetMine(true);
            minesToPlace--;
        }

        // Calculate adjacent mine counts
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (cells[r, c].IsMine) continue;

                int count = 0;
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = r + dr;
                        int nc = c + dc;
                        if (nr >= 0 && nr < rows && nc >= 0 && nc < cols && cells[nr, nc].IsMine)
                        {
                            count++;
                        }
                    }
                }
                cells[r, c].SetAdjacentMines(count);
            }
        }
    }

    private void RevealCell(int row, int col)
    {
        if (MineSweeper_GameManager.Instance.IsGameOver || GameNavigation.IsTransitioning) return;
        if (row < 0 || row >= rows || col < 0 || col >= cols) return;
        MineSweeper_Cell startCell = cells[row, col];
        if (startCell.IsRevealed || startCell.IsFlagged) return;

        if (startCell.IsMine)
        {
            startCell.Reveal();
            MineSweeper_GameManager.Instance.GameOver(false);
            RevealAllMines();
            return;
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        HashSet<Vector2Int> enqueued = new HashSet<Vector2Int>();
        
        queue.Enqueue(new Vector2Int(col, row));
        enqueued.Add(new Vector2Int(col, row));

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int r = current.y;
            int c = current.x;

            MineSweeper_Cell cell = cells[r, c];
            if (cell.IsRevealed || cell.IsFlagged) continue;

            cell.Reveal();
            revealedCellCount++;

            if (cell.AdjacentMines == 0)
            {
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = r + dr;
                        int nc = c + dc;
                        if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                        {
                            Vector2Int neighbor = new Vector2Int(nc, nr);
                            if (!cells[nr, nc].IsRevealed && !cells[nr, nc].IsFlagged && !enqueued.Contains(neighbor))
                            {
                                queue.Enqueue(neighbor);
                                enqueued.Add(neighbor);
                            }
                        }
                    }
                }
            }
        }

        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        if (revealedCellCount == (rows * cols) - mineCount)
        {
            MineSweeper_GameManager.Instance.GameOver(true);
        }
    }

    private void RevealAllMines()
    {
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (cells[r, c].IsMine)
                {
                    cells[r, c].Reveal();
                }
            }
        }
    }
}
