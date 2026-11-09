using DG.Tweening;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// All heads and fading trail stamps share one Graphic, one bounded buffer and one tween.
public sealed class BlockdokuBorderEffect : MaskableGraphic
{
    public const int TrailCapacity = 384;
    public const int EmissionBudget = 16;
    private struct Stamp { public Vector2 position; public Color color; public float born; public bool active; }
    private readonly Stamp[] stamps = new Stamp[TrailCapacity];
    private readonly Vector2[] heads = new Vector2[2];
    private readonly Vector2[] previous = new Vector2[2];
    private readonly Color[] colors = new Color[2];
    private readonly Vector3[][] paths = new Vector3[2][];
    private readonly float[] lengths = new float[2];
    private Sprite sprite;
    private Vector2 stampSize;
    private Tween playback;
    private int cursor;
    private float elapsed, duration, spacing, lifetime;
    public int ActiveTrailCount { get; private set; }
    public int LastEmissionCount { get; private set; }
    public bool IsPlaying => playback != null && playback.IsActive();
    public override Texture mainTexture => sprite != null ? sprite.texture : s_WhiteTexture;

    public void Configure(Image template)
    {
        sprite = template.sprite;
        material = template.material;
        stampSize = Vector2.Scale(template.rectTransform.sizeDelta, template.rectTransform.localScale);
        raycastTarget = false;
        SetMaterialDirty();
    }

    public void Play(Vector3[] first, Vector3[] second, Color firstColor, Color secondColor,
        float moveDuration, float stepDistance, float trailLifetime)
    {
        Stop();
        if (first == null || second == null || first.Length < 2 || second.Length < 2) return;
        paths[0] = first; paths[1] = second;
        colors[0] = firstColor == Color.clear ? Color.white : firstColor;
        colors[1] = secondColor == Color.clear ? Color.white : secondColor;
        duration = Mathf.Max(.01f, moveDuration);
        spacing = Mathf.Max(1, stepDistance);
        lifetime = Mathf.Max(.01f, trailLifetime);
        for (int h = 0; h < 2; h++)
        {
            lengths[h] = 0;
            for (int i = 1; i < paths[h].Length; i++) lengths[h] += Vector3.Distance(paths[h][i - 1], paths[h][i]);
            heads[h] = previous[h] = paths[h][0];
        }
        playback = DOTween.To(() => elapsed, ApplyTime, duration + Mathf.Max(.2f, lifetime), duration + Mathf.Max(.2f, lifetime))
            .SetEase(Ease.Linear).SetTarget(this).SetLink(gameObject).OnComplete(Finish);
        SetVerticesDirty();
    }

    private void ApplyTime(float time)
    {
        elapsed = time;
        LastEmissionCount = 0;
        if (time <= duration)
        {
            float progress = Mathf.Clamp01(time / duration);
            float eased = 1 - (1 - progress) * (1 - progress);
            for (int h = 0; h < 2; h++)
            {
                heads[h] = PositionOnPath(paths[h], lengths[h] * eased);
                float distance = Vector2.Distance(previous[h], heads[h]);
                if (distance < spacing) continue;
                // Never replay an unbounded trail backlog after a slow frame.
                int count = Mathf.Min(EmissionBudget / 2, Mathf.FloorToInt(distance / spacing));
                for (int i = 1; i <= count; i++)
                {
                    var color = colors[h]; color.a = 1;
                    stamps[cursor] = new Stamp { position = Vector2.Lerp(previous[h], heads[h], (float)i / count), color = color, born = time, active = true };
                    cursor = (cursor + 1) % TrailCapacity;
                    LastEmissionCount++;
                }
                previous[h] = heads[h];
            }
        }
        else
        {
            heads[0] = paths[0][paths[0].Length - 1];
            heads[1] = paths[1][paths[1].Length - 1];
        }
        ActiveTrailCount = 0;
        for (int i = 0; i < stamps.Length; i++)
        {
            if (stamps[i].active && time - stamps[i].born >= lifetime) stamps[i].active = false;
            if (stamps[i].active) ActiveTrailCount++;
        }
        SetVerticesDirty();
    }

    private static Vector2 PositionOnPath(Vector3[] path, float distance)
    {
        for (int i = 1; i < path.Length; i++)
        {
            float segment = Vector3.Distance(path[i - 1], path[i]);
            if (distance <= segment) return Vector3.Lerp(path[i - 1], path[i], segment > 0 ? distance / segment : 1);
            distance -= segment;
        }
        return path[path.Length - 1];
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (paths[0] == null) return;
        var uv = sprite != null ? DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
        for (int i = 0; i < stamps.Length; i++)
        {
            if (!stamps[i].active) continue;
            float age = Mathf.Clamp01((elapsed - stamps[i].born) / lifetime);
            var tint = stamps[i].color; tint.a *= 1 - age;
            AddStamp(vh, stamps[i].position, 1 - age * age, tint, uv);
        }
        float headScale = 1 - Mathf.Clamp01((elapsed - duration) / .2f);
        if (headScale > 0)
            for (int h = 0; h < 2; h++) AddStamp(vh, heads[h], headScale, colors[h], uv);
    }

    private void AddStamp(VertexHelper vh, Vector2 center, float scale, Color tint, Vector4 uv)
    {
        Vector2 half = stampSize * (.5f * scale);
        int index = vh.currentVertCount;
        vh.AddVert(center + new Vector2(-half.x, -half.y), tint, new Vector2(uv.x, uv.y));
        vh.AddVert(center + new Vector2(-half.x, half.y), tint, new Vector2(uv.x, uv.w));
        vh.AddVert(center + new Vector2(half.x, half.y), tint, new Vector2(uv.z, uv.w));
        vh.AddVert(center + new Vector2(half.x, -half.y), tint, new Vector2(uv.z, uv.y));
        vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
    }

    private void Finish() { playback = null; Clear(); }
    public void Stop()
    {
        if (playback != null) { playback.Kill(); playback = null; }
        Clear();
    }
    private void Clear()
    {
        System.Array.Clear(stamps, 0, stamps.Length);
        cursor = ActiveTrailCount = LastEmissionCount = 0;
        elapsed = 0;
        paths[0] = paths[1] = null;
        SetVerticesDirty();
    }
    protected override void OnDisable() { Stop(); base.OnDisable(); }
    protected override void OnDestroy() { Stop(); base.OnDestroy(); }
}
