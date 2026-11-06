using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SafeAreaTopPanelTools
{
    public static void Apply()
    {
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            var header = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).First(r => r.name == "TopUIPanel");
            Configure(header);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    public static void Configure(RectTransform header)
    {
        var safeArea = header.GetComponent<SafeAreaTopPanel>() ?? header.gameObject.AddComponent<SafeAreaTopPanel>();
        header.anchoredPosition = new Vector2(0, -safeArea.topPadding);
    }
}
