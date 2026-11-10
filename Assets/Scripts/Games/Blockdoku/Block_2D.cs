using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI; // Added for Image component

public class Block_2D : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Data")]
    [SerializeField] private BlockArray blockData;
    public BlockArray BlockData { get { return blockData; } } // Public getter for blockData
    [SerializeField] private GameObject cellPrefab; // The prefab for a single visual cell
    public Color blockColor; // New: Stores the color of this block

    [Header("Interaction")]
    [SerializeField] private float dragMovementMultiplier = 1.2f; 
    [SerializeField] private float spawnScale = 1f;
    [SerializeField] private float dragScale = 1.2f;
    [SerializeField] private float dragYOffset = 150.0f; // Offset in pixels/units to move block above finger

    private Vector3 originalScale;
    private Vector3 grabWorldSpaceOffset;
    private List<Vector2Int> shape;
    private RectTransform rectTransform;
    private Canvas canvas;
    private Vector2 originalPosition;
    private Transform originalParent;
    private Vector2Int lastGridPosition;
    private int currentRotationStep; // New field to store rotation step
    public int CurrentRotationStep { get { return currentRotationStep; } } // Public getter
    public Transform SpawnParent { get; private set; }
    private bool isDragging;
    private bool restorePending;
    public bool IsDragging => isDragging;

    private readonly List<Transform> childCubes = new List<Transform>();
    private Vector2 anchorOffsetPixels;

    /// <summary>
    /// Initializes the block with data, loads its shape, and applies rotation.
    /// This should be called by the spawner right after instantiation.
    /// </summary>
    public void Initialize(BlockArray data, int rotationCount, Color color)
    {
        rectTransform = GetComponent<RectTransform>();
        blockData = data;
        blockColor = color; 
        currentRotationStep = rotationCount;
        SpawnParent = transform.parent;
        
        // Apply initial spawn scale
        rectTransform.localScale = Vector3.one * spawnScale;
        originalScale = Vector3.one * spawnScale;

        shape = blockData.GetShape(rotationCount);
        UpdateVisuals();
        RefreshSpawnScale();
        if (SpawnParent.GetComponent<BlockdokuHandSlot>() != null)
            foreach (var graphic in GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        SetColor(blockColor);
    }


    /// <summary>
    /// Clears existing visuals and creates new ones based on the current 'shape' data.
    /// It automatically centers the final visual shape and matches its cell size to the grid.
    /// </summary>
    private void UpdateVisuals()
    {
        // Clear old visuals
        foreach (Transform child in childCubes)
        {
            Destroy(child.gameObject);
        }
        childCubes.Clear();

        if (shape == null || shape.Count == 0 || GridManager_2D.Instance == null) return;
        
        // --- Centering Logic ---
        // Find the bounds of the current shape
        float minX = shape.Min(p => p.x);
        float maxX = shape.Max(p => p.x);
        float minY = shape.Min(p => p.y);
        float maxY = shape.Max(p => p.y);

        // The center of the shape is the average of its min/max bounds
        Vector2 shapeCenter = new Vector2((minX + maxX) / 2.0f, (minY + maxY) / 2.0f);
        // --- End of Centering Logic ---

        Vector2 cellPitch = GridManager_2D.Instance.GetCellPitch();
        Vector2 cellSize = GridManager_2D.Instance.GetCellSize();

        // Create and position new visuals
        foreach (Vector2Int pos in shape)
        {
            GameObject newCell = Instantiate(cellPrefab, transform);
            RectTransform cellRect = newCell.GetComponent<RectTransform>();

            // Set the size of the cell to match the grid
            cellRect.sizeDelta = cellSize;

            // Position relative to the calculated center
            Vector2 centeredPos = new Vector2(pos.x - shapeCenter.x, pos.y - shapeCenter.y);
            cellRect.anchoredPosition = centeredPos * cellPitch;
            
            childCubes.Add(newCell.transform);
        }

        // --- Calculate anchorOffsetPixels ---
        if (shape.Count > 0)
        {
            float maxAnchorY = shape.Max(p => p.y); // For consistency, though now we're using Vector2Int.zero
            
            // Define the anchor cell as (0,0) in the block's local coordinate system (fixed origin)
            Vector2Int anchorCellLocal = Vector2Int.zero; 

            // Calculate the position of this anchor cell relative to the block's *current* pivot
            // (which is where the shapeCenter is adjusted to)
            // The anchorOffsetPixels represents the shift from the block's (container's) pivot
            // to the center of the designated anchorCellLocal.
            anchorOffsetPixels = new Vector2(anchorCellLocal.x - shapeCenter.x, anchorCellLocal.y - shapeCenter.y) * cellPitch;
        }
        else
        {
            anchorOffsetPixels = Vector2.zero;
        }
    }

    public List<Vector2Int> GetShape()
    {
        return shape;
    }

    public void RefreshSpawnScale()
    {
        if (!(SpawnParent is RectTransform slot) || shape == null || shape.Count == 0 || GridManager_2D.Instance == null) return;
        var hand = slot.GetComponentInParent<BlockdokuHandPanel>();
        if (hand == null) return;
        CancelDrag();
        var pitch = GridManager_2D.Instance.GetCellPitch();
        var size = GridManager_2D.Instance.GetCellSize();
        float width = (shape.Max(p => p.x) - shape.Min(p => p.x)) * pitch.x + size.x;
        float height = (shape.Max(p => p.y) - shape.Min(p => p.y)) * pitch.y + size.y;
        float scale = Mathf.Min(hand.GetCommonPreviewScale(size, pitch),
            Mathf.Max(1, slot.rect.width - 16) / width, Mathf.Max(1, slot.rect.height - 16) / height);
        originalScale = Vector3.one * scale;
        rectTransform.localScale = Vector3.one * scale;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Sets the color of all individual cells within this block.
    /// </summary>
    public void SetColor(Color color)
    {
        blockColor = color; // Also update the stored blockColor
        foreach (Transform child in childCubes)
        {
            Image cellImage = child.GetComponent<Image>();
            if (cellImage != null)
            {
                cellImage.color = color;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (GameNavigation.IsTransitioning || RankingOverlay.IsOpen || GameManager_2D.Instance == null || GameManager_2D.Instance.IsGameOver() ||
            BlockSpawner_2D.Instance == null || !BlockSpawner_2D.Instance.TryBeginDrag(this)) return;
        isDragging = true;
        originalPosition = rectTransform.anchoredPosition;
        originalParent = transform.parent;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        
        lastGridPosition = new Vector2Int(-1, -1); // Initialize lastGridPosition

        if (GridManager_2D.Instance != null)
        {
            GridManager_2D.Instance.ClearPreview();
        }

        // Bring to front
        transform.SetParent(canvas.transform);
        transform.SetAsLastSibling();

        // Scale up for dragging
        rectTransform.localScale = Vector3.one * dragScale;

        // Apply initial Y offset to lift it above finger
        rectTransform.anchoredPosition += new Vector2(0, dragYOffset);

        grabWorldSpaceOffset = Vector3.zero;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RankingOverlay.IsOpen) { CancelDrag(); return; }
        if (!isDragging) return;
        // Apply movement with multiplier
        rectTransform.anchoredPosition += (eventData.delta / canvas.scaleFactor) * dragMovementMultiplier;

        if (GridManager_2D.Instance != null)
        {
            Vector3 checkPosition = rectTransform.TransformPoint(anchorOffsetPixels);
            Vector2Int gridPosition = GridManager_2D.Instance.GetNearestValidPosition(checkPosition, shape);
            if (gridPosition != lastGridPosition)
            {
                lastGridPosition = gridPosition;
                GridManager_2D.Instance.ShowPreview(gridPosition, shape);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (RankingOverlay.IsOpen) { CancelDrag(); return; }
        if (!isDragging) return;
        isDragging = false;
        BlockSpawner_2D.Instance.EndDrag(this);
        if (GridManager_2D.Instance != null)
        {
            GridManager_2D.Instance.ClearPreview();
            Vector3 checkPosition = rectTransform.TransformPoint(anchorOffsetPixels);
            Vector2Int gridPosition = GridManager_2D.Instance.GetNearestValidPosition(checkPosition, shape);

            // If GetNearestValidPosition found a valid spot (it returns -1, -1 if not found)
            if (gridPosition.x != -1 && gridPosition.y != -1)
            {
                if (AudioManager_2D.Instance != null) AudioManager_2D.Instance.PlayBlockThudAudio();
                BlockSpawner_2D.Instance.BlockPlaced(gameObject, gridPosition, shape, blockColor);
                
                // Hide or disable the block instead of immediate Destroy to avoid issues while the routine starts
                gameObject.SetActive(false);
                Destroy(gameObject, 0.1f); 
            }
            else
            {
                // Return to original position and scale if placement is invalid
                transform.SetParent(originalParent);
                rectTransform.localScale = (Vector3)originalScale;
                rectTransform.anchoredPosition = originalPosition;
            }
        }
        else
        {
             // Fallback if GridManager is not found
            transform.SetParent(originalParent);
            rectTransform.localScale = (Vector3)originalScale;
            rectTransform.anchoredPosition = originalPosition;
        }
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) CancelDrag();
    }

    private void OnDisable() => CancelDrag(true);
    private void OnEnable() { if (restorePending) StartCoroutine(RestoreAfterDisable()); }

    public void CancelDrag(bool deferRestore = false)
    {
        if (!isDragging) return;
        isDragging = false;
        if (BlockSpawner_2D.Instance != null) BlockSpawner_2D.Instance.EndDrag(this);
        if (GridManager_2D.Instance != null) GridManager_2D.Instance.ClearPreview();
        if (originalParent == null) return;
        if (deferRestore)
        {
            restorePending = true;
            if (isActiveAndEnabled) StartCoroutine(RestoreAfterDisable());
            return;
        }
        RestoreDragPosition();
    }

    private System.Collections.IEnumerator RestoreAfterDisable()
    {
        yield return null; // Unity disallows reparenting into a hierarchy while it is deactivating.
        if (originalParent != null) RestoreDragPosition();
    }

    private void RestoreDragPosition()
    {
        restorePending = false;
        transform.SetParent(originalParent, false);
        rectTransform.localScale = (Vector3)originalScale;
        rectTransform.anchoredPosition = originalPosition;
    }
}
