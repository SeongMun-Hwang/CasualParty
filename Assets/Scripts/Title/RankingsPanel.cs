using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RankingsPanel : MonoBehaviour
{
    private TextMeshProUGUI status;
    private Button signIn;
    private PlayGamesService service;
    private bool loginChoice;
    private LeaderboardKind? requestedKind;
    private bool minesweeperSelection;
    private bool openAfterSignIn;
    private static RankingsPanel current;
    public static bool IsOpen => current != null;
    private readonly List<KeyValuePair<Button, LeaderboardKind>> boardButtons = new List<KeyValuePair<Button, LeaderboardKind>>();

    public static void Open()
    {
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.ShowAllLeaderboards();
    }

    public static void OpenLoginChoice() => Open(true, null, false);
    public static void OpenForLeaderboard(LeaderboardKind? kind) => Open(false, kind, false);
    public static void OpenMinesweeper() => Open(false, null, true);

    private static void Open(bool offerLogin, LeaderboardKind? kind, bool minesweeper)
    {
        if (current != null) return;
        var root = new GameObject("Rankings", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        var panel = root.AddComponent<RankingsPanel>();
        current = panel;
        panel.loginChoice = offerLogin;
        panel.requestedKind = kind;
        panel.minesweeperSelection = minesweeper;
        panel.Build();
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
        service = PlayGamesService.Instance;
        if (loginChoice)
        {
            AddText(panel.transform, font, "GOOGLE PLAY GAMES", 48, 100);
            AddText(panel.transform, font,
                "Sign in to view rankings and submit scores using your Play Games profile.\n\nYou can play all games without signing in. Local progress stays on this device.", 34, 380);
            AddButton(panel.transform, font, "Sign in and use rankings", () =>
            {
                service?.SignIn();
                Destroy(gameObject);
            });
            AddButton(panel.transform, font, "Play without signing in", Close);
            return;
        }
        string heading = minesweeperSelection ? "MINESWEEPER RANKINGS" : requestedKind.HasValue ? NameFor(requestedKind.Value) + " RANKING" : "ALL RANKINGS";
        AddText(panel.transform, font, heading, 42, 100);
        AddText(panel.transform, font, "Sign in to view Google Play Games rankings.\nYou can keep playing without signing in.", 32, 150);
        status = AddText(panel.transform, font, "Sign in to use rankings.", 30, 140);
        signIn = AddButton(panel.transform, font, "Google Play Games Sign In", () =>
        {
            openAfterSignIn = !minesweeperSelection;
            service?.SignIn();
        });
        if (minesweeperSelection)
        {
            AddBoard(panel.transform, font, "Beginner", LeaderboardKind.MineSweeperBeginner);
            AddBoard(panel.transform, font, "Intermediate", LeaderboardKind.MineSweeperIntermediate);
            AddBoard(panel.transform, font, "Advanced", LeaderboardKind.MineSweeperAdvanced);
        }
        AddButton(panel.transform, font, "Close", Close);
        if (service != null) service.Changed += Refresh;
        Refresh();
    }

    private void AddBoard(Transform parent, TMP_FontAsset font, string label, LeaderboardKind kind)
    {
        var button = AddButton(parent, font, label, () =>
        {
            HideForNativeUI();
            service?.ShowLeaderboard(kind);
        });
        boardButtons.Add(new KeyValuePair<Button, LeaderboardKind>(button, kind));
    }

    private void Refresh()
    {
        status.text = service != null ? service.Status : "Rankings are not available yet.";
        if (service != null && !string.IsNullOrEmpty(service.ScoreStatus)) status.text += "\n" + service.ScoreStatus;
        signIn.interactable = service != null && !service.IsSigningIn && !service.IsAuthenticated;
        foreach (var item in boardButtons)
            item.Key.interactable = service != null && service.IsAuthenticated && service.IsConfigured(item.Value);
        if (service != null && service.IsAuthenticated && openAfterSignIn)
        {
            openAfterSignIn = false;
            HideForNativeUI();
            if (requestedKind.HasValue) service.ShowLeaderboard(requestedKind.Value);
            else service.ShowAllLeaderboards();
        }
    }

    private void HideForNativeUI()
    {
        if (service != null) service.Changed -= Refresh;
        if (current == this) current = null;
        Destroy(gameObject);
    }

    public static string NameFor(LeaderboardKind kind)
    {
        switch (kind)
        {
            case LeaderboardKind.Blockdoku: return "BLOCKDOKU";
            case LeaderboardKind.TenSum: return "10SUM";
            case LeaderboardKind.MineSweeperBeginner: return "MINESWEEPER - BEGINNER";
            case LeaderboardKind.MineSweeperIntermediate: return "MINESWEEPER - INTERMEDIATE";
            default: return "MINESWEEPER - ADVANCED";
        }
    }

    public static bool TryClose()
    {
        var panel = current;
        if (panel == null) return false;
        panel.Close();
        return true;
    }

    private void Close()
    {
        if (loginChoice) service?.ContinueAsGuest();
        Destroy(gameObject);
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

    private void OnDestroy()
    {
        if (service != null) service.Changed -= Refresh;
        if (current == this) current = null;
    }
}
