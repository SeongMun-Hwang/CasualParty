using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaTopPanel : MonoBehaviour
{
    [Min(0)] public float topPadding = 16f;
    [SerializeField] private Image panelBackground;
    public Image BackgroundImage => panelBackground;
    private RectTransform panel;
    private Canvas canvas;
    private Rect lastSafeArea;
    private Rect lastViewport;
    private float lastPadding = -1;

    private void OnEnable()
    {
        panel = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        Refresh();
        RefreshBackground();
    }

    private void LateUpdate() { Refresh(); RefreshBackground(); }

    public void RefreshBackground()
    {
        if (panel == null) panel = (RectTransform)transform;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var source = GetComponent<Image>();
        if (source == null) return;
        if (panelBackground == null)
        {
            if (!source.enabled) return;
            var go = new GameObject("Safe Area Background", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            panelBackground = go.GetComponent<Image>();
        }
        source.enabled = false;
        panelBackground.color = source.color;
        panelBackground.sprite = source.sprite;
        panelBackground.type = source.type;
        panelBackground.material = source.material;
        panelBackground.preserveAspect = source.preserveAspect;
        panelBackground.raycastTarget = false;
        var viewport = canvas.rootCanvas.pixelRect;
        if (viewport.width <= 0 || viewport.height <= 0) return;
        var uiCamera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, new Vector2(viewport.xMin, viewport.yMax), uiCamera, out var topLeft) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, new Vector2(viewport.xMax, viewport.yMax), uiCamera, out var topRight)) return;
        var rect = panelBackground.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(topLeft.x - panel.rect.xMin, 0);
        rect.offsetMax = new Vector2(topRight.x - panel.rect.xMax, topRight.y - panel.rect.yMax);
    }

    private void Refresh()
    {
        if (canvas == null) return;
        var viewport = canvas.rootCanvas.pixelRect;
        var safeArea = Screen.safeArea;
        if (viewport.width <= 0 || viewport.height <= 0) return;
        if (safeArea == lastSafeArea && viewport == lastViewport && topPadding == lastPadding) return;
        lastSafeArea = safeArea; lastViewport = viewport; lastPadding = topPadding;
        GetTopAnchors(safeArea, viewport, out var min, out var max);
        panel.anchorMin = min; panel.anchorMax = max;
        panel.pivot = new Vector2(.5f, 1);
        panel.anchoredPosition = new Vector2(0, -topPadding);
        panel.sizeDelta = new Vector2(0, panel.sizeDelta.y);
    }

    public static void GetTopAnchors(Rect safeArea, Rect viewport, out Vector2 min, out Vector2 max)
    {
        // Screen.safeArea is in physical pixels; anchors follow the CanvasScaler at any resolution.
        if (viewport.width <= 0 || viewport.height <= 0 || safeArea.width <= 0 || safeArea.height <= 0)
        {
            min = new Vector2(0, 1); max = Vector2.one;
            return;
        }
        float left = Mathf.Clamp01((safeArea.xMin - viewport.xMin) / viewport.width);
        float right = Mathf.Clamp01((safeArea.xMax - viewport.xMin) / viewport.width);
        float top = Mathf.Clamp01((safeArea.yMax - viewport.yMin) / viewport.height);
        min = new Vector2(left, top); max = new Vector2(Mathf.Max(left, right), top);
    }
}
