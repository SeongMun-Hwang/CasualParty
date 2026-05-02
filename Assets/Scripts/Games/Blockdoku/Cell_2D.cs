using UnityEngine;
using UnityEngine.UI;

public class Cell_2D : BaseCell
{
    public bool IsPreviewing { get; private set; }
    public bool IsClearing { get; private set; }
    public Color BlockColor { get; private set; } 

    public Image cellImage;
    private Animator animator;

    void Awake()
    {
        if (cellImage == null)
        {
            cellImage = GetComponent<Image>();
        }
        animator = GetComponent<Animator>();
    }

    public void ClearLogically()
    {
        IsClearing = true;
        base.SetEmpty(); // Mark as logically empty immediately
        BlockColor = Color.clear;
    }

    public void TriggerClearAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger("Clear");
        }
    }

    public override void Initialize(int row, int col, bool isEmpty)
    {
        base.Initialize(row, col, isEmpty);
        IsPreviewing = false;
        IsClearing = false;
        BlockColor = Color.clear; 

        // 초기화 시에는 즉시 시각적 상태 반영
        if (IsEmpty) UpdateVisualsToEmpty(); else SetOccupied(BlockColor); 
    }

    public void SetOccupied(Color color)
    {
        base.SetOccupied(); // Set IsEmpty = false via BaseCell
        IsClearing = false;
        BlockColor = color; 

        if (animator != null)
        {
            animator.Play("New State", 0, 0f); // Reset to idle state
        }

        if (cellImage != null && GridManager_2D.Instance != null)
        {
            cellImage.sprite = GridManager_2D.Instance.defaultOccupiedCellSprite;
            cellImage.color = BlockColor; // Apply the block's color
            transform.localScale = Vector3.one; 
        }
    }

    // 기존 SetEmpty 로직을 시각적 업데이트와 논리 업데이트로 명확히 분리
    public void SetEmpty()
    {
        if (!IsClearing && !IsEmpty) return;

        IsClearing = false;
        base.SetEmpty(); 
        BlockColor = Color.clear;

        UpdateVisualsToEmpty();
    }

    private void UpdateVisualsToEmpty()
    {
        if (cellImage != null && GridManager_2D.Instance != null)
        {
            cellImage.sprite = GridManager_2D.Instance.defaultEmptyCellSprite;
            cellImage.color = Color.white;
            transform.localScale = Vector3.one;
        }
    }

    public void SetPreview(Color previewColor)
    {
        if (IsEmpty)
        {
            IsPreviewing = true;
            cellImage.color = previewColor;
        }
    }

    public void ClearPreview()
    {
        if (IsPreviewing)
        {
            IsPreviewing = false;
            if (IsEmpty)
            {
                SetEmpty(); // Restore empty visuals
            }
            else
            {
                SetOccupied(BlockColor); // Restore occupied visuals with its stored color
            }
        }
    }
}
