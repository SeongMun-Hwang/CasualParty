using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RankingsPanel : MonoBehaviour
{
    private TextMeshProUGUI status;
    private Button signIn;
    private PlayGamesService service;

    public static void Open()
    {
        if (FindFirstObjectByType<RankingsPanel>() != null) return;
        var root = new GameObject("Rankings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<RankingsPanel>().Build();
    }

    private void Build()
    {
        var sample = FindFirstObjectByType<TextMeshProUGUI>();
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(transform, false);
        Stretch(backdrop.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        backdrop.GetComponent<Image>().color = new Color(0, 0, 0, 0.75f);

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(backdrop.transform, false);
        Stretch(panel.GetComponent<RectTransform>(), new Vector2(0.08f, 0.18f), new Vector2(0.92f, 0.84f));
        panel.GetComponent<Image>().color = new Color32(31, 49, 69, 255);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 30, 30);
        layout.spacing = 16;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        AddText(panel.transform, font, "RANKINGS", 54, 80);
        status = AddText(panel.transform, font, "Sign in to use rankings.", 30, 110);
        signIn = AddButton(panel.transform, font, "Google Play Games Sign In", () => service?.SignIn());
        AddBoard(panel.transform, font, "Blockdoku", LeaderboardKind.Blockdoku);
        AddBoard(panel.transform, font, "10SUM", LeaderboardKind.TenSum);
        AddBoard(panel.transform, font, "Minesweeper - Beginner", LeaderboardKind.MineSweeperBeginner);
        AddBoard(panel.transform, font, "Minesweeper - Intermediate", LeaderboardKind.MineSweeperIntermediate);
        AddBoard(panel.transform, font, "Minesweeper - Advanced", LeaderboardKind.MineSweeperAdvanced);
        AddButton(panel.transform, font, "Close", () => Destroy(gameObject));
        service = PlayGamesService.Instance;
        if (service != null) service.Changed += Refresh;
        Refresh();
    }

    private void AddBoard(Transform parent, TMP_FontAsset font, string label, LeaderboardKind kind)
    {
        AddButton(parent, font, label, () => service?.ShowLeaderboard(kind));
    }

    private void Refresh()
    {
        status.text = service != null ? service.Status : "Rankings are not available yet.";
        if (service != null && !string.IsNullOrEmpty(service.ScoreStatus)) status.text += "\n" + service.ScoreStatus;
        signIn.interactable = service != null && !service.IsSigningIn && !service.IsAuthenticated;
    }

    private static Button AddButton(Transform parent, TMP_FontAsset font, string label, Action action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color32(48, 101, 145, 255);
        go.GetComponent<LayoutElement>().preferredHeight = 90;
        var text = AddText(go.transform, font, label, 34, 0);
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(() => action());
        return button;
    }

    private static TextMeshProUGUI AddText(Transform parent, TMP_FontAsset font, string value, float size, float height)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy() { if (service != null) service.Changed -= Refresh; }
}
