using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CasualButtonStyleTools
{
    private const string Pack = "Assets/GUIPackCartoon/Demo/Sprites/";
    private static readonly Color Ink = new Color32(33, 91, 143, 255);

    [MenuItem("Tools/Casual Party/Apply casual button style")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (GameMenuPrefabTools.Exists) GameMenuPrefabTools.EditShared(menu => ApplyScene(menu.gameObject.scene));
        foreach (var name in new[] { "Title", "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            ApplyScene(scene);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    public static void ApplyScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var difficultyButtons = roots.SelectMany(r => r.GetComponentsInChildren<MineSweeper_UIManager>(true))
            .SelectMany(ui => new[] { ui.beginnerButton, ui.intermediateButton, ui.advancedButton }).ToArray();
        foreach (var button in roots.SelectMany(r => r.GetComponentsInChildren<Button>(true)).ToArray())
        {
            var popup = button.GetComponentInParent<GameMenuPopup>(true);
            if (popup != null && GameMenuPrefabTools.IsSharedInstance(popup.gameObject)) continue;
            if (difficultyButtons.Contains(button)) continue;
            string icon = null;
            if (button.GetComponent<LeaderboardButton>() != null) icon = White("Basic/Ranking");
            else if (button.name == "SettingBtn") icon = "Assets/Sprites/CartoonGUI/Settings.png";
            else if (button.name == "CloseBtn" || button.name == "NoBtn") icon = White("Basic/X Icon - Rounded");
            else
            {
                var methods = Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Where(i => button.onClick.GetPersistentTarget(i) != null && button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off).Select(button.onClick.GetPersistentMethodName).ToArray();
                if (button.name == "BackToTitleBtn" || button.name == "TitleBtn" || methods.Any(m => m == "ReturnToTitle" || m == "GoToTitle" || m == "BackToTitle" || m == "BacktoTitleOnClicked")) icon = White("Navigation/Home");
                else if (button.name == "ResetBtn" || button.name == "RetryBtn" || methods.Any(m => m == "Restart" || m == "RestartGame" || m == "ResetPanelYes" || m == "TriggerGameRestart")) icon = "Assets/Sprites/CartoonGUI/Repeat.png";
            }
            if (icon != null) Style(button, Load(icon));
        }
        foreach (var block in roots.SelectMany(r => r.GetComponentsInChildren<UIManager_2D>(true)))
        {
            if (block.GameOverPanelYesButton != null) Style(block.GameOverPanelYesButton, Load(White("Navigation/Home")));
            if (block.ResetPanelYesButton != null) Style(block.ResetPanelYesButton, Load("Assets/Sprites/CartoonGUI/Repeat.png"));
        }
        foreach (var menu in roots.SelectMany(r => r.GetComponentsInChildren<GameMenu>(true)))
        {
            if (GameMenuPrefabTools.IsSharedInstance(menu.popup)) continue;
            var settings = menu.popup.GetComponent<SettingsUI>();
            settings.bgmOn = Load(White("Media/Sound")); settings.bgmOff = Load(White("Media/Sound Off"));
            settings.sfxOn = Load(White("Media/Music")); settings.sfxOff = Load("Assets/Sprites/UI/Music-Note-Off.png");
            // The project has dedicated vibration on/off assets; retain their recognizable phone symbol.
            settings.vibrationOn = Load("Assets/Sprites/gpt/vibration_on.png"); settings.vibrationOff = Load("Assets/Sprites/gpt/vibration_off.png");
            settings.bgmMuteButtonIcon = Style(settings.bgmMuteButton, settings.bgmOn);
            settings.sfxMuteButtonIcon = Style(settings.sfxMuteButton, settings.sfxOn);
            settings.vibrationMuteButtonIcon = Style(settings.vibrationMuteButton, settings.vibrationOn);
            GameMenuVisualFixTools.Fix(menu);
            GameMenuLayoutTools.Configure(menu);
        }
        foreach (var mine in roots.SelectMany(r => r.GetComponentsInChildren<MineSweeper_UIManager>(true)))
        {
            // Give the paired zoom controls matching faces and enlarge the shorter minus hit area.
            ((RectTransform)mine.zoomInButton.transform).sizeDelta = new Vector2(200, 200);
            ((RectTransform)mine.zoomOutButton.transform).sizeDelta = new Vector2(200, 200);
            Style(mine.zoomInButton, Load(White("Basic/Plus")));
            var existingMinus = ExistingIcon(mine.zoomOutButton);
            var minus = Style(mine.zoomOutButton, existingMinus);
            if (existingMinus == null)
            {
                minus.rectTransform.anchorMin = new Vector2(.24f, .44f);
                minus.rectTransform.anchorMax = new Vector2(.76f, .56f);
            }
            if (mine.resetCameraButton != null) Style(mine.resetCameraButton, Load("Assets/Sprites/CartoonGUI/Repeat.png"));
        }
    }

    private static string White(string name) => Pack + "Icons/Icons White/" + name + ".png";
    private static Sprite Load(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) throw new System.InvalidOperationException("Missing button sprite: " + path);
        return sprite;
    }

    private static Sprite ExistingIcon(Button button)
    {
        var icon = button.transform.Find("Icon");
        return icon != null ? icon.GetComponent<Image>().sprite : button.GetComponent<Image>().sprite;
    }

    private static Image Style(Button button, Sprite sprite)
    {
        if (button == null) throw new System.InvalidOperationException("A button reference is missing.");
        // Unpack only the button instance when necessary, keeping every existing click/event binding.
        if (PrefabUtility.IsAnyPrefabInstanceRoot(button.gameObject))
            PrefabUtility.UnpackPrefabInstance(button.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var background = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
        background.sprite = Load(Pack + "Buttons/Square/Light.png");
        background.color = Color.white; background.type = Image.Type.Simple; background.preserveAspect = true; background.raycastTarget = true;
        var child = button.transform.Find("Icon");
        if (child == null)
        {
            child = new GameObject("Icon", typeof(RectTransform), typeof(Image)).transform;
            child.SetParent(button.transform, false);
        }
        var rect = (RectTransform)child;
        rect.anchorMin = new Vector2(.24f, .24f); rect.anchorMax = new Vector2(.76f, .76f);
        rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        var image = child.GetComponent<Image>(); image.sprite = sprite; image.color = Ink; image.preserveAspect = true; image.raycastTarget = false;
        child.gameObject.SetActive(true); child.SetAsLastSibling();
        foreach (var text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            text.gameObject.SetActive(false);
        var animator = button.GetComponent<Animator>(); if (animator != null) animator.enabled = false;
        button.targetGraphic = background; button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(.95f, .98f, 1);
        colors.selectedColor = Color.white; colors.pressedColor = new Color(.77f, .87f, .97f); colors.disabledColor = new Color(.7f, .7f, .7f, .5f);
        colors.fadeDuration = .08f; colors.colorMultiplier = 1; button.colors = colors;
        if (PrefabUtility.IsPartOfPrefabInstance(button))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            PrefabUtility.RecordPrefabInstancePropertyModifications(background);
            if (animator != null) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
        return image;
    }
}
