using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameMenuVisualFixTools
{
    public static void Apply()
    {
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var menu in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenu>(true))) Fix(menu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    public static void Fix(GameMenu menu)
    {
        var card = menu.popup.transform.Find("SettingsCard");
        foreach (var surface in new[] { menu.popup.GetComponent<Button>(), card.GetComponent<Button>() })
        {
            // These buttons intercept clicks; hovering/selecting descendants must not tint the panel.
            surface.transition = Selectable.Transition.None;
            var navigation = surface.navigation; navigation.mode = Navigation.Mode.None; surface.navigation = navigation;
            surface.targetGraphic = surface.GetComponent<Image>();
        }
        foreach (var text in card.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (text.transform.parent == card && (text.text == "Title" || text.text == "Restart")) Object.DestroyImmediate(text.gameObject);
    }
}
