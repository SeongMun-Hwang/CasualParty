using System.IO;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BlockdokuEffectPreviewTools
{
    public static void Capture()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Blockdoku_2D.unity");
        var grid = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GridManager_2D>(true)).Single();
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
        var texture = new RenderTexture(1080, 1920, 24);
        camera.targetTexture = texture;
        foreach (var canvas in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
            if (canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        if (grid.symmetryEffectContainer.GetComponent<Canvas>() == null) grid.symmetryEffectContainer.gameObject.AddComponent<Canvas>();
        var go = new GameObject("Effect preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(BlockdokuBorderEffect));
        go.layer = grid.symmetryEffectContainer.gameObject.layer;
        go.transform.SetParent(grid.symmetryEffectContainer, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var effect = go.GetComponent<BlockdokuBorderEffect>();
        effect.Configure(grid.symmetryEffectPrefab.GetComponent<Image>());
        Canvas.ForceUpdateCanvases();
        foreach (var header in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SafeAreaTopPanel>(true))) header.RefreshBackground();
        foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMeshProUGUI>(true)))
            if (text.gameObject.activeInHierarchy) text.ForceMeshUpdate();
        float w = grid.symmetryEffectContainer.rect.width * .5f, h = grid.symmetryEffectContainer.rect.height * .5f;
        var tl = new Vector3(-w, h); var tr = new Vector3(w, h); var bl = new Vector3(-w, -h); var br = new Vector3(w, -h);
        foreach (bool fullClear in new[] { false, true })
        {
            var first = fullClear ? new[] { tl, tr, br, bl, tl } : new[] { new Vector3(0, h), tr, br, new Vector3(0, -h) };
            var second = fullClear ? new[] { br, bl, tl, tr, br } : new[] { new Vector3(0, h), tl, bl, new Vector3(0, -h) };
            effect.Play(first, second, new Color32(245, 130, 63, 255), new Color32(42, 185, 230, 255), fullClear ? 2.5f : 1, grid.ghostStepDistance, grid.trailExist);
            int frames = fullClear ? 72 : 24;
            for (int frame = 1; frame <= frames; frame++) DOTween.Goto(effect, frame / 60f, false);
            Canvas.ForceUpdateCanvases();
            effect.canvasRenderer.cull = false;
            effect.Rebuild(CanvasUpdate.PreRender);
            Debug.Log($"[Border preview] stamps={effect.ActiveTrailCount}, vertices={effect.canvasRenderer.GetMesh()?.vertexCount}, cull={effect.canvasRenderer.cull}, canvas={effect.canvas.name}, texture={effect.mainTexture.name}");
            camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = texture;
            var png = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0); png.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "CasualPartyVerification", fullClear ? "Blockdoku-full-clear-optimized.png" : "Blockdoku-symmetry-optimized.png"), png.EncodeToPNG());
            RenderTexture.active = previous;
            Object.DestroyImmediate(png);
        }
        effect.Stop();
        camera.targetTexture = null;
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(go);
    }
}
