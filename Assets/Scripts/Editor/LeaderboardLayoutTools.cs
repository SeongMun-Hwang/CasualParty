using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class LeaderboardLayoutTools
{
    [MenuItem("Tools/Casual Party/Apply leaderboard layout")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var name in new[] { "Title", "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var rects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
            if (name == "Title")
            {
                var rank = rects.First(r => r.name == "RankingsBtn");
                if (PrefabUtility.IsPartOfPrefabInstance(rank.gameObject))
                    PrefabUtility.UnpackPrefabInstance(rank.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                rank.SetParent(rank.GetComponentInParent<Canvas>().transform, false);
                ConfigureRanking(rank.gameObject, LeaderboardTarget.All);
                Place(rank, 1, -100, -100, 128, 128);
                rank.SetAsLastSibling();
            }
            else
            {
                ApplyGameHeader(scene, rects, name);
                HeaderStatLayoutTools.ApplyScene(scene);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }

    private static void ApplyGameHeader(UnityEngine.SceneManagement.Scene scene, RectTransform[] rects, string name)
    {
        var header = rects.First(r => r.name == "TopUIPanel" || r.name == "TopPanel");
        var canvas = header.GetComponentInParent<Canvas>();
        header.name = "TopUIPanel"; header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
        header.pivot = new Vector2(.5f, 1); header.anchoredPosition = Vector2.zero; header.sizeDelta = new Vector2(0, 300);
        var menu = canvas.GetComponent<GameMenu>() ?? canvas.gameObject.AddComponent<GameMenu>();
        var back = header.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "BackToTitleBtn");
        var reset = header.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "ResetBtn");
        if (menu.popup != null)
        {
            back = menu.popup.GetComponentsInChildren<Button>(true).First(b => b.name == "BackToTitleBtn");
            reset = menu.popup.GetComponentsInChildren<Button>(true).First(b => b.name == "ResetBtn");
            back.transform.SetParent(header, false); reset.transform.SetParent(header, false);
            Object.DestroyImmediate(menu.popup);
        }
        var settingsButton = header.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "SettingBtn");
        if (settingsButton == null)
        {
            var go = new GameObject("SettingBtn", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(header, false);
            settingsButton = go.GetComponent<Button>();
            go.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ResetBtn.prefab").GetComponent<Image>().sprite;
        }
        // Reuse the existing settings icon where present; use the same icon for 10SUM.
        if (name == "10SUM")
        {
            var blockScene = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingPanelBtn.prefab");
            settingsButton.GetComponent<Image>().sprite = blockScene.GetComponent<Image>().sprite;
        }
        Unpack(settingsButton.gameObject);
        var settingsImage = settingsButton.GetComponent<Image>();
        settingsImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath("e31017c6b9cca4bee8b18e3d56a549da"));
        settingsImage.color = new Color(0, .63529414f, 1); settingsImage.type = Image.Type.Simple; settingsImage.preserveAspect = true;
        foreach (Transform child in settingsButton.transform) child.gameObject.SetActive(false);
        var animator = settingsButton.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
        Bind(settingsButton, menu.Open);
        Place((RectTransform)settingsButton.transform, 1, -90, -85, 128, 128);
        var ranking = header.Find("RankingBtn").gameObject;
        ConfigureRanking(ranking, name == "Blockdoku_2D" ? LeaderboardTarget.Blockdoku : name == "10SUM" ? LeaderboardTarget.TenSum : LeaderboardTarget.MineSweeper);
        Place((RectTransform)ranking.transform, 1, -240, -85, 128, 128);
        var font = header.GetComponentInChildren<TextMeshProUGUI>(true).font;
        var popup = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SettingPanelBtn.prefab"), canvas.transform);
        Unpack(popup); popup.name = "GameMenuPopup"; menu.popup = popup;
        var settings = popup.GetComponent<SettingsUI>();
        var popupRect = (RectTransform)popup.transform; popupRect.anchorMin = Vector2.zero; popupRect.anchorMax = Vector2.one; popupRect.offsetMin = popupRect.offsetMax = Vector2.zero;
        popup.GetComponent<Image>().sprite = null; popup.GetComponent<Image>().color = new Color(0, 0, 0, .65f);
        var popupAnimator = popup.GetComponent<Animator>(); if (popupAnimator != null) Object.DestroyImmediate(popupAnimator);
        Bind(popup.GetComponent<Button>(), menu.Close);
        var card = new GameObject("SettingsCard", typeof(RectTransform), typeof(Image), typeof(Button)); card.transform.SetParent(popup.transform, false);
        var cardRect = (RectTransform)card.transform; cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .5f); cardRect.sizeDelta = new Vector2(880, 1010);
        card.GetComponent<Image>().color = new Color32(236, 247, 255, 255);
        Label(card.transform, font, "SETTINGS", 0, -65, 600, 80, 48);
        var sliders = new[] { settings.bgmVolumeSlider, settings.sfxVolumeSlider, settings.vibrationVolumeSlider };
        var buttons = new[] { settings.bgmMuteButton, settings.sfxMuteButton, settings.vibrationMuteButton };
        var labels = new[] { "Background Music", "Sound Effects", "Vibration" };
        for (int i = 0; i < 3; i++)
        {
            float y = -220 - i * 190;
            sliders[i].transform.SetParent(card.transform, false); Place((RectTransform)sliders[i].transform, .5f, 60, y, 530, 60);
            buttons[i].transform.SetParent(card.transform, false); Place((RectTransform)buttons[i].transform, .5f, -320, y, 100, 100);
            buttons[i].onClick = new Button.ButtonClickedEvent(); sliders[i].onValueChanged = new Slider.SliderEvent();
            Label(card.transform, font, labels[i], 30, y + 65, 600, 55, 32);
        }
        foreach (Transform child in popup.transform.Cast<Transform>().ToArray()) if (child != card.transform) Object.DestroyImmediate(child.gameObject);
        if (back == null) back = NewIcon("BackToTitleBtn", "Assets/Prefabs/UI/BackToTitleBtn.prefab", card.transform);
        if (reset == null) reset = NewIcon("ResetBtn", "Assets/Prefabs/UI/ResetBtn.prefab", card.transform);
        Unpack(back.gameObject); Unpack(reset.gameObject);
        back.transform.SetParent(card.transform, false); reset.transform.SetParent(card.transform, false);
        Place((RectTransform)back.transform, .5f, -220, -790, 128, 128); Place((RectTransform)reset.transform, .5f, 220, -790, 128, 128);
        Bind(back, menu.ReturnToTitle); Bind(reset, menu.Restart);
        var closeGo = new GameObject("CloseBtn", typeof(RectTransform), typeof(Image), typeof(Button)); closeGo.transform.SetParent(card.transform, false);
        Place((RectTransform)closeGo.transform, 1, -65, -65, 90, 90); closeGo.GetComponent<Image>().color = new Color32(47, 114, 164, 255);
        var closeText = Label(closeGo.transform, font, "X", 0, -45, 90, 90, 42); closeText.color = Color.white;
        Bind(closeGo.GetComponent<Button>(), menu.Close);
        if (name == "Blockdoku_2D")
        {
            var ui = canvas.GetComponent<UIManager_2D>(); ui.titleButton = back; ui.restartButton = reset; ui.settingsButton = settingsButton;
            if (ui.settingPanel != null) ui.settingPanel.SetActive(false);
            ui.settingPanel = popup;
            Place(ui.scoreText.rectTransform, .5f, 0, -85, 300, 120);
            ui.scoreText.enableAutoSizing = true; ui.scoreText.fontSizeMin = 30; ui.scoreText.fontSizeMax = 85;
            ArrangeBest(ui.bestScoreText, header);
        }
        else if (name == "10SUM")
        {
            var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).First();
            Place(ui.scoreText.rectTransform, .5f, 0, -85, 300, 120); ArrangeBest(ui.bestScoreMainGameText, header);
            ui.scoreText.enableAutoSizing = true; ui.scoreText.fontSizeMin = 30; ui.scoreText.fontSizeMax = 85;
            var timer = rects.First(r => r.name == "TimerBgImg"); Place(timer, 0, 195, -85, 330, 140);
            Place(ui.timerText.rectTransform, .5f, 35, -70, 220, 110); ui.timerText.enableAutoSizing = true; ui.timerText.fontSizeMin = 35; ui.timerText.fontSizeMax = 70;
            Place(rects.First(r => r.name == "TimerImg"), .5f, -120, -70, 60, 60);
        }
        else
        {
            var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MineSweeper_UIManager>(true)).First();
            Place(ui.timerText.rectTransform, 0, 215, -85, 220, 110); ui.timerText.enableAutoSizing = true; ui.timerText.fontSizeMin = 32; ui.timerText.fontSizeMax = 65;
            Place(rects.First(r => r.name == "TimerImg"), 0, 65, -85, 60, 60);
            Place(ui.mineCountText.rectTransform, .5f, 0, -85, 285, 110); ui.mineCountText.enableAutoSizing = true; ui.mineCountText.fontSizeMin = 28; ui.mineCountText.fontSizeMax = 50;
            ui.mineCountText.alignment = TextAlignmentOptions.Center;
            Place(rects.First(r => r.name == "MineImg"), .5f, -185, -85, 55, 55);
            var best = header.Find("HeaderBestTime");
            ui.headerBestTimeText = best != null ? best.GetComponent<TextMeshProUGUI>() : Label(header, font, "Best: --:--", 0, -225, 430, 90, 45);
            ui.headerBestTimeText.name = "HeaderBestTime"; Place(ui.headerBestTimeText.rectTransform, 1, -265, -225, 430, 90); ui.headerBestTimeText.alignment = TextAlignmentOptions.MidlineRight;
        }
        popup.SetActive(false);
        GameMenuVisualFixTools.Fix(menu);
        SafeAreaTopPanelTools.Configure(header);
    }

    private static void ArrangeBest(TextMeshProUGUI best, Transform header)
    {
        best.transform.SetParent(header, false); Place(best.rectTransform, 1, -165, -225, 250, 100);
        best.alignment = TextAlignmentOptions.MidlineRight; best.enableAutoSizing = true; best.fontSizeMin = 26; best.fontSizeMax = 55;
        foreach (var icon in best.GetComponentsInChildren<Image>(true)) Place((RectTransform)icon.transform, .5f, -175, -50, 65, 65);
        if (PrefabUtility.IsPartOfPrefabInstance(best)) PrefabUtility.RecordPrefabInstancePropertyModifications(best);
    }

    private static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string value, float x, float y, float width, float height, float size)
    {
        var go = new GameObject(value, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = new Color32(31, 65, 91, 255); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        Place(text.rectTransform, .5f, x, y, width, height); return text;
    }

    private static Button NewIcon(string name, string path, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent); go.name = name; return go.GetComponent<Button>();
    }

    private static void Unpack(GameObject go)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick = new Button.ButtonClickedEvent(); button.transition = Selectable.Transition.ColorTint;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }

    private static void ConfigureRanking(GameObject go, LeaderboardTarget target)
    {
        foreach (Transform child in go.transform) child.gameObject.SetActive(false);
        go.layer = 5;
        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUIPackCartoon/Demo/Sprites/Icons/Icons Colored/Basic/Ranking.png");
        image.color = Color.white; image.preserveAspect = true; image.raycastTarget = true;
        var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        var animator = go.GetComponent<Animator>();
        if (animator != null) animator.enabled = false;
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = image; button.onClick = new Button.ButtonClickedEvent();
        var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
        var action = go.GetComponent<LeaderboardButton>() ?? go.AddComponent<LeaderboardButton>(); action.target = target;
        UnityEventTools.AddPersistentListener(button.onClick, action.Open);
        EditorUtility.SetDirty(button);
        if (PrefabUtility.IsPartOfPrefabInstance(go))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            if (animator != null) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
    }

    private static void Place(RectTransform rect, float anchor, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one;
    }

    public static void Preview()
    {
        Apply();
        foreach (var name in new[] { "Title", "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var cameraGo = new GameObject("Preview Camera", typeof(Camera));
            foreach (var ui in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UIManager>(true)))
                if (ui.gameOverPanel != null) ui.gameOverPanel.SetActive(false);
            var camera = cameraGo.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographic = true; camera.orthographicSize = 960;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
            var texture = new RenderTexture(1080, 1920, 24); camera.targetTexture = texture;
            foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            {
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            }
            Canvas.ForceUpdateCanvases();
            foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMeshProUGUI>(true)))
                if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var png = new Texture2D(1080, 1920, TextureFormat.RGB24, false); png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CasualPartyVerification", name + "-header.png"), png.EncodeToPNG());
            var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenu>(true)).FirstOrDefault();
            if (menu != null)
            {
                menu.popup.SetActive(true); Canvas.ForceUpdateCanvases(); camera.Render();
                png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CasualPartyVerification", name + "-menu.png"), png.EncodeToPNG());
            }
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(png); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraGo);
        }
    }
}
