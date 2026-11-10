using UnityEngine;
using UnityEngine.EventSystems;

// The entire waiting slot is a touch target, independent of the preview's visual scale.
public sealed class BlockdokuHandSlot : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private Block_2D grabbedPiece;
    private int pointerId;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (grabbedPiece != null && grabbedPiece.IsDragging) return;
        grabbedPiece = null;
        var piece = GetComponentInChildren<Block_2D>();
        if (piece == null) return;
        piece.OnPointerDown(eventData);
        if (!piece.IsDragging) return;
        grabbedPiece = piece;
        pointerId = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (grabbedPiece != null && eventData.pointerId == pointerId) grabbedPiece.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (grabbedPiece == null || eventData.pointerId != pointerId) return;
        var piece = grabbedPiece; grabbedPiece = null;
        piece.OnPointerUp(eventData);
    }

    private void OnDisable() => CancelGrab(true);
    private void OnApplicationFocus(bool focused) { if (!focused) CancelGrab(); }
    private void CancelGrab(bool deferRestore = false)
    {
        var piece = grabbedPiece; grabbedPiece = null;
        if (piece != null) piece.CancelDrag(deferRestore);
    }
}
