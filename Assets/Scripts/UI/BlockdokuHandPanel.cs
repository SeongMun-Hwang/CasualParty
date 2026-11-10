using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class BlockdokuHandPanel : MonoBehaviour
{
    public RectTransform[] slots;
    public RectTransform board;
    [Min(16)] public float previewCellSize = 320;
    [Min(120)] public float panelHeight = 240;
    [Range(.5f, 1f)] public float heightRatio = .8f;
    private Rect lastViewport, lastSafeArea;
    private float lastScale, lastBanner = -1;
    private float lastHeight, lastCellSize;
    private float lastHeightRatio;

    private void OnEnable()
    {
        if (slots != null)
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                var target = slot.GetComponent<UnityEngine.UI.Image>() ?? slot.gameObject.AddComponent<UnityEngine.UI.Image>();
                target.color = Color.clear; target.raycastTarget = true;
                if (slot.GetComponent<BlockdokuHandSlot>() == null) slot.gameObject.AddComponent<BlockdokuHandSlot>();
            }
        Refresh();
    }
    private void LateUpdate() => Refresh();

    public static float GetReservedBannerHeight(float viewportWidth, float loadedHeight)
    {
        // Reserve the portrait banner envelope from the first frame. A smaller loaded ad
        // must not resize the hand or interrupt an active drag.
        return Mathf.Max(loadedHeight, viewportWidth * 90f / 320f);
    }

    public float GetCommonPreviewScale(Vector2 cellSize, Vector2 cellPitch)
    {
        float width = float.PositiveInfinity, height = float.PositiveInfinity;
        if (slots != null)
            foreach (var slot in slots)
                if (slot != null) { width = Mathf.Min(width, slot.rect.width - 16); height = Mathf.Min(height, slot.rect.height - 16); }
        // A common scale fits ordinary shapes up to 3 by 3; longer shapes may shrink further.
        return Mathf.Min(previewCellSize / Mathf.Max(cellSize.x, cellSize.y),
            Mathf.Max(1, width) / (cellPitch.x * 2 + cellSize.x),
            Mathf.Max(1, height) / (cellPitch.y * 2 + cellSize.y));
    }

    public static Rect GetAvailableRect(Rect viewport, Rect safe, float boardBottom, float banner, float scale)
    {
        float inset = 12 * scale;
        float left = Mathf.Max(viewport.xMin, safe.xMin) + inset;
        float right = Mathf.Min(viewport.xMax, safe.xMax) - inset;
        float bottom = Mathf.Max(viewport.yMin + banner, safe.yMin) + inset;
        float top = Mathf.Min(boardBottom, Mathf.Min(viewport.yMax, safe.yMax)) - inset;
        return new Rect(left, bottom, Mathf.Max(0, right - left), Mathf.Max(0, top - bottom));
    }

    public void Refresh()
    {
        var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (canvas == null || canvas.scaleFactor <= 0 || Screen.height <= 0) return;
        var viewport = canvas.pixelRect;
        if (viewport.width <= 0 || viewport.height <= 0) return;
        var safe = Screen.safeArea;
        if (canvas.worldCamera != null && canvas.worldCamera.targetTexture != null && Screen.width > 0)
            safe = new Rect(viewport.xMin + safe.xMin / Screen.width * viewport.width, viewport.yMin + safe.yMin / Screen.height * viewport.height,
                safe.width / Screen.width * viewport.width, safe.height / Screen.height * viewport.height);
        float scale = canvas.scaleFactor;
        float banner = GetReservedBannerHeight(viewport.width, AdManager.Instance != null ? AdManager.Instance.BannerHeightPixels : 0);
        if (lastViewport == viewport && lastSafeArea == safe && Mathf.Approximately(lastScale, scale) && Mathf.Approximately(lastBanner, banner) && lastHeight == panelHeight && lastCellSize == previewCellSize && lastHeightRatio == heightRatio) return;
        lastViewport = viewport; lastSafeArea = safe; lastScale = scale; lastBanner = banner;
        lastHeight = panelHeight; lastCellSize = previewCellSize;
        lastHeightRatio = heightRatio;
        var panel = (RectTransform)transform;
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        float boardBottom = viewport.yMin + (panelHeight + 24) * scale + banner;
        if (board != null)
        {
            var corners = new Vector3[4]; board.GetWorldCorners(corners);
            boardBottom = Mathf.Min(RectTransformUtility.WorldToScreenPoint(camera, corners[0]).y, RectTransformUtility.WorldToScreenPoint(camera, corners[3]).y);
        }
        var bounds = GetAvailableRect(viewport, safe, boardBottom, banner, scale);
        float verticalInset = bounds.height * (1 - Mathf.Clamp(heightRatio, .5f, 1)) * .5f;
        bounds.yMin += verticalInset;
        bounds.yMax -= verticalInset;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)panel.parent, bounds.center, camera, out var local)) return;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.sizeDelta = bounds.size / scale;
        panel.localPosition = new Vector3(local.x, local.y, panel.localPosition.z);
        Canvas.ForceUpdateCanvases();
        // A dragged piece is temporarily outside its slot. Include it when geometry changes.
        foreach (var piece in Object.FindObjectsByType<Block_2D>(FindObjectsSortMode.None)) piece.RefreshSpawnScale();
    }
}
