using UnityEngine;

// Prefab events target this component, so the asset never references a scene object.
public sealed class GameMenuPopup : MonoBehaviour
{
    private GameMenu Owner => GetComponentInParent<GameMenu>();
    public void Close() => Owner?.Close();
    public void ReturnToTitle() => Owner?.ReturnToTitle();
    public void Restart() => Owner?.Restart();
}
