using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BlockPuzzleVisualTools
{
    private const string Pack = "Assets/GUIPackCartoon/Demo/Sprites/";
    private static readonly Color Ink = new Color32(36, 84, 125, 255);
    private static readonly Color Gold = new Color32(255, 204, 103, 255);
    private static TMP_FontAsset font;
    private static Sprite Tile => Load(Pack + "Buttons/Square/Light.png");
    private static Sprite Rounded => Load(Pack + "Shapes/Shapes/Rectangle/Rounded Rectangle - 256px.png");
    private static readonly System.Collections.Generic.HashSet<string> changed = new System.Collections.Generic.HashSet<string>();

    // Only menu and popup presentation belongs here. Gameplay boards and HUD stay unchanged.
    public static void Apply()
    {
        changed.Clear();
        StyleSettingsPrefab();
        TitleGameCardArtTools.Apply(); changed.Add("Assets/Scenes/Title.unity");
        foreach (string name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            string path = "Assets/Scenes/" + name + ".unity";
            var scene = EditorSceneManager.OpenScene(path);
            font = GetFont();
            var all = scene.GetRootGameObjects();
            var block = all.SelectMany(r => r.GetComponentsInChildren<UIManager_2D>(true)).FirstOrDefault();
            if (block != null)
            {
                StyleResults(block.gameOverPanel, "GAME OVER", block.finalScoreText, null, block.newBestObj);
                StyleReset(block.gameResetPanel);
            }
            var ten = all.SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).FirstOrDefault();
            if (ten != null) StyleResults(ten.gameOverPanel, "TIME'S UP!", ten.finalScoreText, null);
            var mine = all.SelectMany(r => r.GetComponentsInChildren<MineSweeper_UIManager>(true)).FirstOrDefault();
            if (mine != null)
            {
                StyleResults(mine.gameOverPanel, "GAME OVER", mine.finalTimeText, mine.bestTimeText);
                StyleDifficulty(mine);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); changed.Add(path);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "CasualPartyVerification", "popup-only-assets.txt"), changed.OrderBy(p => p));
    }

    public static void FixGameOverHeadings()
    {
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var roots = scene.GetRootGameObjects();
            var block = roots.SelectMany(r => r.GetComponentsInChildren<UIManager_2D>(true)).FirstOrDefault();
            var ten = roots.SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).FirstOrDefault();
            var mine = roots.SelectMany(r => r.GetComponentsInChildren<MineSweeper_UIManager>(true)).FirstOrDefault();
            var panel = block != null ? block.gameOverPanel : ten != null ? ten.gameOverPanel : mine.gameOverPanel;
            var headings = panel.GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.name == "GameOverTmp" || t.name == "ResultTmp" || t.name == "Theme Heading").ToArray();
            var template = headings.FirstOrDefault(t => t.name == "Theme Heading") ?? headings.First();
            var keep = mine != null ? mine.resultText : headings.FirstOrDefault(t => t.name != "Theme Heading") ?? template;
            keep.text = template.text; keep.font = template.font; keep.color = template.color;
            keep.transform.SetParent(template.transform.parent, false);
            Top(keep.rectTransform, 0, -220, 680, 100); keep.fontSize = 56; keep.enableAutoSizing = false;
            foreach (var heading in headings) heading.gameObject.SetActive(heading == keep);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    private static TMP_FontAsset GetFont() => AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPrefabTools.PrefabPath).GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.text == "SETTINGS").font;

    private static RectTransform Card(GameObject overlay, float height)
    {
        Unpack(overlay);
        foreach (var text in overlay.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            text.font = font; text.color = Ink;
        }
        var overlayAnimator = overlay.GetComponent<Animator>(); if (overlayAnimator != null) overlayAnimator.enabled = false;
        var rect = (RectTransform)overlay.transform;
        bool fullscreen = rect.anchorMax.y - rect.anchorMin.y > .9f;
        RectTransform card;
        if (fullscreen)
        {
            var dim = overlay.GetComponent<Image>() ?? overlay.AddComponent<Image>();
            dim.sprite = null; dim.color = new Color(.035f, .10f, .17f, .68f);
            card = overlay.transform.Find("Panel") as RectTransform ?? Rect("Panel", overlay.transform);
        }
        else
        {
            card = rect;
            var dim = card.Find("Theme Backdrop") as RectTransform ?? Rect("Theme Backdrop", card);
            dim.anchorMin = dim.anchorMax = new Vector2(.5f, .5f); dim.sizeDelta = new Vector2(10000, 10000);
            dim.anchoredPosition = Vector2.zero;
            var image = dim.GetComponent<Image>() ?? dim.gameObject.AddComponent<Image>();
            image.sprite = null; image.color = new Color(.035f, .10f, .17f, .68f); dim.SetAsFirstSibling();
        }
        Center(card, 0, 0, 780, height);
        var cardAnimator = card.GetComponent<Animator>(); if (cardAnimator != null) cardAnimator.enabled = false;
        Surface(card, new Color32(92, 145, 181, 255));
        var surface = card.Find("Theme Surface") as RectTransform ?? Rect("Theme Surface", card);
        Stretch(surface, 6); Surface(surface, new Color32(246, 251, 255, 255));
        surface.SetAsFirstSibling();
        var backdrop = card.Find("Theme Backdrop"); if (backdrop != null) backdrop.SetAsFirstSibling();
        var intercept = card.GetComponent<Button>(); if (intercept != null) intercept.transition = Selectable.Transition.None;
        return card;
    }

    private static void StyleResults(GameObject overlay, string title, TextMeshProUGUI primary, TextMeshProUGUI secondary, GameObject newBest = null)
    {
        var card = Card(overlay, 820);
        var headings = overlay.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Where(t => t != primary && t != secondary && (t.name == "GameOverTmp" || t.name == "ResultTmp" || t.name == "Theme Heading")).ToArray();
        var header = headings.FirstOrDefault(t => t.name != "Theme Heading") ?? headings.FirstOrDefault();
        if (header == null) header = Label(card, "Theme Heading", title, 56);
        foreach (var duplicate in headings) duplicate.gameObject.SetActive(duplicate == header);
        header.gameObject.SetActive(true);
        header.text = title; header.transform.SetParent(card, false); Top(header.rectTransform, 0, -220, 680, 100); header.fontSize = 56; header.enableAutoSizing = false;
        Badge(card, Load(Pack + "Icons/Icons White/Basic/Star.png"), Gold);
        primary.transform.SetParent(card, false); Top(primary.rectTransform, 0, -385, 650, 150); primary.fontSize = 62;
        primary.enableAutoSizing = true; primary.fontSizeMin = 40; primary.fontSizeMax = 62;
        if (secondary != null) { secondary.transform.SetParent(card, false); Top(secondary.rectTransform, 0, -500, 650, 90); secondary.fontSize = 36; }
        if (newBest != null)
        {
            newBest.transform.SetParent(card, false); Top((RectTransform)newBest.transform, 0, -535, 500, 80);
            foreach (var text in newBest.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.gameObject != newBest) Stretch(text.rectTransform, 0);
                text.fontSize = 38; text.enableAutoSizing = false; text.color = new Color32(210, 143, 40, 255);
            }
        }
        var buttons = overlay.GetComponentsInChildren<Button>(true).Where(b => b.transform != card).ToArray();
        for (int i = 0; i < buttons.Length; i++)
        {
            bool restart = buttons[i].name.Contains("Retry") || buttons[i].name.Contains("Yes");
            buttons[i].transform.SetParent(card, false);
            Top((RectTransform)buttons[i].transform, buttons.Length == 1 ? 0 : i == 0 ? -170 : 170, -710, 310, 112);
            ActionButton(buttons[i], restart ? "PLAY AGAIN" : "MAIN MENU", restart);
        }
    }

    private static void StyleReset(GameObject overlay)
    {
        var card = Card(overlay, 620);
        var title = overlay.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.name.Contains("GameOver"));
        title.transform.SetParent(card, false); title.text = "NEW GAME?"; title.fontSize = 56; title.enableAutoSizing = false; Top(title.rectTransform, 0, -220, 680, 90);
        Badge(card, Load("Assets/Sprites/CartoonGUI/Repeat.png"), Ink);
        var hint = card.Find("Theme Hint")?.GetComponent<TextMeshProUGUI>() ?? Label(card, "Theme Hint", "Your current board will be reset.", 32);
        Top(hint.rectTransform, 0, -335, 650, 100);
        var buttons = overlay.GetComponentsInChildren<Button>(true).Where(b => b.transform != card).ToArray();
        for (int i = 0; i < buttons.Length; i++)
        {
            bool cancel = buttons[i].name.Contains("No"); buttons[i].transform.SetParent(card, false);
            Top((RectTransform)buttons[i].transform, i == 0 ? -170 : 170, -510, 310, 112);
            ActionButton(buttons[i], cancel ? "CANCEL" : "RESTART", !cancel);
        }
    }

    private static void StyleDifficulty(MineSweeper_UIManager ui)
    {
        var card = Card(ui.difficultyPanel, 1160);
        var title = ui.difficultyPanel.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.name.Contains("GameOver"));
        title.text = "MINESWEEPER"; title.fontSize = 54; title.enableAutoSizing = false; title.transform.SetParent(card, false); Top(title.rectTransform, 0, -215, 680, 100);
        Badge(card, Load(Pack + "Icons/Icons Colored/Bombs/Bomb Spiked - Blue.png"), Color.white);
        var subtitle = card.Find("Theme Subtitle")?.GetComponent<TextMeshProUGUI>() ?? Label(card, "Theme Subtitle", "SELECT DIFFICULTY", 28);
        Top(subtitle.rectTransform, 0, -290, 640, 60);
        var buttons = new[] { ui.beginnerButton, ui.intermediateButton, ui.advancedButton };
        var best = new[] { ui.bestTimeBeginnerText, ui.bestTimeIntermediateText, ui.bestTimeAdvancedText };
        var labels = new[] { "BEGINNER", "INTERMEDIATE", "ADVANCED" };
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].transform.SetParent(card, false); Top((RectTransform)buttons[i].transform, 0, -430 - i * 245, 640, 185);
            ActionButton(buttons[i], labels[i], false);
            var caption = buttons[i].transform.Find("Theme Label").GetComponent<TextMeshProUGUI>();
            Top(caption.rectTransform, 0, -62, 570, 70); caption.fontSize = 40; caption.fontSizeMax = 40;
            best[i].transform.SetParent(buttons[i].transform, false); best[i].gameObject.SetActive(true);
            best[i].color = new Color32(92, 133, 162, 255); best[i].fontSize = 29;
            Top(best[i].rectTransform, 0, -133, 570, 60);
        }
    }

    private static void StyleSettingsPrefab()
    {
        string path = GameMenuPrefabTools.PrefabPath;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            font = root.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => t.text == "SETTINGS").font;
            var card = (RectTransform)root.transform.Find("SettingsCard");
            Surface(card, new Color32(92, 145, 181, 255));
            var fill = card.Find("Theme Surface") as RectTransform ?? Rect("Theme Surface", card);
            Stretch(fill, 6); Surface(fill, new Color32(246, 251, 255, 255)); fill.SetAsFirstSibling();
            foreach (var text in card.GetComponentsInChildren<TextMeshProUGUI>(true)) text.color = Ink;
            var settings = root.GetComponent<SettingsUI>();
            foreach (var slider in new[] { settings.bgmVolumeSlider, settings.sfxVolumeSlider, settings.vibrationVolumeSlider })
            {
                if (slider.fillRect != null) { var image = slider.fillRect.GetComponent<Image>(); if (image != null) image.color = new Color32(54, 170, 213, 255); }
                if (slider.handleRect != null) { var image = slider.handleRect.GetComponent<Image>(); if (image != null) { image.sprite = Tile; image.color = Color.white; } }
            }
            PrefabUtility.SaveAsPrefabAsset(root, path); changed.Add(path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void ActionButton(Button button, string label, bool primary)
    {
        Unpack(button.gameObject);
        var animator = button.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
        var image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
        image.sprite = primary ? Load(Pack + "Buttons/Rectangles/Blue.png") : Rounded;
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = primary ? 2 : 4;
        image.color = primary ? Color.white : new Color32(220, 237, 248, 255); image.preserveAspect = false; image.raycastTarget = true;
        button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint;
        foreach (Transform child in button.transform) if (child.name != "Theme Label") child.gameObject.SetActive(false);
        var caption = button.transform.Find("Theme Label")?.GetComponent<TextMeshProUGUI>() ?? Label(button.transform, "Theme Label", label, 30);
        caption.text = label; caption.color = primary ? Color.white : Ink; caption.gameObject.SetActive(true);
        Stretch(caption.rectTransform, 14); caption.alignment = TextAlignmentOptions.Center; caption.enableAutoSizing = true; caption.fontSizeMin = 24; caption.fontSizeMax = 34;
    }

    private static void Badge(RectTransform card, Sprite sprite, Color tint)
    {
        var badge = card.Find("Theme Badge") as RectTransform ?? Rect("Theme Badge", card);
        Top(badge, 0, -95, 130, 130); var face = badge.GetComponent<Image>() ?? badge.gameObject.AddComponent<Image>();
        face.sprite = Tile; face.color = Color.white; face.raycastTarget = false;
        var icon = badge.Find("Icon") as RectTransform ?? Rect("Icon", badge); Stretch(icon, 31);
        var image = icon.GetComponent<Image>() ?? icon.gameObject.AddComponent<Image>();
        image.sprite = sprite; image.color = tint; image.preserveAspect = true; image.raycastTarget = false;
    }

    private static Image Surface(RectTransform rect, Color tint)
    {
        var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
        image.sprite = Rounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 5;
        image.color = tint; image.preserveAspect = false;
        if (rect.GetComponent<Button>() == null) image.raycastTarget = false;
        return image;
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
    }
    private static void Top(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
    }
    private static void Center(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
    }
    private static TextMeshProUGUI Label(Transform parent, string name, string value, float size)
    {
        var rect = Rect(name, parent); var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.color = Ink; text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    private static void Unpack(GameObject go)
    {
        var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (instance != null) PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    public static void Report()
    {
        foreach (string name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var root in scene.GetRootGameObjects())
                foreach (var panel in root.GetComponentsInChildren<RectTransform>(true).Where(r => r.name.Contains("Panel") || r.name.Contains("Card")))
                    Debug.Log($"[VISUAL REPORT] {name} {panel.name}: parent={panel.parent?.name}, anchors={panel.anchorMin}/{panel.anchorMax}, position={panel.anchoredPosition}, size={panel.sizeDelta}, buttons={string.Join(",", panel.GetComponentsInChildren<Button>(true).Select(b => b.name))}, texts={string.Join("|", panel.GetComponentsInChildren<TextMeshProUGUI>(true).Select(t => t.name + ":" + t.text.Replace("\n", " ")))}");
        }
    }

    private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Missing theme sprite: " + path);
}
