using UnityEditor;
using UnityEngine;

public static class RankingPopupStyleTools
{
    public static void Build()
    {
        const string assetPath = "Assets/Resources/RankingPopupStyle.asset";
        const string pack = "Assets/GUIPackCartoon/Demo/Sprites/";
        var style = AssetDatabase.LoadAssetAtPath<RankingPopupStyle>(assetPath);
        if (style == null) { style = ScriptableObject.CreateInstance<RankingPopupStyle>(); AssetDatabase.CreateAsset(style, assetPath); }
        style.roundedPanel = AssetDatabase.LoadAssetAtPath<Sprite>(pack + "Shapes/Shapes/Rectangle/Rounded Rectangle - 256px.png");
        style.primaryButton = AssetDatabase.LoadAssetAtPath<Sprite>(pack + "Buttons/Rectangles/Blue.png");
        style.secondaryButton = style.roundedPanel;
        style.iconButton = AssetDatabase.LoadAssetAtPath<Sprite>(pack + "Buttons/Square/Light.png");
        style.rankingIcon = AssetDatabase.LoadAssetAtPath<Sprite>(pack + "Icons/Icons White/Basic/Ranking.png");
        style.closeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(pack + "Icons/Icons White/Basic/X Icon - Rounded.png");
        style.playerIcon = AssetDatabase.LoadAssetAtPath<Sprite>(pack + "Icons/Icons White/User/User.png");
        foreach (var sprite in new[] { style.roundedPanel, style.primaryButton, style.secondaryButton, style.iconButton, style.rankingIcon, style.closeIcon, style.playerIcon })
            if (sprite == null) throw new System.InvalidOperationException("Ranking popup style has a missing sprite.");
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssets();
    }
}
