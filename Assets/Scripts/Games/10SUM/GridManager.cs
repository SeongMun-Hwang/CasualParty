
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

public class GridManager : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public GameObject cellPrefab; // Assign a UI Image prefab for the cell
    public RectTransform gridParent; // The RectTransform of the panel this script is attached to
    public Sprite appleSprite; // Assign the apple sprite
    public Color highlightColor = new Color(92f / 255f, 92f / 255f, 92f / 255f); // Color when selected

    private int[,] grid;
    private GameObject[,] cellObjects;

    private const int GRID_WIDTH = 10;
    private const int GRID_HEIGHT = 17;

    private List<Cell> selectedCells = new List<Cell>();
    private Vector2Int dragStartCoords;
    private bool isDragging = false;

    private GridLayoutGroup gridLayout;

    void Awake()
    {
        gridLayout = GetComponent<GridLayoutGroup>();
        if (gridParent == null) gridParent = GetComponent<RectTransform>();
    }

    public void InitializeGrid()
    {
        // Clear existing children
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        grid = new int[GRID_HEIGHT, GRID_WIDTH];
        cellObjects = new GameObject[GRID_HEIGHT, GRID_WIDTH];

        List<int> numbers = new List<int>();
        int totalCells = GRID_WIDTH * GRID_HEIGHT;
        int baseCount = totalCells / 9;
        int remainder = totalCells % 9;

        for (int num = 1; num <= 9; num++)
        {
            for (int i = 0; i < baseCount; i++)
            {
                numbers.Add(num);
            }
        }

        for (int i = 0; i < remainder; i++)
        {
            numbers.Add((i % 9) + 1);
        }

        System.Random rng = new System.Random();
        int n = numbers.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            int value = numbers[k];
            numbers[k] = numbers[n];
            numbers[n] = value;
        }

        int numberIndex = 0;
        for (int r = 0; r < GRID_HEIGHT; r++)
        {
            for (int c = 0; c < GRID_WIDTH; c++)
            {
                grid[r, c] = numbers[numberIndex++];
                CreateCellObject(r, c, grid[r, c]);
            }
        }
    }

    private void CreateCellObject(int row, int col, int value)
    {
        GameObject cellGO = Instantiate(cellPrefab, transform);
        cellGO.name = $"Cell_{row}_{col}";

        Image image = cellGO.GetComponent<Image>();
        if (image != null) image.sprite = appleSprite;

        TextMeshProUGUI textMesh = cellGO.GetComponentInChildren<TextMeshProUGUI>();
        if (textMesh != null) textMesh.text = value.ToString();

        Cell cellComponent = cellGO.AddComponent<Cell>();
        cellComponent.Initialize(row, col);

        cellObjects[row, col] = cellGO;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (TENSUM_GameManager.Instance.IsGameOver() || GameNavigation.IsTransitioning) return;
        isDragging = true;
        dragStartCoords = GetCoordsFromPointer(eventData);
        UpdateSelection(dragStartCoords);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isDragging)
        {
            Vector2Int currentCoords = GetCoordsFromPointer(eventData);
            UpdateSelection(currentCoords);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging)
        {
            isDragging = false;
            ProcessSelectedCells();
            ClearSelectedCellsVisual();
            selectedCells.Clear();
        }
    }

    private Vector2Int GetCoordsFromPointer(PointerEventData eventData)
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            gridParent,
            eventData.position,
            eventData.pressEventCamera,
            out localPos
        );

        // (0,0) 셀의 좌상단 좌표 구하기
        RectTransform firstCellRect = cellObjects[0, 0].GetComponent<RectTransform>();
        Vector2 firstCellPos = firstCellRect.localPosition;
        firstCellPos.x -= firstCellRect.rect.width / 2f;
        firstCellPos.y += firstCellRect.rect.height / 2f; // Y 위쪽 기준

        // 셀 크기와 간격을 실제 위치에서 계산
        float cellWidth = Vector2.Distance(
            cellObjects[0, 0].GetComponent<RectTransform>().localPosition,
            cellObjects[0, 1].GetComponent<RectTransform>().localPosition
        );
        float cellHeight = Vector2.Distance(
            cellObjects[0, 0].GetComponent<RectTransform>().localPosition,
            cellObjects[1, 0].GetComponent<RectTransform>().localPosition
        );

        // localPos → 그리드 좌표 변환
        float relativeX = localPos.x - firstCellPos.x;
        float relativeY = firstCellPos.y - localPos.y; // Y 반전

        int col = Mathf.FloorToInt(relativeX / cellWidth);
        int row = Mathf.FloorToInt(relativeY / cellHeight);

        col = Mathf.Clamp(col, 0, GRID_WIDTH - 1);
        row = Mathf.Clamp(row, 0, GRID_HEIGHT - 1);

        return new Vector2Int(col, row);
    }

    private void UpdateSelection(Vector2Int endCoords)
    {
        ClearSelectedCellsVisual();
        selectedCells.Clear();

        int minCol = Mathf.Min(dragStartCoords.x, endCoords.x);
        int maxCol = Mathf.Max(dragStartCoords.x, endCoords.x);
        int minRow = Mathf.Min(dragStartCoords.y, endCoords.y);
        int maxRow = Mathf.Max(dragStartCoords.y, endCoords.y);

        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                if (cellObjects[r, c] != null)
                {
                    Cell cell = cellObjects[r, c].GetComponent<Cell>();
                    if (cell != null && cell.enabled)
                    {
                        selectedCells.Add(cell);
                        HighlightCell(cell, true);
                    }
                }
            }
        }
    }

    private void HighlightCell(Cell cell, bool highlight)
    {
        if (cell != null)
        {
            Image img = cell.GetComponent<Image>();
            if (img != null)
            {
                img.color = highlight ? highlightColor : Color.white;
            }
        }
    }

    private void ClearSelectedCellsVisual()
    {
        foreach (Cell cell in selectedCells)
        {
            HighlightCell(cell, false);
        }
    }

    private void ProcessSelectedCells()
    {
        if (TENSUM_GameManager.Instance.IsGameOver() || GameNavigation.IsTransitioning) return;
        if (selectedCells.Count == 0) return;

        int sum = 0;
        foreach (Cell cell in selectedCells)
        {
            if (grid[cell.gridPosition.y, cell.gridPosition.x] != 0)
            {
                sum += grid[cell.gridPosition.y, cell.gridPosition.x];
            }
        }

        if (sum == 10)
        {
            int applesRemoved = 0;
            foreach (Cell cell in selectedCells)
            {
                int row = cell.gridPosition.y;
                int col = cell.gridPosition.x;

                if (grid[row, col] != 0)
                {
                    grid[row, col] = 0;
                    applesRemoved++;

                    cell.PlayClearAnimation();
                }
            }
            if (applesRemoved > 0)
            {
                TENSUM_GameManager.Instance.AddScore(applesRemoved);
            }
        }
    }
}
