using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TitleGameCardArtTools
{
    private sealed class GameArt
    {
        public Sprite empty, filled, mine;
        public TMP_FontAsset font;
        public Color[] colors;
    }

    public static void Apply()
    {
        // Read the actual scene/prefab references so menu illustrations match gameplay assets.
        EditorSceneManager.OpenScene("Assets/Scenes/Blockdoku_2D.unity");
        var block = Object.FindFirstObjectByType<GridManager_2D>();
        var palette = new SerializedObject(Object.FindFirstObjectByType<BlockSpawner_2D>()).FindProperty("availableColors");
        var colors = new Color[Mathf.Max(1, palette.arraySize)];
        for (int i = 0; i < palette.arraySize; i++)
        {
            var entry = palette.GetArrayElementAtIndex(i);
            colors[i] = Color.HSVToRGB(entry.FindPropertyRelative("h").floatValue, entry.FindPropertyRelative("s").floatValue, entry.FindPropertyRelative("v").floatValue);
        }
        var blockArt = new GameArt { empty = block.defaultEmptyCellSprite, filled = block.defaultOccupiedCellSprite, colors = colors };
        EditorSceneManager.OpenScene("Assets/Scenes/10SUM.unity");
        var ten = Object.FindFirstObjectByType<GridManager>();
        var tenArt = new GameArt { filled = ten.appleSprite, font = ten.cellPrefab.GetComponentInChildren<TextMeshProUGUI>(true).font };
        EditorSceneManager.OpenScene("Assets/Scenes/MineSweeper.unity");
        var mine = Object.FindFirstObjectByType<MineSweeper_GridManager>();
        var mineArt = new GameArt { empty = mine.revealedSprite, filled = mine.hiddenSprite, mine = mine.mineSprite, font = mine.cellPrefab.GetComponentInChildren<TextMeshProUGUI>(true).font };
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        ApplyCard("BlockdokuBtnOnClicked", "BLOCKDOKU", 36, blockArt, 0);
        ApplyCard("TenSumBtnOnClicked", "10SUM", 40, tenArt, 1);
        ApplyCard("MineSweeperBtnOnclicked", "MINESWEEPER", 32, mineArt, 2);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ApplyCard(string method, string title, float fontSize, GameArt art, int kind)
    {
        var button = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == method));
        if (PrefabUtility.IsAnyPrefabInstanceRoot(button.gameObject))
            PrefabUtility.UnpackPrefabInstance(button.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var previous = button.transform.Find("Gameplay Artwork");
        if (previous != null) Object.DestroyImmediate(previous.gameObject);
        var oldInset = button.transform.Find("Card Inset");
        if (oldInset != null) Object.DestroyImmediate(oldInset.gameObject);
        var face = button.targetGraphic as Image;
        if (face == null) face = button.transform.Find("Button").GetComponent<Image>();
        var rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUIPackCartoon/Demo/Sprites/Shapes/Shapes/Rectangle/Rounded Rectangle - 256px.png");
        face.sprite = rounded;
        face.color = new Color32(15, 80, 152, 255); face.type = Image.Type.Sliced;
        face.pixelsPerUnitMultiplier = 4; face.preserveAspect = false;
        face.rectTransform.anchorMin = Vector2.zero; face.rectTransform.anchorMax = Vector2.one;
        face.rectTransform.offsetMin = face.rectTransform.offsetMax = Vector2.zero;
        var shadow = button.transform.Find("Shadow"); if (shadow != null) shadow.gameObject.SetActive(false);
        var icon = button.transform.Find("Icon"); if (icon != null) icon.gameObject.SetActive(false);
        var label = button.GetComponentsInChildren<TextMeshProUGUI>(true).Single();
        label.gameObject.SetActive(true); label.text = title; label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize; label.enableAutoSizing = true; label.fontSizeMin = fontSize - 6; label.fontSizeMax = fontSize;
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, 0);
        label.rectTransform.pivot = new Vector2(.5f, .5f);
        label.rectTransform.sizeDelta = new Vector2(300, 58); label.rectTransform.anchoredPosition = new Vector2(0, 70);
        // One continuous inset connects the gameplay illustration and its name.
        var inset = RoundedAt("Card Inset", button.transform, Vector2.zero, new Vector2(378, 378), rounded, new Color32(66, 170, 245, 255));
        var deck = RoundedAt("Card Interior", inset.transform, Vector2.zero, new Vector2(358, 358), rounded, new Color32(17, 51, 91, 255));
        RoundedAt("Name Band", deck.transform, new Vector2(0, -124), new Vector2(338, 80), rounded, new Color32(13, 43, 80, 255));
        ImageAt("Name Divider", deck.transform, new Vector2(0, -81), new Vector2(304, 2), null, new Color32(65, 133, 188, 180));
        RoundedAt("Rim Highlight", inset.transform, new Vector2(0, 182), new Vector2(266, 3), rounded, new Color32(151, 219, 255, 255));
        var tray = ImageAt("Gameplay Artwork", button.transform, Vector2.zero, new Vector2(238, 238), null, Color.clear);
        tray.rectTransform.anchoredPosition = new Vector2(0, 42);
        int count = kind == 0 ? 5 : 3;
        float pitch = 226f / count, size = pitch - (kind == 0 ? 3 : 5);
        int[] values = { 1, 3, 7, 5, 2, 3, 7, 4, 6 };
        for (int r = 0; r < count; r++) for (int c = 0; c < count; c++)
        {
            var pos = new Vector2((c - (count - 1) * .5f) * pitch, ((count - 1) * .5f - r) * pitch);
            Sprite sprite = art.filled; Color tint = Color.white;
            if (kind == 0)
            {
                int piece = r >= 2 && c == 0 || r == 4 && c <= 1 ? 0 : r == 1 && c >= 1 && c <= 3 || r == 2 && c == 2 ? 1 : c == 4 && r >= 2 ? 2 : -1;
                sprite = piece < 0 ? art.empty : art.filled;
                tint = piece < 0 ? Color.white : art.colors[piece % art.colors.Length];
            }
            bool revealed = kind == 2 && (c == 1 || r == 2 && c == 0);
            if (revealed) sprite = art.empty;
            var cell = ImageAt("Cell " + r + "," + c, tray.transform, pos, Vector2.one * size, sprite, tint);
            if (kind == 1 || kind == 2 && c == 1)
            {
                var text = new GameObject("Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                text.layer = 5; text.transform.SetParent(cell.transform, false);
                var tmp = text.GetComponent<TextMeshProUGUI>(); tmp.font = art.font;
                tmp.text = kind == 1 ? values[r * count + c].ToString() : (r % 2 + 1).ToString();
                tmp.color = kind == 1 ? Color.white : r % 2 == 0 ? new Color32(0, 80, 240, 255) : new Color32(0, 140, 0, 255);
                tmp.fontSize = kind == 1 ? 46 : 43; tmp.alignment = TextAlignmentOptions.Center; tmp.raycastTarget = false;
                tmp.rectTransform.anchorMin = Vector2.zero; tmp.rectTransform.anchorMax = Vector2.one;
                tmp.rectTransform.offsetMin = tmp.rectTransform.offsetMax = Vector2.zero;
            }
            if (kind == 2 && r == 2 && c == 0) ImageAt("Mine", cell.transform, Vector2.zero, Vector2.one * size * .75f, art.mine, Color.white);
        }
        label.transform.SetAsLastSibling();
        if (face.sprite == null || art.filled == null || button.onClick.GetPersistentEventCount() == 0)
            throw new System.InvalidOperationException("Missing gameplay art or game navigation: " + title);
    }

    private static Image ImageAt(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5; go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color;
        image.raycastTarget = false; image.preserveAspect = sprite != null;
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(.5f, .5f);
        image.rectTransform.anchoredPosition = position; image.rectTransform.sizeDelta = size;
        return image;
    }

    private static Image RoundedAt(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
    {
        var image = ImageAt(name, parent, position, size, sprite, color);
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 5; image.preserveAspect = false;
        return image;
    }

    public static void ApplyAndPreview()
    {
        Apply();
        Preview();
    }

    public static void Preview()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
        var texture = new RenderTexture(1080, 1920, 24); camera.targetTexture = texture;
        foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            if (canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        Canvas.ForceUpdateCanvases();
        foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMeshProUGUI>(true)))
            if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = texture;
        var png = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
        png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "CasualPartyVerification", "Title-block-game-cards.png"), png.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.DestroyImmediate(png); Object.DestroyImmediate(texture);
    }
}
