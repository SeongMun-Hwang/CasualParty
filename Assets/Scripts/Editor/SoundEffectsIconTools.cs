using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SoundEffectsIconTools
{
    public static void Apply()
    {
        const string offPath = "Assets/Sprites/UI/Music-Note-Off.png";
        AssetDatabase.ImportAsset(offPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(offPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        // The clean generated white-on-black mask imports as a transparent UI sprite.
        importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var on = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUIPackCartoon/Demo/Sprites/Icons/Icons White/Media/Music.png");
        var off = AssetDatabase.LoadAssetAtPath<Sprite>(offPath);
        if (GameMenuPrefabTools.Exists)
        {
            GameMenuPrefabTools.EditShared(menu => Configure(menu, on, off));
            return;
        }
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var menu in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenu>(true)))
            {
                Configure(menu, on, off);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }

    private static void Configure(GameMenu menu, Sprite on, Sprite off)
    {
        var ui = menu.popup.GetComponent<SettingsUI>();
        ui.bgmOn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUIPackCartoon/Demo/Sprites/Icons/Icons White/Media/Sound.png");
        ui.bgmOff = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GUIPackCartoon/Demo/Sprites/Icons/Icons White/Media/Sound Off.png");
        ui.bgmMuteButtonIcon.sprite = ui.bgmOn;
        ui.sfxOn = on; ui.sfxOff = off; ui.sfxMuteButtonIcon.sprite = on;
    }
}
