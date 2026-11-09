using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RankingPopupPreviewTools
{
    public static void Capture()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
        var texture = new RenderTexture(1080, 1920, 24); camera.targetTexture = texture;
        foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            if (canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 2; }
        foreach (string variant in new[] { "login", "rankings", "minesweeper" })
        {
            if (variant == "login") RankingsPanel.OpenLoginChoice();
            else if (variant == "minesweeper") RankingsPanel.OpenMinesweeper();
            else RankingsPanel.OpenForLeaderboard(LeaderboardKind.Blockdoku);
            var panel = Object.FindFirstObjectByType<RankingsPanel>();
            var popupCanvas = panel.GetComponent<Canvas>();
            popupCanvas.renderMode = RenderMode.ScreenSpaceCamera; popupCanvas.worldCamera = camera; popupCanvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            typeof(RankingsPanel).GetMethod("FitPanel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
            Canvas.ForceUpdateCanvases();
            if (variant != "login") typeof(RankingsPanel).GetMethod("ResizeStatus", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
            Canvas.ForceUpdateCanvases();
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true)) text.ForceMeshUpdate();
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var png = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "CasualPartyVerification", "Ranking-popup-" + variant + ".png"), png.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(png); Object.DestroyImmediate(panel.gameObject);
        }
        camera.targetTexture = null; Object.DestroyImmediate(texture);
    }
}
