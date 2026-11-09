using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class StackTowerRotationHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public StackTowerGame game;
    public bool clockwise;
    public bool IsHeld { get; private set; }
    private int pointerId;
    private Button button;

    private void Awake() => button = GetComponent<Button>();
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || IsHeld || game == null || !game.CanRotate || !button.IsInteractable()) return;
        pointerId = eventData.pointerId;
        IsHeld = true;
    }
    public void OnPointerUp(PointerEventData eventData) { if (IsHeld && eventData.pointerId == pointerId) ReleaseHold(); }
    public void OnPointerExit(PointerEventData eventData) { if (IsHeld && eventData.pointerId == pointerId) ReleaseHold(); }
    private void ReleaseHold()
    {
        CancelHold();
        if (game != null) game.OnRotationReleased();
    }
    public void CancelHold() => IsHeld = false;
    private void Update() { if (IsHeld && (game == null || !game.CanRotate || !button.IsInteractable())) CancelHold(); }
    private void OnDisable() => CancelHold();
    private void OnApplicationFocus(bool focused) { if (!focused) CancelHold(); }
}
