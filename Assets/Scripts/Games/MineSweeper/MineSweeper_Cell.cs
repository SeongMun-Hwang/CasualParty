using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class MineSweeper_Cell : BaseCell, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public bool IsMine { get; private set; }
    public bool IsRevealed { get; private set; }
    public bool IsFlagged { get; private set; }
    public int AdjacentMines { get; private set; }

    [SerializeField] private Image cellImage;
    [SerializeField] private Image flagImage; // Child object for flag
    [SerializeField] private Image mineImage;
    [SerializeField] private TextMeshProUGUI mineText;
    [SerializeField] private Sprite hiddenSprite;
    [SerializeField] private Sprite revealedSprite;

    private MineSweeper_GridManager gridManager;
    private Color highlightColor;
    private float longPressDuration;
    private float pointerDownTime;
    private bool isPointerDown;
    private bool longPressTriggered;

    public void Initialize(int row, int col, MineSweeper_GridManager manager, Sprite hidden, Sprite revealed, Sprite mine, Sprite flag, Color hColor, float lpDuration)
    {
        base.Initialize(row, col);
        gridManager = manager;
        hiddenSprite = hidden;
        revealedSprite = revealed;
        highlightColor = hColor;
        longPressDuration = lpDuration;

        IsMine = false;
        IsRevealed = false;
        IsFlagged = false;
        AdjacentMines = 0;

        cellImage.sprite = hiddenSprite;
        cellImage.color = Color.white;
        
        if (flagImage != null)
        {
            flagImage.sprite = flag;
            flagImage.gameObject.SetActive(false);
        }
        
        mineText.text = "";
    }

    public void SetMine(bool isMine)
    {
        IsMine = isMine;
    }

    public void SetAdjacentMines(int count)
    {
        AdjacentMines = count;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (MineSweeper_GameManager.Instance.IsGameOver || GameNavigation.IsTransitioning) return;

        isPointerDown = true;
        pointerDownTime = Time.time;
        longPressTriggered = false;

        if (IsRevealed && AdjacentMines > 0)
        {
            gridManager.HighlightNeighbors(gridPosition.y, gridPosition.x, true);
        }
        else if (!IsRevealed && !IsFlagged)
        {
            // Just tint the current sprite (hiddenSprite)
            cellImage.color = highlightColor;
        }

        StartCoroutine(LongPressRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isPointerDown || MineSweeper_GameManager.Instance.IsGameOver) return;

        isPointerDown = false;
        
        if (IsRevealed && AdjacentMines > 0)
        {
            gridManager.HighlightNeighbors(gridPosition.y, gridPosition.x, false);
            gridManager.AttemptChord(gridPosition.y, gridPosition.x);
        }
        else if (!longPressTriggered)
        {
            if (!IsFlagged && !IsRevealed)
            {
                gridManager.OnCellClicked(gridPosition.y, gridPosition.x);
            }
            else if (!IsRevealed)
            {
                // Reset tint
                cellImage.color = Color.white;
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPointerDown)
        {
            isPointerDown = false;
            if (IsRevealed && AdjacentMines > 0)
            {
                gridManager.HighlightNeighbors(gridPosition.y, gridPosition.x, false);
            }
            else if (!IsRevealed)
            {
                cellImage.color = Color.white;
            }
        }
    }

    private IEnumerator LongPressRoutine()
    {
        while (isPointerDown && !longPressTriggered &&
               !MineSweeper_GameManager.Instance.IsGameOver && !GameNavigation.IsTransitioning)
        {
            if (Time.time - pointerDownTime >= longPressDuration)
            {
                longPressTriggered = true;
                if (!IsRevealed)
                {
                    ToggleFlag();
                    Vibrate();
                }
            }
            yield return null;
        }
    }

    public void Reveal()
    {
        if (IsRevealed) return;
        IsRevealed = true;
        IsFlagged = false;

        if (flagImage != null) flagImage.gameObject.SetActive(false);
        cellImage.color = Color.white;
        cellImage.sprite = revealedSprite;

        if (IsMine)
        {
            mineImage.gameObject.SetActive(IsMine);
            mineText.text = "";
        }
        else
        {
            if (AdjacentMines > 0)
            {
                mineText.text = AdjacentMines.ToString();
                mineText.color = GetMineCountColor(AdjacentMines);
            }
            else
            {
                mineText.text = "";
            }
        }
    }

    public void SetHighlight(bool highlight)
    {
        if (IsRevealed) return;
        if (highlight)
        {
            if (!IsFlagged)
            {
                cellImage.color = highlightColor;
            }
        }
        else
        {
            cellImage.color = Color.white;
        }
    }

    private void ToggleFlag()
    {
        IsFlagged = !IsFlagged;
        if (flagImage != null)
        {
            flagImage.gameObject.SetActive(IsFlagged);
        }
        // Tint is reset when flagging
        cellImage.color = Color.white;
        gridManager.OnFlagToggled(IsFlagged);
    }

    private void Vibrate()
    {
        if (SettingsManager.Instance != null)
        {
            SettingsManager.Instance.Vibrate();
        }
    }

    private Color GetMineCountColor(int count)
    {
        switch (count)
        {
            case 1: return Color.blue;
            case 2: return new Color(0, 0.5f, 0);
            case 3: return Color.red;
            case 4: return new Color(0, 0, 0.5f);
            case 5: return new Color(0.5f, 0, 0);
            case 6: return Color.cyan;
            case 7: return Color.black;
            case 8: return Color.gray;
            default: return Color.white;
        }
    }
}
