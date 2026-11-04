using UnityEditor;
using UnityEngine;

public sealed class PlayGamesSetupWindow : EditorWindow
{
    [MenuItem("CasualParty/Leaderboard Settings")]
    private static void Open()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LeaderboardSettings>("Assets/Resources/LeaderboardSettings.asset");
        if (settings == null)
        {
            settings = CreateInstance<LeaderboardSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/Resources/LeaderboardSettings.asset");
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }
}
