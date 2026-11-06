using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class GameMenuPrefabTools
{
    public const string PrefabPath = "Assets/Prefabs/UI/GameMenuPopup.prefab";
    public static bool Exists => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;

    [MenuItem("Tools/Casual Party/Connect shared game menu prefab")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!Exists)
        {
            var canonical = EditorSceneManager.OpenScene("Assets/Scenes/10SUM.unity");
            var menu = FindMenu(canonical);
            BindInternalEvents(menu.popup);
            PrefabUtility.SaveAsPrefabAsset(menu.popup, PrefabPath);
        }
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            Connect(FindMenu(scene));
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    private static GameMenu FindMenu(UnityEngine.SceneManagement.Scene scene) =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenu>(true)).Single();

    public static bool IsSharedInstance(GameObject popup) => popup != null && PrefabUtility.IsPartOfPrefabInstance(popup) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(popup) == PrefabPath;

    public static void Connect(GameMenu menu)
    {
        if (!Exists) return;
        if (!IsSharedInstance(menu.popup))
        {
            var previous = menu.popup;
            var parent = previous != null ? previous.transform.parent : menu.transform;
            var index = previous != null ? previous.transform.GetSiblingIndex() : parent.childCount;
            menu.popup = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), parent);
            menu.popup.transform.SetSiblingIndex(index);
            if (previous != null) Object.DestroyImmediate(previous);
        }
        // Blockdoku keeps these references for its existing confirmation/UI flows.
        var block = menu.GetComponent<UIManager_2D>();
        if (block != null)
        {
            block.settingPanel = menu.popup;
            block.titleButton = Button(menu.popup, "BackToTitleBtn");
            block.restartButton = Button(menu.popup, "ResetBtn");
        }
    }

    private static Button Button(GameObject root, string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name);

    private static void BindInternalEvents(GameObject root)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(root)) PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var actions = root.GetComponent<GameMenuPopup>() ?? root.AddComponent<GameMenuPopup>();
        Bind(root.GetComponent<Button>(), actions.Close);
        Bind(Button(root, "CloseBtn"), actions.Close);
        Bind(Button(root, "BackToTitleBtn"), actions.ReturnToTitle);
        Bind(Button(root, "ResetBtn"), actions.Restart);
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    // Legacy layout/style tools edit the shared asset instead of adding per-scene overrides.
    public static void EditShared(Action<GameMenu> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var context = root.AddComponent<GameMenu>(); context.popup = root;
            edit(context);
            Object.DestroyImmediate(context);
            BindInternalEvents(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
