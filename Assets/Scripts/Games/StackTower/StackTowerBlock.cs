using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class StackTowerBlock : MonoBehaviour
{
    public Sprite cellSprite;
    public PhysicsMaterial2D grip;
    [SerializeField] private BlockArray shapeData;
    [SerializeField] private int rotation;
    private readonly HashSet<(Collider2D, Collider2D)> supports = new HashSet<(Collider2D, Collider2D)>();
    private readonly List<BoxCollider2D> cells = new List<BoxCollider2D>();
    public Rigidbody2D Body { get; private set; }
    public BlockArray ShapeData => shapeData;
    public int Rotation => rotation;
    public int CellCount => cells.Count;
    public Vector2 Size { get; private set; }
    public Vector2 RotatedSize
    {
        get
        {
            float radians = Body.rotation * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(radians)), s = Mathf.Abs(Mathf.Sin(radians));
            return new Vector2(Size.x * c + Size.y * s, Size.x * s + Size.y * c);
        }
    }
    public Bounds WorldBounds
    {
        get
        {
            var bounds = cells.Count > 0 ? cells[0].bounds : new Bounds(transform.position, Vector3.zero);
            for (int i = 1; i < cells.Count; i++) bounds.Encapsulate(cells[i].bounds);
            return bounds;
        }
    }
    public bool IsSupported => supports.Count > 0;
    public bool IsSupportedByPiece
    {
        get
        {
            foreach (var pair in supports)
                if (pair.Item2 != null && pair.Item2.attachedRigidbody != null && pair.Item2.attachedRigidbody.GetComponent<StackTowerBlock>() != null) return true;
            return false;
        }
    }
    private void Awake() => Body = GetComponent<Rigidbody2D>();

    private void OnCollisionEnter2D(Collision2D collision) => UpdateSupport(collision);
    private void OnCollisionStay2D(Collision2D collision) => UpdateSupport(collision);
    private void OnCollisionExit2D(Collision2D collision) => supports.Remove((collision.otherCollider, collision.collider));

    public static bool IsConnectedShape(BlockArray data)
    {
        if (data == null) return false;
        var shape = data.GetShape();
        if (shape.Count == 0) return false;
        var remaining = new HashSet<Vector2Int>(shape);
        var pending = new Queue<Vector2Int>(); pending.Enqueue(shape[0]); remaining.Remove(shape[0]);
        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (pending.Count > 0)
        {
            var cell = pending.Dequeue();
            foreach (var direction in directions)
                if (remaining.Remove(cell + direction)) pending.Enqueue(cell + direction);
        }
        return remaining.Count == 0;
    }

    public void Configure(BlockArray data, int quarterTurns, float cellSize, Color color)
    {
        if (!IsConnectedShape(data) || cellSize <= 0) throw new ArgumentException("Stack Tower requires an edge-connected BlockArray and a positive cell size.");
        if (Body == null) Body = GetComponent<Rigidbody2D>();
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            child.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
        }
        cells.Clear(); supports.Clear(); shapeData = data; rotation = ((quarterTurns % 4) + 4) % 4;
        var shape = data.GetShape(rotation);
        Vector2 min = shape[0], max = shape[0];
        foreach (var cell in shape) { min = Vector2.Min(min, cell); max = Vector2.Max(max, cell); }
        Vector2 center = (min + max) * .5f;
        Size = (max - min + Vector2.one) * cellSize;
        foreach (var cell in shape)
        {
            var go = new GameObject("Cell " + cell.x + "," + cell.y, typeof(BoxCollider2D));
            go.transform.SetParent(transform, false); go.transform.localPosition = ((Vector2)cell - center) * cellSize;
            var collider = go.GetComponent<BoxCollider2D>(); collider.size = Vector2.one * cellSize; collider.sharedMaterial = grip; cells.Add(collider);
            // Scale only the visual; collider cells touch along their full edges, preserving concave gaps.
            var visual = new GameObject("Tile", typeof(SpriteRenderer)); visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(cellSize * .95f / cellSprite.bounds.size.x, cellSize * .95f / cellSprite.bounds.size.y, 1);
            visual.GetComponent<SpriteRenderer>().sprite = cellSprite; visual.GetComponent<SpriteRenderer>().color = color;
        }
        Body.mass = Mathf.Max(.3f, shape.Count * .3f);
    }

    private void UpdateSupport(Collision2D collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y > .45f) { supports.Add((collision.otherCollider, collision.collider)); return; }
        supports.Remove((collision.otherCollider, collision.collider));
    }
}
