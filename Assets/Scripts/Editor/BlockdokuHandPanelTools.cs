using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BlockdokuHandPanelTools
{
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Blockdoku_2D.unity");
        var grid = Object.FindFirstObjectByType<GridManager_2D>();
        var spawner = Object.FindFirstObjectByType<BlockSpawner_2D>();
        var positions = new SerializedObject(spawner).FindProperty("spawnPositions");
        var parent = grid.gridParent.parent;
        var existing = parent.Find("Block Hand Panel");
        var panel = existing != null ? (RectTransform)existing : NewRect("Block Hand Panel", parent);
        var image = panel.GetComponent<Image>() ?? panel.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUIPackCartoon/Demo/Sprites/Shapes/Shapes/Rectangle/Rounded Rectangle - 256px.png");
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4;
        image.color = new Color(1, 1, 1, .56f); image.raycastTarget = false;
        var hand = panel.GetComponent<BlockdokuHandPanel>() ?? panel.gameObject.AddComponent<BlockdokuHandPanel>();
        hand.slots = new RectTransform[positions.arraySize];
        hand.board = (RectTransform)grid.gridParent;
        hand.panelHeight = 240; hand.previewCellSize = 320; hand.heightRatio = .8f;
        for (int i = 0; i < positions.arraySize; i++)
        {
            var slot = (RectTransform)((Transform)positions.GetArrayElementAtIndex(i).objectReferenceValue);
            slot.SetParent(panel, false); slot.localScale = Vector3.one;
            slot.anchorMin = new Vector2((float)i / positions.arraySize, 0);
            slot.anchorMax = new Vector2((float)(i + 1) / positions.arraySize, 1);
            slot.pivot = new Vector2(.5f, .5f);
            slot.offsetMin = new Vector2(8, 8); slot.offsetMax = new Vector2(-8, -8);
            hand.slots[i] = slot;
        }
        // Draw below the board and modal overlays; preserve the original spawn transform references.
        panel.SetSiblingIndex(grid.gridParent.GetSiblingIndex());
        Canvas.ForceUpdateCanvases(); hand.Refresh();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    public static void Preview()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Blockdoku_2D.unity");
        var roots = scene.GetRootGameObjects();
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
        var texture = new RenderTexture(1080, 1920, 24); camera.targetTexture = texture;
        foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            if (canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        var ui = Object.FindFirstObjectByType<UIManager_2D>(); ui.gameOverPanel.SetActive(false); ui.gameResetPanel.SetActive(false);
        Object.FindFirstObjectByType<GameMenu>().popup.SetActive(false);
        Canvas.ForceUpdateCanvases();
        var hand = Object.FindFirstObjectByType<BlockdokuHandPanel>(); hand.Refresh();
        Canvas.ForceUpdateCanvases();
        var grid = Object.FindFirstObjectByType<GridManager_2D>();
        foreach (Transform child in grid.gridParent.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            var cell = NewRect("Preview cell", grid.gridParent).gameObject.AddComponent<Image>();
            cell.sprite = grid.defaultEmptyCellSprite; cell.color = Color.white;
        }
        var shapes = new[] { new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right }, new[] { Vector2Int.left * 2, Vector2Int.left, Vector2Int.zero, Vector2Int.right, Vector2Int.right * 2 }, new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.one } };
        var colors = new[] { Color.red, Color.green, Color.blue };
        for (int i = 0; i < hand.slots.Length; i++)
        {
            var shape = shapes[i]; var center = new Vector2((shape.Min(p => p.x) + shape.Max(p => p.x)) * .5f, (shape.Min(p => p.y) + shape.Max(p => p.y)) * .5f);
            float commonPitch = hand.slots.Min(slot => Mathf.Min(slot.rect.width - 16, slot.rect.height - 16)) / 3;
            float pitch = Mathf.Min(commonPitch, (hand.slots[i].rect.width - 16) / (shape.Max(p => p.x) - shape.Min(p => p.x) + 1), (hand.slots[i].rect.height - 16) / (shape.Max(p => p.y) - shape.Min(p => p.y) + 1));
            foreach (var point in shape)
            {
                var rect = NewRect("Preview piece", hand.slots[i]); rect.sizeDelta = Vector2.one * (pitch - 2);
                rect.anchoredPosition = ((Vector2)point - center) * pitch;
                var image = rect.gameObject.AddComponent<Image>(); image.sprite = grid.defaultOccupiedCellSprite; image.color = colors[i];
            }
        }
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = texture;
        var png = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "CasualPartyVerification", "Blockdoku-hand-panel.png"), png.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(png); Object.DestroyImmediate(texture);
    }

    public static void ApplyAndPreview() { Apply(); Preview(); }
    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); return rect;
    }
}
