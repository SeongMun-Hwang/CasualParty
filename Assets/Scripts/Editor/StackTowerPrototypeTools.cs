using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StackTowerPrototypeTools
{
    private const string ScenePath = "Assets/Scenes/StackTower.unity";
    private const string BlockPath = "Assets/Prefabs/StackTowerBlock.prefab";
    private const string Pack = "Assets/GUIPackCartoon/Demo/Sprites/";

    [MenuItem("Tools/Casual Party/Rebuild Stack Tower prototype")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var font = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPrefabTools.PrefabPath).GetComponentsInChildren<TextMeshProUGUI>(true).First().font;
        BuildBlock();
        var shapes = LoadBlockdokuShapePaths();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cameraObject.tag = "MainCamera";
        var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 9;
        camera.transform.position = new Vector3(0, 5, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(170, 217, 243, 255);
        var game = new GameObject("Stack Tower Game").AddComponent<StackTowerGame>(); game.gameCamera = camera;
        game.blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath).GetComponent<StackTowerBlock>();
        game.shapePool = shapes.Select(AssetDatabase.LoadAssetAtPath<BlockArray>).ToArray();
        game.blockRoot = new GameObject("Blocks").transform;
        var platform = new GameObject("Foundation", typeof(BoxCollider2D)); platform.transform.position = new Vector3(0, -.3f, 0);
        platform.GetComponent<BoxCollider2D>().size = new Vector2(6, .6f);
        platform.GetComponent<BoxCollider2D>().sharedMaterial = game.blockPrefab.grip;
        Visual(platform.transform, new Vector2(6, .6f), new Color32(41, 103, 151, 255));
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameMenu));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
        var header = Rect("TopUIPanel", canvas.transform); header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one; header.pivot = new Vector2(.5f, 1); header.sizeDelta = new Vector2(0, 300);
        SafeAreaTopPanelTools.Configure(header);
        Text(header, font, "STACK\nTOWER", new Vector2(190, -85), new Vector2(300, 120), 44, 0);
        game.scoreText = Text(header, font, "0", new Vector2(0, -85), new Vector2(300, 120), 85);
        var settings = Button(header, "SettingBtn", null, new Vector2(-90, -85), new Vector2(128, 128), 1);
        UnityEventTools.AddPersistentListener(settings.onClick, canvasGo.GetComponent<GameMenu>().Open);
        var best = Rect("BestScoreDisplay", header); Place(best, new Vector2(-260, -225), new Vector2(440, 100), 1);
        var row = best.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 12; row.childAlignment = TextAnchor.MiddleRight; row.childControlWidth = row.childControlHeight = true; row.childForceExpandWidth = row.childForceExpandHeight = false;
        var crown = Rect("Crown", best); var crownImage = crown.gameObject.AddComponent<Image>(); crownImage.sprite = Sprite("Assets/Sprites/CartoonGUI/Crown.png"); crownImage.color = new Color32(255, 189, 64, 255); crownImage.preserveAspect = true; crownImage.raycastTarget = false;
        var iconLayout = crown.gameObject.AddComponent<LayoutElement>(); iconLayout.minWidth = iconLayout.preferredWidth = iconLayout.minHeight = iconLayout.preferredHeight = 65;
        game.bestText = Text(best, font, "0", Vector2.zero, new Vector2(240, 100), 55); game.bestText.alignment = TextAlignmentOptions.MidlineLeft;
        var bestLayout = game.bestText.gameObject.AddComponent<LayoutElement>(); bestLayout.minWidth = 0; bestLayout.preferredHeight = 100; game.bestText.enableAutoSizing = true; game.bestText.fontSizeMin = 26; game.bestText.fontSizeMax = 55;
        game.statusText = Text(canvas.transform, font, "TAP DROP TO STACK", new Vector2(0, 335), new Vector2(950, 70), 38); Bottom(game.statusText.rectTransform, 335);
        game.dropButton = Button(canvas.transform, "DropBtn", Sprite(Pack + "Buttons/Rectangles/Orange.png"), Vector2.zero, new Vector2(430, 160)); Bottom((RectTransform)game.dropButton.transform, 200);
        var dropLabel = Text(game.dropButton.transform, font, "DROP", new Vector2(0, -80), new Vector2(400, 140), 64); dropLabel.color = Color.white;
        UnityEventTools.AddPersistentListener(game.dropButton.onClick, game.Drop);
        var overlay = Rect("GameOverPanel", canvas.transform); Stretch(overlay); var shade = overlay.gameObject.AddComponent<Image>(); shade.color = new Color(0, 0, 0, .45f);
        var card = Rect("ResultCard", overlay); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(760, 700); card.gameObject.AddComponent<Image>().color = new Color32(237, 248, 255, 255);
        Text(card, font, "NICE TRY!", new Vector2(0, -100), new Vector2(660, 110), 65);
        game.resultText = Text(card, font, "0 FLOORS\nBEST  0", new Vector2(0, -285), new Vector2(650, 230), 65);
        var home = Button(card, "TitleBtn", null, new Vector2(-160, -550), new Vector2(160, 160)); UnityEventTools.AddPersistentListener(home.onClick, game.GoToTitle);
        var retry = Button(card, "RetryBtn", null, new Vector2(160, -550), new Vector2(160, 160)); UnityEventTools.AddPersistentListener(retry.onClick, game.RestartGame);
        game.gameOverPanel = overlay.gameObject; game.gameOverPanel.SetActive(false);
        GameMenuPrefabTools.Connect(canvasGo.GetComponent<GameMenu>());
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        CasualButtonStyleTools.ApplyScene(scene);
        AddRotationControls(game);
        AddBackground(game);
        EditorSceneManager.SaveScene(scene, ScenePath);
        var buildScenes = EditorBuildSettings.scenes.ToList();
        if (!buildScenes.Any(s => s.path == ScenePath)) buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AddTitleEntry();
        AssetDatabase.SaveAssets();
    }

    public static void UpdatePieces()
    {
        BuildBlock();
        var shapes = LoadBlockdokuShapePaths();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StackTowerGame>(true)).Single();
        game.blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath).GetComponent<StackTowerBlock>();
        game.shapePool = shapes.Select(AssetDatabase.LoadAssetAtPath<BlockArray>).ToArray(); game.cellSize = .6f;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("[StackTower] Connected Blockdoku shapes: " + string.Join(", ", shapes.Select(System.IO.Path.GetFileNameWithoutExtension)));
    }

    public static void UpdateControls()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StackTowerGame>(true)).Single();
        AddRotationControls(game);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    public static void UpdateBackground()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StackTowerGame>(true)).Single();
        AddBackground(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void AddBackground(StackTowerGame game)
    {
        var paths = new[] { "01-Ground", "02-Village", "03-Mountains", "04-Sky", "05-Space" }
            .Select(name => "Assets/Art/StackTower/Backgrounds/Zones-v1/" + name + ".png").ToArray();
        foreach (var path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        var background = game.background;
        if (background == null) background = new GameObject("Altitude Background", typeof(StackTowerBackground)).GetComponent<StackTowerBackground>();
        bool migrating = background.zoneSprites == null || background.zoneSprites.Length == 0;
        background.transform.localScale = Vector3.one;
        background.transform.position = Vector3.zero;
        var oldRenderer = background.GetComponent<SpriteRenderer>();
        if (oldRenderer != null) { oldRenderer.sprite = null; oldRenderer.enabled = false; }
        background.zoneSprites = paths.Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
        var primary = background.transform.Find("Current Zone");
        var next = background.transform.Find("Next Zone");
        if (primary == null) { primary = new GameObject("Current Zone", typeof(SpriteRenderer)).transform; primary.SetParent(background.transform, false); }
        if (next == null) { next = new GameObject("Next Zone", typeof(SpriteRenderer)).transform; next.SetParent(background.transform, false); }
        background.primaryLayer = primary.GetComponent<SpriteRenderer>();
        background.transitionLayer = next.GetComponent<SpriteRenderer>();
        background.primaryLayer.sortingOrder = -100;
        background.transitionLayer.sortingOrder = -99;
        if (migrating) background.heightToSpace = 120;
        background.targetCamera = game.gameCamera;
        game.background = background;
        // Keep the blue HUD legible even when the background reaches dark outer space.
        var header = game.scoreText.GetComponentInParent<SafeAreaTopPanel>();
        if (header != null)
        {
            var shade = header.GetComponent<Image>() ?? header.gameObject.AddComponent<Image>();
            shade.color = new Color(1, 1, 1, .75f);
            shade.raycastTarget = false;
            Canvas.ForceUpdateCanvases();
            header.RefreshBackground();
        }
        var statusRect = game.statusText.rectTransform;
        var statusBackdrop = statusRect.parent.Find("Status Backdrop") as RectTransform;
        if (statusBackdrop == null) statusBackdrop = Rect("Status Backdrop", statusRect.parent);
        var statusShade = statusBackdrop.GetComponent<Image>() ?? statusBackdrop.gameObject.AddComponent<Image>();
        statusShade.color = new Color(1, 1, 1, .75f);
        statusShade.raycastTarget = false;
        statusBackdrop.anchorMin = statusRect.anchorMin; statusBackdrop.anchorMax = statusRect.anchorMax;
        statusBackdrop.pivot = statusRect.pivot; statusBackdrop.sizeDelta = statusRect.sizeDelta;
        statusBackdrop.anchoredPosition = statusRect.anchoredPosition;
        int labelIndex = statusRect.GetSiblingIndex();
        statusBackdrop.SetSiblingIndex(labelIndex - (statusBackdrop.GetSiblingIndex() < labelIndex ? 1 : 0));
        game.statusBackdrop = statusBackdrop;
        Canvas.ForceUpdateCanvases();
        background.ResetAltitude(game.GetCameraBaselineY());
    }

    private static void AddRotationControls(StackTowerGame game)
    {
        var parent = game.dropButton.transform.parent;
        var left = parent.Find("RotateCounterClockwiseBtn");
        var right = parent.Find("RotateClockwiseBtn");
        game.rotateCounterClockwiseButton = left != null ? left.GetComponent<Button>() : Button(parent, "RotateCounterClockwiseBtn", null, Vector2.zero, new Vector2(160, 160));
        game.rotateClockwiseButton = right != null ? right.GetComponent<Button>() : Button(parent, "RotateClockwiseBtn", null, Vector2.zero, new Vector2(160, 160));
        var drop = (RectTransform)game.dropButton.transform;
        Bottom(drop, StackTowerGame.GetControlsCenterY(1080f * 90f / 320f, 0, 1, drop.rect.height, game.controlsBottomGap));
        Bottom(game.statusText.rectTransform, drop.anchoredPosition.y + drop.rect.height * .5f + 55f);
        foreach (var button in new[] { game.rotateCounterClockwiseButton, game.rotateClockwiseButton })
        {
            var rect = (RectTransform)button.transform; rect.anchorMin = drop.anchorMin; rect.anchorMax = drop.anchorMax; rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(160, 160);
            rect.anchoredPosition = drop.anchoredPosition + new Vector2(button == game.rotateClockwiseButton ? 340 : -340, 0);
            button.onClick = new Button.ButtonClickedEvent();
            var hold = button.GetComponent<StackTowerRotationHold>() ?? button.gameObject.AddComponent<StackTowerRotationHold>();
            hold.game = game; hold.clockwise = button == game.rotateClockwiseButton;
        }
        CasualButtonStyleTools.ApplyScene(game.gameObject.scene);
    }

    private static string[] LoadBlockdokuShapePaths()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Blockdoku_2D.unity");
        var spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BlockSpawner_2D>(true)).Single();
        var groups = new SerializedObject(spawner).FindProperty("blockGroups");
        var result = new System.Collections.Generic.List<BlockArray>();
        System.Action<SerializedProperty> add = item =>
        {
            if (!item.FindPropertyRelative("isActive").boolValue) return;
            var shape = item.FindPropertyRelative("block").objectReferenceValue as BlockArray;
            if (StackTowerBlock.IsConnectedShape(shape) && !result.Contains(shape)) result.Add(shape);
        };
        for (int i = 0; i < groups.arraySize; i++)
        {
            var group = groups.GetArrayElementAtIndex(i);
            if (!group.FindPropertyRelative("isActive").boolValue) continue;
            add(group.FindPropertyRelative("mainBlock"));
            var variants = group.FindPropertyRelative("variants");
            for (int j = 0; j < variants.arraySize; j++) add(variants.GetArrayElementAtIndex(j));
        }
        return result.Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p)).ToArray();
    }

    private static void BuildBlock()
    {
        const string materialPath = "Assets/Prefabs/StackTowerGrip.physicsMaterial2D";
        var grip = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
        if (grip == null) { grip = new PhysicsMaterial2D("Stack Tower Grip") { friction = .85f, bounciness = 0 }; AssetDatabase.CreateAsset(grip, materialPath); }
        var go = new GameObject("Stack Block", typeof(Rigidbody2D), typeof(StackTowerBlock));
        var body = go.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 2.2f; body.linearDamping = .25f; body.angularDamping = 1.5f; body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var block = go.GetComponent<StackTowerBlock>(); block.cellSprite = RectangleSprite(); block.grip = grip;
        block.Configure(AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/Traditional/O.asset"), 0, .6f, new Color32(43, 166, 220, 255));
        PrefabUtility.SaveAsPrefabAsset(go, BlockPath); Object.DestroyImmediate(go);
    }

    private static void Visual(Transform parent, Vector2 size, Color color)
    {
        var sprite = RectangleSprite();
        var go = new GameObject("Block Visual", typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color;
        go.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1);
        var shine = new GameObject("Top Edge", typeof(SpriteRenderer)); shine.transform.SetParent(parent, false);
        shine.transform.localPosition = new Vector3(0, size.y * .36f, -.01f);
        shine.transform.localScale = new Vector3(size.x * .92f, size.y * .09f, 1);
        shine.GetComponent<SpriteRenderer>().sprite = sprite; shine.GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, .28f);
    }

    private static Sprite RectangleSprite()
    {
        const string path = "Assets/Prefabs/StackTowerRectangle.asset";
        var existing = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (existing != null) return existing;
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Stack Tower Rectangle", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); texture.Apply();
        AssetDatabase.CreateAsset(texture, path);
        var sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
        sprite.name = "Stack Tower Rectangle"; AssetDatabase.AddObjectToAsset(sprite, texture); AssetDatabase.SaveAssets();
        return sprite;
    }

    private static void AddTitleEntry()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        var manager = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TitleManager>(true)).Single();
        var content = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).First(r => r.name == "Content");
        var entry = content.Find("StackTowerBtn");
        if (entry == null)
        {
            var template = content.GetComponentsInChildren<Button>(true).First(b => b.GetComponentsInChildren<TextMeshProUGUI>(true).Any(t => t.text.Contains("Sum")));
            entry = Object.Instantiate(template.gameObject, content, false).transform;
        }
        entry.name = "StackTowerBtn";
        if (PrefabUtility.IsAnyPrefabInstanceRoot(entry.gameObject)) PrefabUtility.UnpackPrefabInstance(entry.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var button = entry.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent(); UnityEventTools.AddPersistentListener(button.onClick, manager.StackTowerBtnOnClicked);
        foreach (var text in entry.GetComponentsInChildren<TextMeshProUGUI>(true)) { text.text = "Stack\nTower"; text.fontSize = 70; }
        foreach (var image in entry.GetComponentsInChildren<Image>(true)) image.sprite = Sprite(Pack + "Buttons/Square/Blue.png");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size, float anchor = .5f)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void Bottom(RectTransform rect, float y) { rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, y); }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string value, Vector2 position, Vector2 size, float fontSize, float anchor = .5f)
    {
        var rect = Rect(value, parent); Place(rect, position, size, anchor); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = fontSize; text.color = new Color32(32, 82, 123, 255); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    private static Button Button(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, float anchor = .5f)
    {
        var rect = Rect(name, parent); Place(rect, position, size, anchor); var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; return button;
    }

    public static void Preview() => PreviewAtAltitude(0);
    public static void PreviewSpace() => PreviewAtAltitude(1);
    public static void PreviewAllZones()
    {
        foreach (float altitude in new[] { 0f, .2f, .25f, .5f, .75f, 1f }) PreviewAtAltitude(altitude);
    }

    private static void PreviewAtAltitude(float altitude)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var game = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StackTowerGame>(true)).Single();
        var camera = game.gameCamera;
        var preview = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath));
        var piece = preview.GetComponent<StackTowerBlock>();
        piece.Configure(game.shapePool.FirstOrDefault(s => s.name == "T") ?? game.shapePool[0], 0, game.cellSize, new Color32(43, 166, 220, 255));
        preview.transform.position = new Vector3(.7f, 3.4f + piece.Size.y * .5f, 0);
        var texture = new RenderTexture(1080, 1920, 24); camera.targetTexture = texture;
        foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true))) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        Canvas.ForceUpdateCanvases(); foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMeshProUGUI>(true))) if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        camera.transform.position = new Vector3(0, game.GetCameraBaselineY(), -10);
        if (game.background != null) game.background.ResetAltitude(camera.transform.position.y);
        if (game.background != null && altitude > 0)
        {
            float ascent = game.background.heightToSpace * altitude;
            camera.transform.position += Vector3.up * ascent;
            preview.transform.position += Vector3.up * ascent;
            game.background.Refresh();
        }
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = texture;
        var png = new Texture2D(1080, 1920, TextureFormat.RGB24, false); png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
        string filename = altitude <= 0 ? "StackTower-prototype.png" : altitude >= 1 ? "StackTower-space.png" : $"StackTower-altitude-{Mathf.RoundToInt(altitude * 100):000}.png";
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CasualPartyVerification", filename), png.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(png); Object.DestroyImmediate(texture);
    }
}
