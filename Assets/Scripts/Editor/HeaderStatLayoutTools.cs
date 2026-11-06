using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class HeaderStatLayoutTools
{
    [MenuItem("Tools/Casual Party/Align header icons and values")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            ApplyScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    public static void ApplyScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var header = roots.SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).First(r => r.name == "TopUIPanel");
        var block = roots.SelectMany(r => r.GetComponentsInChildren<UIManager_2D>(true)).FirstOrDefault();
        var ten = roots.SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).FirstOrDefault();
        var mine = roots.SelectMany(r => r.GetComponentsInChildren<MineSweeper_UIManager>(true)).FirstOrDefault();
        if (block != null) Best(header, block.bestScoreText);
        if (ten != null)
        {
            Best(header, ten.bestScoreMainGameText);
            var background = header.Find("TimerBgImg");
            var icon = background.GetComponentsInChildren<Image>(true).First(i => i.transform != background);
            var display = Pair(background, "TimerDisplay", ten.timerText, icon, TextAnchor.MiddleCenter, 60, 70);
            display.anchorMin = Vector2.zero; display.anchorMax = Vector2.one;
            display.offsetMin = display.offsetMax = Vector2.zero;
            display.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(16, 16, 0, 0);
        }
        if (mine != null)
        {
            var images = header.GetComponentsInChildren<Image>(true);
            var timer = images.First(i => i.name == "TimerImg");
            var count = images.First(i => i.name == "MineImg");
            Position(Pair(header, "TimerDisplay", mine.timerText, timer, TextAnchor.MiddleLeft, 60, 65), 0, 30, 330, -85, 110);
            Position(Pair(header, "MineCountDisplay", mine.mineCountText, count, TextAnchor.MiddleCenter, 55, 50), .5f, -165, 330, -85, 110);
        }
    }

    private static void Best(Transform header, TextMeshProUGUI text)
    {
        var existing = header.Find("BestScoreDisplay");
        var icon = text.GetComponentInChildren<Image>(true);
        if (icon == null && existing != null) icon = existing.GetComponentInChildren<Image>(true);
        Position(Pair(header, "BestScoreDisplay", text, icon, TextAnchor.MiddleRight, 65, 55), 1, -480, 440, -225, 100);
    }

    private static RectTransform Pair(Transform parent, string name, TextMeshProUGUI text, Image icon, TextAnchor alignment, float iconSize, float fontSize)
    {
        // Keep the existing text and image components so game/UI references remain valid.
        if (PrefabUtility.IsAnyPrefabInstanceRoot(text.gameObject))
            PrefabUtility.UnpackPrefabInstance(text.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var found = parent.Find(name);
        var root = found != null ? (RectTransform)found : (RectTransform)new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup)).transform;
        root.SetParent(parent, false); root.gameObject.layer = 5;
        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 12; layout.childAlignment = alignment;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childScaleWidth = layout.childScaleHeight = false;
        layout.padding = new RectOffset();
        icon.transform.SetParent(root, false); icon.transform.SetAsFirstSibling();
        text.transform.SetParent(root, false); text.transform.SetSiblingIndex(1);
        icon.transform.localScale = text.transform.localScale = Vector3.one;
        icon.preserveAspect = true; icon.raycastTarget = text.raycastTarget = false;
        var iconLayout = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.ignoreLayout = false; iconLayout.minWidth = iconLayout.preferredWidth = iconSize;
        iconLayout.minHeight = iconLayout.preferredHeight = iconSize; iconLayout.flexibleWidth = iconLayout.flexibleHeight = 0;
        var valueLayout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
        valueLayout.ignoreLayout = false; valueLayout.minWidth = 0; valueLayout.preferredWidth = -1;
        valueLayout.minHeight = valueLayout.preferredHeight = root.rect.height > 0 ? Mathf.Min(100, root.rect.height) : 100;
        valueLayout.flexibleWidth = valueLayout.flexibleHeight = 0;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true; text.fontSize = text.fontSizeMax = fontSize; text.fontSizeMin = 26;
        return root;
    }

    private static void Position(RectTransform root, float anchor, float left, float width, float y, float height)
    {
        root.anchorMin = root.anchorMax = new Vector2(anchor, 1); root.pivot = new Vector2(0, .5f);
        root.anchoredPosition = new Vector2(left, y); root.sizeDelta = new Vector2(width, height); root.localScale = Vector3.one;
    }
}
