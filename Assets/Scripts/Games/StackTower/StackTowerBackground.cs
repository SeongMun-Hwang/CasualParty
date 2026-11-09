using UnityEngine;

[ExecuteAlways]
public sealed class StackTowerBackground : MonoBehaviour
{
    public Camera targetCamera;
    public Sprite[] zoneSprites;
    public SpriteRenderer primaryLayer, transitionLayer;
    [Tooltip("Camera ascent in world units from the ground zone to space.")]
    [Min(1)] public float heightToSpace = 120f;
    [Tooltip("Fraction of each zone spent holding its image before fading to the next zone.")]
    [Range(0, .9f)] public float holdFraction = .55f;
    [SerializeField] private float groundCameraY;
    public float AltitudeProgress { get; private set; }
    public int CurrentZoneIndex { get; private set; }
    public float TransitionAmount { get; private set; }
#if UNITY_EDITOR
    public bool IsPreviewing { get; private set; }
    private float previewProgress;
    public void SetPreview(float progress) { IsPreviewing = true; previewProgress = Mathf.Clamp01(progress); Refresh(); }
    public void ClearPreview() { IsPreviewing = false; Refresh(); }
#endif

    public void ResetAltitude(float cameraY)
    {
        groundCameraY = cameraY;
#if UNITY_EDITOR
        IsPreviewing = false;
#endif
        Refresh();
    }

    private void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (targetCamera == null || !targetCamera.orthographic || zoneSprites == null || zoneSprites.Length == 0 || primaryLayer == null || transitionLayer == null) return;
        float progress = Mathf.Clamp01((targetCamera.transform.position.y - groundCameraY) / Mathf.Max(1, heightToSpace));
#if UNITY_EDITOR
        if (IsPreviewing) progress = previewProgress;
#endif
        AltitudeProgress = progress;
        float zonePosition = progress * (zoneSprites.Length - 1);
        CurrentZoneIndex = Mathf.Min(Mathf.FloorToInt(zonePosition), zoneSprites.Length - 1);
        int nextIndex = Mathf.Min(CurrentZoneIndex + 1, zoneSprites.Length - 1);
        float phase = zonePosition - CurrentZoneIndex;
        TransitionAmount = nextIndex == CurrentZoneIndex ? 0 : Mathf.SmoothStep(0, 1,
            Mathf.InverseLerp(Mathf.Clamp(holdFraction, 0, .9f), 1, phase));
        LayoutLayer(primaryLayer, zoneSprites[CurrentZoneIndex], 1, phase);
        LayoutLayer(transitionLayer, zoneSprites[nextIndex], TransitionAmount, 0);
    }

    private void LayoutLayer(SpriteRenderer layer, Sprite sprite, float alpha, float phase)
    {
        layer.sprite = sprite;
        layer.enabled = sprite != null && alpha > 0;
        if (sprite == null) return;
        float viewHeight = targetCamera.orthographicSize * 2;
        float viewWidth = viewHeight * targetCamera.aspect;
        var size = sprite.bounds.size;
        // Cover the viewport with a uniform scale: crop edges instead of stretching the illustration.
        float scale = Mathf.Max(viewWidth / size.x, viewHeight / size.y);
        layer.transform.localScale = Vector3.one * scale;
        float excessHeight = Mathf.Max(0, size.y * scale - viewHeight);
        layer.transform.position = new Vector3(targetCamera.transform.position.x,
            targetCamera.transform.position.y + excessHeight * (.5f - phase), 5);
        layer.color = new Color(1, 1, 1, alpha);
    }
}