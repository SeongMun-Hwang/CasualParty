using UnityEditor;
using UnityEngine;

public sealed class StackTowerBackgroundPreviewWindow : EditorWindow
{
    private static readonly string[] Zones = { "땅", "마을", "산", "하늘", "우주" };
    private StackTowerBackground background;
    private float progress, direction = 1;
    private bool preview, autoPlay;
    private double previousTime;
    private float secondsPerTour = 35;

    [MenuItem("Tools/Casual Party/Stack Tower Background Preview")]
    public static void Open() => GetWindow<StackTowerBackgroundPreviewWindow>("배경 미리보기");

    private void OnEnable() { EditorApplication.update += Tick; previousTime = EditorApplication.timeSinceStartup; }
    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        if (background != null) background.ClearPreview();
        EditorApplication.QueuePlayerLoopUpdate();
    }

    private void OnGUI()
    {
        if (background == null) background = Object.FindFirstObjectByType<StackTowerBackground>();
        EditorGUILayout.HelpBox("StackTower 씬의 Game 창에서 확인하세요. Play 없이도 슬라이더와 자동 재생으로 전체 배경을 볼 수 있습니다. 점수와 블록에는 영향을 주지 않습니다.", MessageType.Info);
        if (background == null)
        {
            EditorGUILayout.HelpBox("먼저 Assets/Scenes/StackTower.unity 씬을 열어 주세요.", MessageType.Warning);
            return;
        }
        EditorGUILayout.ObjectField("배경", background, typeof(StackTowerBackground), true);
        bool enabled = EditorGUILayout.Toggle("미리보기 사용", preview);
        if (enabled != preview) { preview = enabled; if (!preview) StopPreview(); else Apply(); }
        EditorGUI.BeginChangeCheck();
        progress = EditorGUILayout.Slider("고도", progress, 0, 1);
        if (EditorGUI.EndChangeCheck()) { preview = true; Apply(); }
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < Zones.Length; i++)
            if (GUILayout.Button(Zones[i])) { progress = i / 4f; preview = true; autoPlay = false; Apply(); }
        EditorGUILayout.EndHorizontal();
        autoPlay = EditorGUILayout.Toggle("자동 왕복 재생", autoPlay);
        secondsPerTour = EditorGUILayout.Slider("편도 시간 (초)", secondsPerTour, 10, 90);
        if (autoPlay && !preview) { preview = true; Apply(); }
        if (GUILayout.Button("미리보기 종료 / 실제 고도로 복귀")) { preview = false; StopPreview(); }
    }

    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min(.1f, (float)(now - previousTime)); previousTime = now;
        if (!preview || background == null || !autoPlay) return;
        if (autoPlay)
        {
            progress += direction * dt / Mathf.Max(1, secondsPerTour);
            if (progress >= 1) { progress = 1; direction = -1; }
            if (progress <= 0) { progress = 0; direction = 1; }
        }
        Apply();
        Repaint();
    }

    private void Apply()
    {
        background.SetPreview(progress);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private void StopPreview()
    {
        autoPlay = false;
        background.ClearPreview();
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}
