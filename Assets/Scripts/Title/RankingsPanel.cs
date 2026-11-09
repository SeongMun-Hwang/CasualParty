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
    private static RankingsPanel current;
    public static bool IsOpen => current != null;
    private readonly List<KeyValuePair<Button, LeaderboardKind>> boardButtons = new List<KeyValuePair<Button, LeaderboardKind>>();

    public static void Open()
    {
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.ShowAllLeaderboards();
    }

    public static void OpenLoginChoice()
    {
        var service = PlayGamesService.Instance;
        if (service != null && service.IsAuthenticated) { service.ShowAllLeaderboards(); return; }
        Open(true, null, false);
    }

    public static void OpenForLeaderboard(LeaderboardKind? kind)
    {
        var service = PlayGamesService.Instance;
        if (CanOpenDirectly(service, kind)) { ShowBoard(service, kind); return; }
        Open(false, kind, false);
    }

    public static void OpenMinesweeper()
    {
        var service = PlayGamesService.Instance;
        if (service != null && service.IsAuthenticated) { service.ShowAllLeaderboards(); return; }
        Open(false, null, true);
    }

    private static bool CanOpenDirectly(PlayGamesService service, LeaderboardKind? kind) =>
        service != null && service.IsAuthenticated && service.SupportsNativeLeaderboards &&
        (!kind.HasValue || service.IsConfigured(kind.Value));

    private static void ShowBoard(PlayGamesService service, LeaderboardKind? kind)
    {
        if (kind.HasValue) service.ShowLeaderboard(kind.Value);
        else service.ShowAllLeaderboards();
    }

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

    private static readonly Color Ink = new Color32(36, 84, 125, 255);
    private RankingPopupStyle style;
    private RectTransform card, safeFrame;
    private TextMeshProUGUI signInLabel, statusHeading;
    private LayoutElement statusHeight;
    private Image statusSurface;

    private void Build()
    {
        style = Resources.Load<RankingPopupStyle>("RankingPopupStyle");
        var sample = FindFirstObjectByType<TextMeshProUGUI>();
        TMP_FontAsset font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
        var backdrop = MakeRect("Backdrop", transform);
        Stretch(backdrop, Vector2.zero, Vector2.one);
        var dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = new Color(.035f, .10f, .17f, .68f);
        safeFrame = MakeRect("Safe Area", backdrop);
        card = MakeRect("Panel", safeFrame);
        card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
        card.pivot = new Vector2(.5f, .5f);
        RoundedImage(card, new Color32(92, 145, 181, 255));
        var shadow = MakeRect("Shadow", card);
        Stretch(shadow, Vector2.zero, Vector2.one);
        shadow.anchoredPosition = new Vector2(0, -12);
        RoundedImage(shadow, new Color(0, .08f, .15f, .23f));
        shadow.SetAsFirstSibling();
        var surface = MakeRect("Surface", card);
        Stretch(surface, Vector2.zero, Vector2.one);
        surface.offsetMin = new Vector2(6, 6); surface.offsetMax = new Vector2(-6, -6);
        RoundedImage(surface, new Color32(246, 251, 255, 255));
        var badge = MakeRect("Ranking Badge", card);
        Place(badge, .5f, 0, -94, 136, 136);
        var badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.sprite = style != null ? style.iconButton : null;
        badgeImage.color = Color.white; badgeImage.raycastTarget = false;
        AddIcon(badge, style != null ? style.rankingIcon : null, Ink, .25f);
        var close = MakeRect("Close Button", card);
        Place(close, 1, -62, -62, 88, 88);
        var closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = style != null ? style.iconButton : null;
        var closeButton = close.gameObject.AddComponent<Button>(); closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close); StyleButton(closeButton);
        AddIcon(close, style != null ? style.closeIcon : null, Ink, .30f);
        var heading = AddText(card, font, loginChoice ? "PLAY YOUR WAY" : "RANKINGS", 56, 0);
        Place(heading.rectTransform, .5f, 0, -205, 650, 80);
        var subtitle = AddText(card, font, loginChoice ? "GOOGLE PLAY GAMES" : minesweeperSelection ? "MINESWEEPER" : requestedKind.HasValue ? NameFor(requestedKind.Value) : "ALL GAMES", 27, 0);
        subtitle.color = new Color32(96, 138, 167, 255);
        Place(subtitle.rectTransform, .5f, 0, -265, 700, 60);
        var scrollRect = MakeRect("Body Scroll", card);
        Stretch(scrollRect, Vector2.zero, Vector2.one);
        scrollRect.offsetMin = new Vector2(38, 186); scrollRect.offsetMax = new Vector2(-38, -308);
        var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = MakeRect("Viewport", scrollRect);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
        var content = MakeRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 12); layout.spacing = 20;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;
        var track = MakeRect("Scroll Indicator", scrollRect);
        track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(6, 8); track.offsetMax = new Vector2(14, -8);
        var handle = MakeRect("Handle", track);
        Stretch(handle, Vector2.zero, Vector2.one);
        var handleImage = RoundedImage(handle, new Color(.30f, .56f, .73f, .45f));
        handleImage.raycastTarget = true;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop; scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        service = PlayGamesService.Instance;
        if (loginChoice)
        {
            AddText(content, font, "Compare your best scores\nwith other players.", 38, 125);
            var detail = AddText(content, font, "Sign in to use rankings, or keep playing\nwithout an account.", 29, 110);
            detail.color = new Color32(105, 136, 158, 255);
            AddButton(content, font, "Sign in with Play Games", () => { service?.SignIn(); Destroy(gameObject); });
        }
        else
        {
            var intro = AddText(content, font, minesweeperSelection ? "Choose a difficulty to view its rankings." : "Your next personal best starts here.", 31, 86);
            intro.color = new Color32(105, 136, 158, 255);
            var state = MakeRect("Player Status", content);
            statusSurface = RoundedImage(state, new Color32(226, 240, 250, 255));
            statusHeight = state.gameObject.AddComponent<LayoutElement>(); statusHeight.preferredHeight = 190;
            var profile = MakeRect("Player Icon", state);
            Place(profile, 0, 46, -42, 35, 35);
            AddIcon(profile, style != null ? style.playerIcon : null, new Color32(78, 135, 177, 255), 0);
            statusHeading = AddText(state, font, "PLAYER STATUS", 23, 0);
            statusHeading.alignment = TextAlignmentOptions.Left;
            Stretch(statusHeading.rectTransform, new Vector2(0, 1), Vector2.one);
            statusHeading.rectTransform.offsetMin = new Vector2(80, -65); statusHeading.rectTransform.offsetMax = new Vector2(-28, -18);
            status = AddText(state, font, "Sign in to use rankings.", 30, 0);
            status.alignment = TextAlignmentOptions.Left;
            Stretch(status.rectTransform, Vector2.zero, Vector2.one);
            status.rectTransform.offsetMin = new Vector2(30, 20); status.rectTransform.offsetMax = new Vector2(-30, -72);
            signIn = AddButton(content, font, "Sign in with Play Games", () => service?.SignIn());
            signInLabel = signIn.GetComponentInChildren<TextMeshProUGUI>();
            if (minesweeperSelection)
            {
                var label = AddText(content, font, "SELECT DIFFICULTY", 24, 42);
                label.color = new Color32(105, 136, 158, 255);
                AddBoard(content, font, "Beginner", LeaderboardKind.MineSweeperBeginner);
                AddBoard(content, font, "Intermediate", LeaderboardKind.MineSweeperIntermediate);
                AddBoard(content, font, "Advanced", LeaderboardKind.MineSweeperAdvanced);
            }
            if (service != null) service.Changed += Refresh;
            Refresh();
        }
        var footer = AddButton(card, font, loginChoice ? "Play as guest" : "Close", Close, false);
        var footerRect = (RectTransform)footer.transform;
        footerRect.anchorMin = new Vector2(0, 0); footerRect.anchorMax = new Vector2(1, 0);
        footerRect.pivot = new Vector2(.5f, 0);
        footerRect.offsetMin = new Vector2(50, 62); footerRect.offsetMax = new Vector2(-50, 174);
        var note = AddText(card, font, loginChoice ? "Your progress stays on this device." : "GOOGLE PLAY GAMES", 22, 0);
        note.color = new Color32(116, 146, 165, 255);
        note.rectTransform.anchorMin = new Vector2(0, 0); note.rectTransform.anchorMax = new Vector2(1, 0);
        note.rectTransform.pivot = new Vector2(.5f, 0);
        note.rectTransform.offsetMin = new Vector2(30, 14); note.rectTransform.offsetMax = new Vector2(-30, 55);
        FitPanel();
    }

    private float lastStatusWidth;
    private void LateUpdate()
    {
        FitPanel();
        if (status != null && Mathf.Abs(status.rectTransform.rect.width - lastStatusWidth) > 1) ResizeStatus();
    }

    private void ResizeStatus()
    {
        lastStatusWidth = status.rectTransform.rect.width;
        float width = lastStatusWidth > 0 ? lastStatusWidth : 620;
        statusHeight.preferredHeight = Mathf.Max(190, status.GetPreferredValues(status.text, width, Mathf.Infinity).y + 100);
    }
    private void FitPanel()
    {
        if (card == null || Screen.width <= 0 || Screen.height <= 0) return;
        var safe = Screen.safeArea;
        if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
        var viewport = GetComponent<Canvas>().rootCanvas.pixelRect;
        float banner = AdManager.Instance != null ? AdManager.Instance.BannerHeightPixels : 0;
        if (banner <= 0) banner = viewport.width * 90f / 320f;
        safeFrame.anchorMin = new Vector2(safe.xMin / Screen.width, Mathf.Max(safe.yMin / Screen.height, banner / Mathf.Max(1, viewport.height)));
        safeFrame.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        safeFrame.offsetMin = safeFrame.offsetMax = Vector2.zero;
        var root = (RectTransform)transform;
        float width = root.rect.width * (safeFrame.anchorMax.x - safeFrame.anchorMin.x);
        float height = root.rect.height * (safeFrame.anchorMax.y - safeFrame.anchorMin.y);
        card.sizeDelta = new Vector2(Mathf.Min(840, Mathf.Max(280, width - 64)), Mathf.Min(minesweeperSelection ? 1420 : 1050, Mathf.Max(540, height - 64)));
    }

    private void AddBoard(Transform parent, TMP_FontAsset font, string label, LeaderboardKind kind)
    {
        var button = AddButton(parent, font, label, () =>
        {
            HideForNativeUI();
            service?.ShowLeaderboard(kind);
        }, false);
        var caption = button.GetComponentInChildren<TextMeshProUGUI>();
        caption.alignment = TextAlignmentOptions.Left;
        caption.margin = new Vector4(38, 8, 76, 12);
        var arrow = AddText(button.transform, font, ">", 42, 0);
        var rect = arrow.rectTransform;
        rect.anchorMin = new Vector2(1, 0); rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-72, 8); rect.offsetMax = new Vector2(-20, -12);
        boardButtons.Add(new KeyValuePair<Button, LeaderboardKind>(button, kind));
    }

    private void Refresh()
    {
        if (CanOpenDirectly(service, requestedKind))
        {
            HideForNativeUI();
            ShowBoard(service, requestedKind);
            return;
        }
        status.text = service != null ? service.Status : "Rankings are not available yet.";
        if (service != null && !string.IsNullOrEmpty(service.ScoreStatus)) status.text += "\n" + service.ScoreStatus;
        signIn.interactable = service != null && !service.IsSigningIn && !service.IsAuthenticated;
        bool connected = service != null && service.IsAuthenticated;
        bool connecting = service != null && service.IsSigningIn;
        signIn.gameObject.SetActive(!connected);
        statusHeading.text = connected ? "CONNECTED" : connecting ? "CONNECTING..." : "PLAYER STATUS";
        statusSurface.color = connected ? new Color32(223, 244, 236, 255) : new Color32(226, 240, 250, 255);
        signInLabel.text = connected ? "Signed in" : connecting ? "Signing in..." : "Sign in with Play Games";
        ResizeStatus();
        foreach (var item in boardButtons)
            item.Key.interactable = service != null && service.IsAuthenticated && service.IsConfigured(item.Value);
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

    private Button AddButton(Transform parent, TMP_FontAsset font, string label, Action action, bool primary = true)
    {
        var go = MakeRect(label, parent);
        var background = go.gameObject.AddComponent<Image>();
        background.sprite = style != null ? primary ? style.primaryButton : style.secondaryButton : null;
        background.type = Image.Type.Sliced; background.pixelsPerUnitMultiplier = primary ? 2 : 4;
        background.color = primary ? style != null ? Color.white : new Color32(48, 145, 217, 255) : new Color32(222, 236, 246, 255);
        go.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
        var text = AddText(go, font, label, 33, 0);
        text.color = primary ? Color.white : Ink;
        Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        text.margin = new Vector4(24, 8, 24, 12);
        var button = go.gameObject.AddComponent<Button>(); button.targetGraphic = background;
        button.onClick.AddListener(() => action()); StyleButton(button);
        return button;
    }

    private static void StyleButton(Button button)
    {
        var tint = button.colors;
        tint.normalColor = tint.selectedColor = Color.white;
        tint.highlightedColor = new Color(.95f, .98f, 1);
        tint.pressedColor = new Color(.77f, .87f, .97f);
        tint.disabledColor = new Color(.80f, .84f, .88f, .65f);
        tint.fadeDuration = .08f; button.colors = tint;
    }

    private Image RoundedImage(RectTransform rect, Color tint)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style != null ? style.roundedPanel : null;
        image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 4;
        image.color = tint; image.raycastTarget = false;
        return image;
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Place(RectTransform rect, float anchor, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(anchor, 1);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }

    private static void AddIcon(RectTransform parent, Sprite sprite, Color tint, float inset)
    {
        var rect = MakeRect("Icon", parent);
        Stretch(rect, new Vector2(inset, inset), new Vector2(1 - inset, 1 - inset));
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = tint;
        image.preserveAspect = true; image.raycastTarget = false;
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
        text.color = Ink;
        text.richText = false;
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
