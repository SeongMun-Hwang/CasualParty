using System.Linq;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameMenuLayoutTools
{
    public static void Apply()
    {
        if (GameMenuPrefabTools.Exists) { GameMenuPrefabTools.EditShared(Configure); return; }
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var menu in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenu>(true))) Configure(menu);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    public static void Configure(GameMenu menu)
    {
        var card = (RectTransform)menu.popup.transform.Find("SettingsCard");
        card.sizeDelta = new Vector2(760, 1010);
        var settings = menu.popup.GetComponent<SettingsUI>();
        var sliders = new[] { settings.bgmVolumeSlider, settings.sfxVolumeSlider, settings.vibrationVolumeSlider };
        var buttons = new[] { settings.bgmMuteButton, settings.sfxMuteButton, settings.vibrationMuteButton };
        var labels = new[] { "Background Music", "Sound Effects", "Vibration" };
        var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
        var heading = texts.First(t => t.text == "SETTINGS");
        Place(heading.rectTransform, .5f, -40, -75, 430, 90); heading.fontSize = 56;
        for (int i = 0; i < 3; i++)
        {
            float y = -245 - i * 205;
            Place((RectTransform)sliders[i].transform, .5f, 65, y, 400, 70);
            Place((RectTransform)buttons[i].transform, .5f, -270, y, 136, 136);
            EnlargeIcon(buttons[i]);
            var text = texts.First(t => t.text == labels[i]);
            Place(text.rectTransform, .5f, 65, y + 80, 460, 65); text.fontSize = 42;
        }
        var actions = card.GetComponentsInChildren<Button>(true);
        var back = actions.First(b => b.name == "BackToTitleBtn");
        var reset = actions.First(b => b.name == "ResetBtn");
        var close = actions.First(b => b.name == "CloseBtn");
        Place((RectTransform)back.transform, .5f, -180, -850, 160, 160);
        Place((RectTransform)reset.transform, .5f, 180, -850, 160, 160);
        Place((RectTransform)close.transform, 1, -75, -75, 110, 110);
        EnlargeIcon(back); EnlargeIcon(reset); EnlargeIcon(close);
    }

    private static void EnlargeIcon(Button button)
    {
        var icon = button.transform.Find("Icon") as RectTransform;
        if (icon == null) return;
        icon.anchorMin = new Vector2(.18f, .18f); icon.anchorMax = new Vector2(.82f, .82f);
        icon.offsetMin = icon.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float anchor, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }
}
