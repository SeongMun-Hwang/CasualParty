using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class CasualPartyRegressionTests
{
    [SerializeField] private string testDirectory;
    private int boundaryCount;
    private Action pendingBoundary;

    private sealed class SaveObservation
    {
        public int SettingsWrites, BoardWrites, Notifications;
        public void Saved(string file) { if (file == "setting.json") SettingsWrites++; if (file == "save.json") BoardWrites++; }
        public void Changed() => Notifications++;
    }

    [Test]
    public void BlockGroupSamplingKeepsActiveVariantsUniformAndSkipsDisabledItems()
    {
        var main = ScriptableObject.CreateInstance<BlockArray>();
        var variant = ScriptableObject.CreateInstance<BlockArray>();
        var disabled = ScriptableObject.CreateInstance<BlockArray>();
        var state = UnityEngine.Random.state;
        try
        {
            var group = new BlockSpawner_2D.BlockSpawnGroup
            {
                mainBlock = new BlockSpawner_2D.BlockSpawnItem { block = main },
                variants = new System.Collections.Generic.List<BlockSpawner_2D.BlockSpawnItem>
                {
                    new BlockSpawner_2D.BlockSpawnItem { block = variant },
                    new BlockSpawner_2D.BlockSpawnItem { block = disabled, isActive = false }, null
                }
            };
            UnityEngine.Random.InitState(2917);
            int mainCount = 0;
            for (int i = 0; i < 1000; i++)
            {
                var chosen = group.GetRandomBlock();
                Assert.True(chosen == main || chosen == variant);
                if (chosen == main) mainCount++;
            }
            Assert.That(mainCount, Is.InRange(400, 600));
            group.isActive = false; Assert.IsNull(group.GetRandomBlock());
        }
        finally
        {
            UnityEngine.Random.state = state;
            UnityEngine.Object.DestroyImmediate(main); UnityEngine.Object.DestroyImmediate(variant); UnityEngine.Object.DestroyImmediate(disabled);
        }
    }

    [UnityTest]
    public IEnumerator BannerReusesItsViewAcrossScenesAndRecreatesAfterFailure()
    {
        yield return new EnterPlayMode(); UseIsolatedSaves();
        var ads = AdManager.Instance != null ? AdManager.Instance : new GameObject("Test Ads").AddComponent<AdManager>();
        ads.enabled = false;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(AdManager).GetField("initialized", flags).SetValue(ads, true);
        typeof(AdManager).GetField("_enableAds", flags).SetValue(ads, true);
        typeof(AdManager).GetField("_bannerAdUnitId", flags).SetValue(ads, "editor-test-banner");
        var bannerField = typeof(AdManager).GetField("banner", flags);
        ads.LoadBannerAd(); var first = bannerField.GetValue(ads); Assert.NotNull(first);
        ads.LoadBannerAd(); Assert.AreSame(first, bannerField.GetValue(ads));
        typeof(AdManager).GetMethod("OnSceneLoaded", flags).Invoke(ads, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
        yield return null; yield return null;
        Assert.AreSame(first, bannerField.GetValue(ads));
        typeof(AdManager).GetField("bannerFailed", flags).SetValue(ads, true);
        ads.LoadBannerAd(); Assert.AreNotSame(first, bannerField.GetValue(ads));
        ads.EnableAds = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SettingsChangesApplyImmediatelyButSaveOnceAfterIdleAndFlushOnPause()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var settings = SettingsManager.Instance != null ? SettingsManager.Instance : new GameObject("Test Settings").AddComponent<SettingsManager>();
        settings.FlushPendingSettings();
        var observations = new SaveObservation();
        SaveManager.OnSavedForTests += observations.Saved; settings.OnSettingsChanged += observations.Changed;
        try
        {
            for (int i = 1; i <= 100; i++) { settings.BgmVolume = i / 200f; settings.BgmMute = false; }
            Assert.AreEqual(.5f, settings.BgmVolume); Assert.AreEqual(0, observations.SettingsWrites);
            int before = observations.Notifications; settings.BgmVolume = .5f; settings.BgmMute = false;
            Assert.AreEqual(before, observations.Notifications, "Identical values do not notify or schedule another write.");
            yield return new WaitForSecondsRealtime(.35f);
            Assert.AreEqual(1, observations.SettingsWrites); Assert.AreEqual(.5f, SaveManager.LoadData<AudioData>("setting.json").bgmVolume);
            settings.BgmVolume = .25f; settings.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(2, observations.SettingsWrites); Assert.AreEqual(.25f, SaveManager.LoadData<AudioData>("setting.json").bgmVolume);
        }
        finally { SaveManager.OnSavedForTests -= observations.Saved; settings.OnSettingsChanged -= observations.Changed; }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator TenSumSkipsSameCellSelectionButStillUpdatesNewDragsAndCellChanges()
    {
        yield return new EnterPlayMode(); UseIsolatedSaves();
        var load = SceneManager.LoadSceneAsync("10SUM"); while (!load.isDone) yield return null;
        yield return null;
        var grid = TENSUM_GameManager.Instance.gridManager;
        var cells = (GameObject[,])typeof(GridManager).GetField("cellObjects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(grid);
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, cells[0, 0].transform.position) };
        grid.OnPointerDown(pointer);
        var image = cells[0, 0].GetComponent<UnityEngine.UI.Image>();
        Assert.AreEqual(grid.highlightColor, image.color);
        image.color = Color.magenta; grid.OnDrag(pointer);
        Assert.AreEqual(Color.magenta, image.color, "The same selection must not be repainted.");
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, cells[0, 1].transform.position); grid.OnDrag(pointer);
        Assert.AreEqual(grid.highlightColor, image.color);
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>(); menu.Open(); grid.OnDrag(pointer); menu.Close();
        Assert.AreEqual(Color.white, image.color);
        grid.OnPointerDown(pointer);
        Assert.AreEqual(grid.highlightColor, cells[0, 1].GetComponent<UnityEngine.UI.Image>().color);
        yield return new ExitPlayMode();
    }

    [TestCase(0, 1f, 40)]
    [TestCase(1, 1f, 55)]
    [TestCase(4, 1f, 100)]
    [TestCase(99, 1f, 160)]
    [TestCase(0, .5f, 20)]
    [TestCase(8, 0f, 0)]
    public void PlacementHapticScalesWithComboAndRespectsVolume(int combo, float volume, int expected)
    {
        Assert.AreEqual(expected, SettingsManager.GetPlacementHapticAmplitude(combo, volume));
    }

    [UnityTest]
    public IEnumerator GameOverPanelsShowOneHeadingAndMinesweeperCanStillShowWin()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        foreach (var name in new[] { "Blockdoku_2D", "10SUM", "MineSweeper" })
        {
            var load = SceneManager.LoadSceneAsync(name); while (!load.isDone) yield return null;
            yield return null;
            GameObject panel;
            if (name == "Blockdoku_2D")
            {
                var ui = UnityEngine.Object.FindFirstObjectByType<UIManager_2D>(); ui.ShowGameOverPanel(true, 123); panel = ui.gameOverPanel;
                Assert.AreEqual("Score: 123", ui.finalScoreText.text);
            }
            else if (name == "10SUM")
            {
                var ui = UnityEngine.Object.FindFirstObjectByType<UIManager>(); ui.ShowGameOverPanel(true, 123); panel = ui.gameOverPanel;
            }
            else
            {
                var ui = UnityEngine.Object.FindFirstObjectByType<MineSweeper_UIManager>();
                ui.ShowGameOverPanel(true, true, 42, 42); Assert.AreEqual("YOU WIN!", ui.resultText.text);
                Assert.True(ui.resultText.gameObject.activeInHierarchy);
                ui.ShowGameOverPanel(true, false, 42, 42); Assert.AreEqual("GAME OVER", ui.resultText.text); panel = ui.gameOverPanel;
            }
            Assert.AreEqual(1, panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>()
                .Count(t => t.name == "GameOverTmp" || t.name == "ResultTmp" || t.name == "Theme Heading"));
        }
        yield return new ExitPlayMode();
    }

    [TestCase(720, 150)]
    [TestCase(1080, 240)]
    [TestCase(1440, 360)]
    public void BlockdokuHandKeepsItsReservedSpaceWhenBannerLoadsOrReloads(int width, int loadedHeight)
    {
        float initial = BlockdokuHandPanel.GetReservedBannerHeight(width, 0);
        Assert.AreEqual(initial, BlockdokuHandPanel.GetReservedBannerHeight(width, loadedHeight));
        Assert.AreEqual(initial, BlockdokuHandPanel.GetReservedBannerHeight(width, 0));
        Assert.GreaterOrEqual(initial, loadedHeight);
        Assert.AreEqual(initial + 20, BlockdokuHandPanel.GetReservedBannerHeight(width, initial + 20));
    }

    [TestCase(720, 1280, 200, 24)]
    [TestCase(1080, 2400, 270, 80)]
    [TestCase(1440, 3200, 360, 100)]
    public void BlockdokuHandFillsAvailableSpaceBetweenBoardSafeAreaAndBanner(int width, int height, int banner, int inset)
    {
        var viewport = new Rect(0, 0, width, height);
        var safe = new Rect(inset, inset, width - 2 * inset, height - 2 * inset);
        float boardBottom = height * .4f;
        var bounds = BlockdokuHandPanel.GetAvailableRect(viewport, safe, boardBottom, banner, 1);
        Assert.AreEqual(safe.xMin + 12, bounds.xMin);
        Assert.AreEqual(safe.xMax - 12, bounds.xMax);
        Assert.AreEqual(Mathf.Max(banner, inset) + 12, bounds.yMin);
        Assert.AreEqual(boardBottom - 12, bounds.yMax, .01f);
    }

    [UnityTest]
    public IEnumerator BlockdokuHandKeepsLongPiecesInsideTheirSlotsAndRestoresAfterDrag()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        yield return null;
        var hand = UnityEngine.Object.FindFirstObjectByType<BlockdokuHandPanel>();
        Assert.NotNull(hand); Assert.AreEqual(3, hand.slots.Length);
        // The batch editor's default Game view can be landscape. Exercise the supported phone viewport.
        var camera = Camera.main;
        var phoneViewport = new RenderTexture(1080, 1920, 24);
        camera.targetTexture = phoneViewport;
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.isRootCanvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
        Canvas.ForceUpdateCanvases(); hand.Refresh(); Canvas.ForceUpdateCanvases();
        var background = hand.GetComponent<UnityEngine.UI.Image>();
        Assert.Greater(background.color.a, 0); Assert.Less(background.color.a, 1);
        Assert.False(background.raycastTarget);
        var grid = GridManager_2D.Instance;
        var piece = BlockSpawner_2D.Instance.GetSpawnedBlocks()[0].GetComponent<Block_2D>();
        var slot = (RectTransform)piece.SpawnParent;
        var longShape = ScriptableObject.CreateInstance<BlockArray>();
        longShape.shapeRows = new System.Collections.Generic.List<string> { "11111" };
        for (int rotation = 0; rotation < 2; rotation++)
        {
            piece.Initialize(longShape, rotation, Color.red);
            yield return null;
            var shape = piece.GetShape();
            float width = (shape.Max(p => p.x) - shape.Min(p => p.x)) * grid.GetCellPitch().x + grid.GetCellSize().x;
            float height = (shape.Max(p => p.y) - shape.Min(p => p.y)) * grid.GetCellPitch().y + grid.GetCellSize().y;
            Assert.LessOrEqual(width * piece.transform.localScale.x, slot.rect.width - 15);
            Assert.LessOrEqual(height * piece.transform.localScale.y, slot.rect.height - 15);
            Assert.AreEqual(Mathf.Min(hand.GetCommonPreviewScale(grid.GetCellSize(), grid.GetCellPitch()), (slot.rect.width - 16) / width, (slot.rect.height - 16) / height), piece.transform.localScale.x, .001f);
            Assert.AreEqual(Vector2.zero, ((RectTransform)piece.transform).anchoredPosition);
        }
        var touchTarget = slot.GetComponent<BlockdokuHandSlot>();
        Assert.NotNull(touchTarget);
        Assert.True(slot.GetComponent<UnityEngine.UI.Image>().raycastTarget);
        Assert.True(piece.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g => !g.raycastTarget));
        var corner = slot.TransformPoint(new Vector3(slot.rect.xMin + 5, slot.rect.yMax - 5));
        var slotPointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { pointerId = 21, position = RectTransformUtility.WorldToScreenPoint(camera, corner) };
        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(slotPointer, hits);
        Assert.AreSame(slot.gameObject, hits.First().gameObject, "Empty space in the slot must also pick up its piece.");
        touchTarget.OnPointerDown(slotPointer);
        Assert.True(piece.IsDragging);
        var secondFinger = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerId = 22 };
        touchTarget.OnPointerUp(secondFinger);
        Assert.True(piece.IsDragging, "A second finger must not release the first finger's piece.");
        slotPointer.delta = new Vector2(10, 20); touchTarget.OnDrag(slotPointer);
        ((RectTransform)piece.transform).anchoredPosition = new Vector2(-5000, -5000);
        touchTarget.OnPointerUp(slotPointer);
        Assert.False(piece.IsDragging); Assert.AreSame(slot, piece.transform.parent);
        touchTarget.OnPointerDown(slotPointer);
        touchTarget.gameObject.SetActive(false);
        yield return null;
        yield return null;
        Assert.False(piece.IsDragging); Assert.AreSame(slot, piece.transform.parent);
        touchTarget.gameObject.SetActive(true);
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>(); menu.Open();
        touchTarget.OnPointerDown(slotPointer); Assert.False(piece.IsDragging); menu.Close();

        longShape.shapeRows = new System.Collections.Generic.List<string> { "11", "11" };
        piece.Initialize(longShape, 0, Color.red);
        float commonScale = piece.transform.localScale.x;
        longShape.shapeRows = new System.Collections.Generic.List<string> { "111", "111", "111" };
        piece.Initialize(longShape, 0, Color.red);
        Assert.AreEqual(commonScale, piece.transform.localScale.x, .001f, "Ordinary shapes must use the same cell size.");
        var waitingScale = piece.transform.localScale;
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        piece.OnPointerDown(pointer);
        Assert.AreNotSame(slot, piece.transform.parent);
        ((RectTransform)piece.transform).anchoredPosition = new Vector2(-5000, -5000);
        piece.OnPointerUp(pointer);
        Assert.AreSame(slot, piece.transform.parent);
        Assert.AreEqual(waitingScale, piece.transform.localScale);
        Assert.AreEqual(Vector2.zero, ((RectTransform)piece.transform).anchoredPosition);
        piece.OnPointerDown(pointer);
        hand.previewCellSize = 32; hand.Refresh();
        Assert.AreSame(slot, piece.transform.parent);
        Assert.LessOrEqual(piece.transform.localScale.x * grid.GetCellSize().x, 32.01f);
        Assert.AreEqual(0, GameManager_2D.Instance.GetScore());
        UnityEngine.Object.Destroy(longShape);
        camera.targetTexture = null; UnityEngine.Object.Destroy(phoneViewport);
        yield return new ExitPlayMode();
    }

    private sealed class FakeAuthenticationClient : IPlayGamesAuthenticationClient
    {
        public bool IsAvailable => true;
        public int AutomaticCalls, ManualCalls;
        private Action<PlayGamesAuthenticationResult> callback;
        public void Authenticate(bool manual, Action<PlayGamesAuthenticationResult> completed)
        {
            if (manual) ManualCalls++; else AutomaticCalls++;
            callback = completed;
        }
        public void Complete(bool success)
        {
            var completed = callback; callback = null;
            completed(new PlayGamesAuthenticationResult { success = success, status = success ? "Success" : "SignInRequired", playerId = success ? "sdk-account" : null, playerName = success ? "SDK Nickname" : null });
        }
    }

    private FakeAuthenticationClient UseAuthenticationClient(PlayGamesService service)
    {
        var client = new FakeAuthenticationClient();
        typeof(PlayGamesService).GetField("authenticationClient", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(service, client);
        return client;
    }

    [UnityTest]
    public IEnumerator ExistingSdkLoginOpensRankingWithoutManualAuthentication()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        if (RankingsPanel.IsOpen) { RankingsPanel.TryClose(); yield return null; }
        var service = PlayGamesService.Instance;
        service.ContinueAsGuest();
        var client = UseAuthenticationClient(service);
        service.ShowLeaderboard(LeaderboardKind.TenSum);
        Assert.AreEqual(1, client.AutomaticCalls);
        Assert.AreEqual(0, client.ManualCalls);
        Assert.True(service.IsSigningIn);
        Assert.False(RankingsPanel.IsOpen, "Do not show a login guide before checking SDK authentication.");
        client.Complete(true);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.True(service.IsAuthenticated);
        Assert.AreEqual("SDK Nickname", service.PlayerName);
        Assert.True(SaveManager.LoadData<PlayGamesLoginChoice>("playGamesChoice.json").rankingsEnabled);
        var panel = UnityEngine.Object.FindFirstObjectByType<RankingsPanel>();
        Assert.False(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Sign in with Play Games"));
        Assert.AreEqual(0, client.ManualCalls);
        RankingsPanel.TryClose(); yield return null;
        service.ShowAllLeaderboards();
        Assert.AreEqual(1, client.AutomaticCalls, "Reuse the synchronized session.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator MissingSdkLoginOffersManualSignInOnlyAfterUserChoosesIt()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        if (RankingsPanel.IsOpen) { RankingsPanel.TryClose(); yield return null; }
        var service = PlayGamesService.Instance;
        service.ContinueAsGuest();
        var client = UseAuthenticationClient(service);
        service.ShowAllLeaderboards(); client.Complete(false);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.False(service.IsAuthenticated);
        Assert.AreEqual(0, client.ManualCalls);
        var panel = UnityEngine.Object.FindFirstObjectByType<RankingsPanel>();
        panel.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Sign in with Play Games").onClick.Invoke();
        Assert.AreEqual(1, client.ManualCalls);
        client.Complete(true);
        Assert.True(service.IsAuthenticated);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator GuestChoiceRejectsLateSdkCallbackAndDisablesStartupRestore()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        if (RankingsPanel.IsOpen) { RankingsPanel.TryClose(); yield return null; }
        var service = PlayGamesService.Instance;
        service.ContinueAsGuest();
        var client = UseAuthenticationClient(service);
        service.ShowAllLeaderboards();
        service.ContinueAsGuest();
        client.Complete(true);
        Assert.False(service.IsAuthenticated);
        Assert.AreEqual(string.Empty, service.PlayerName);
        Assert.False(SaveManager.LoadData<PlayGamesLoginChoice>("playGamesChoice.json").rankingsEnabled);
        typeof(PlayGamesService).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(service, null);
        Assert.AreEqual(1, client.AutomaticCalls);
        Assert.False(RankingsPanel.IsOpen);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PreviouslyEnabledRankingsRestoreSdkSessionWithoutManualSignIn()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        if (RankingsPanel.IsOpen) { RankingsPanel.TryClose(); yield return null; }
        var service = PlayGamesService.Instance;
        service.ContinueAsGuest();
        var client = UseAuthenticationClient(service);
        SaveManager.SaveData("playGamesChoice.json", new PlayGamesLoginChoice { hasChosen = true, rankingsEnabled = true });
        typeof(PlayGamesService).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(service, null);
        Assert.AreEqual(1, client.AutomaticCalls);
        Assert.AreEqual(0, client.ManualCalls);
        client.Complete(true);
        Assert.True(service.IsAuthenticated);
        Assert.False(RankingsPanel.IsOpen, "Session restoration must not automatically open a leaderboard.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SignedInRankingRequestsSkipLoginChoiceAndDoNotOfferSignIn()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        if (RankingsPanel.IsOpen) { RankingsPanel.TryClose(); yield return null; }
        var service = PrepareScoreUploadTest(new FakeScoreUploader(), new ScoreOutbox());
        // The Editor cannot present Google's native screen, but must show a platform notice rather than a login choice.
        RankingsPanel.OpenLoginChoice();
        yield return new WaitForSecondsRealtime(.1f);
        var panel = UnityEngine.Object.FindFirstObjectByType<RankingsPanel>();
        Assert.NotNull(panel);
        Assert.False(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Play as guest" || b.name == "Sign in with Play Games"));
        Assert.True(panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text == "ALL GAMES"));
        RankingsPanel.TryClose(); yield return null;
        Assert.True(service.IsAuthenticated, "Closing ranking information must not change an authenticated user into a guest.");
        RankingsPanel.OpenMinesweeper();
        yield return new WaitForSecondsRealtime(.1f);
        panel = UnityEngine.Object.FindFirstObjectByType<RankingsPanel>();
        Assert.True(panel.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text == "ALL GAMES"));
        Assert.False(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Sign in with Play Games"));
        RankingsPanel.TryClose(); yield return null;
        service.ContinueAsGuest();
        RankingsPanel.OpenForLeaderboard(LeaderboardKind.TenSum);
        yield return new WaitForSecondsRealtime(.1f);
        panel = UnityEngine.Object.FindFirstObjectByType<RankingsPanel>();
        Assert.True(panel.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Sign in with Play Games" && b.interactable));
        RankingsPanel.TryClose(); yield return null;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RankingPopupScrollsLongStatusAndKeepsCloseButtonAccessible()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var service = PlayGamesService.Instance;
        service.ContinueAsGuest();
        RankingsPanel.OpenMinesweeper();
        yield return new WaitForSecondsRealtime(.15f);
        var popup = UnityEngine.Object.FindFirstObjectByType<RankingsPanel>();
        Assert.NotNull(Resources.Load<RankingPopupStyle>("RankingPopupStyle"));
        typeof(PlayGamesService).GetMethod("SetStatus", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(service, new object[] { string.Join("\n", Enumerable.Repeat("Unable to connect. Check your connection and try again.", 12)) });
        yield return new WaitForSecondsRealtime(.15f);
        var scroll = popup.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
        var close = popup.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Close");
        Assert.False(close.transform.IsChildOf(scroll.content));
        var before = close.transform.position;
        scroll.verticalNormalizedPosition = 0;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(before, close.transform.position);
        var corners = new Vector3[4]; ((RectTransform)close.transform).GetWorldCorners(corners);
        Assert.Greater(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y, 0);
        Assert.Less(RectTransformUtility.WorldToScreenPoint(null, corners[2]).y, Screen.height);
        close.onClick.Invoke();
        yield return null;
        Assert.False(RankingsPanel.IsOpen);
        yield return new ExitPlayMode();
    }
    [SerializeField] private bool restoreArchivedStackTower;

    private AsyncOperation LoadStackTowerForTests()
    {
        return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/StackTower.unity", new LoadSceneParameters(LoadSceneMode.Single));
    }

    private void EnableStackTowerRestartForTests()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var scene in scenes)
            if (scene.path == "Assets/Scenes/StackTower.unity" && !scene.enabled)
            {
                restoreArchivedStackTower = true;
                scene.enabled = true;
            }
        EditorBuildSettings.scenes = scenes;
    }

    [UnityTest]
    public IEnumerator BlockdokuBorderEffectsStayBoundedAndStopOnResetOrDisable()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        var grid = GridManager_2D.Instance;
        var effect = grid.BorderEffect;
        Assert.NotNull(effect);
        int objectCount = grid.symmetryEffectContainer.childCount;
        var canvas = grid.symmetryEffectContainer.GetComponent<Canvas>();
        Assert.NotNull(canvas);
        Assert.AreNotSame(grid.gridParent.GetComponentInParent<Canvas>(), canvas);
        foreach (string type in new[] { "H", "V", "D1", "D2" })
        {
            grid.PlaySymmetryAnimation(type);
            Assert.AreEqual(1, DG.Tweening.DOTween.TweensByTarget(effect).Count);
            DG.Tweening.DOTween.Goto(effect, .4f, false);
            Assert.Greater(effect.ActiveTrailCount, 0);
            Assert.LessOrEqual(effect.LastEmissionCount, BlockdokuBorderEffect.EmissionBudget);
            Assert.LessOrEqual(effect.ActiveTrailCount, BlockdokuBorderEffect.TrailCapacity);
        }
        for (int i = 0; i < 30; i++)
        {
            grid.PlayFullClearAnimation();
            DG.Tweening.DOTween.Goto(effect, .5f, false);
            Assert.AreEqual(1, DG.Tweening.DOTween.TweensByTarget(effect).Count);
            Assert.LessOrEqual(effect.LastEmissionCount, BlockdokuBorderEffect.EmissionBudget);
        }
        Assert.AreEqual(objectCount, grid.symmetryEffectContainer.childCount);
        Assert.False(effect.raycastTarget);
        grid.PlayFullClearAnimation();
        for (int frame = 1; frame <= 120; frame++)
        {
            DG.Tweening.DOTween.Goto(effect, frame / 60f, false);
            Assert.LessOrEqual(effect.ActiveTrailCount, BlockdokuBorderEffect.TrailCapacity);
            Assert.LessOrEqual(effect.LastEmissionCount, BlockdokuBorderEffect.EmissionBudget);
        }
        Canvas.ForceUpdateCanvases();
        var mesh = effect.canvasRenderer.GetMesh();
        Assert.NotNull(mesh);
        Assert.Greater(mesh.vertexCount, 8, "Heads and trails must actually be rendered.");
        Assert.LessOrEqual(mesh.vertexCount, (BlockdokuBorderEffect.TrailCapacity + 2) * 4);
        Debug.Log($"[Border effect verification] objects={grid.symmetryEffectContainer.childCount}, tweens={DG.Tweening.DOTween.TweensByTarget(effect).Count}, trailStamps={effect.ActiveTrailCount}, vertices={mesh.vertexCount}");
        DG.Tweening.DOTween.Goto(effect, 5f, false);
        Assert.False(effect.IsPlaying);
        Assert.AreEqual(0, effect.ActiveTrailCount);
        grid.PlaySymmetryAnimation("H");
        grid.enabled = false;
        Assert.False(effect.IsPlaying);
        Assert.AreEqual(0, effect.ActiveTrailCount);
        grid.enabled = true;
        grid.PlayFullClearAnimation();
        GameManager_2D.Instance.StartGame();
        Assert.False(effect.IsPlaying);
        Assert.AreEqual(0, effect.ActiveTrailCount);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HeaderBackgroundFillsScreenTopWhileContentsRespectSafeArea()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadStackTowerForTests();
        while (!load.isDone) yield return null;
        yield return null;
        var game = StackTowerGame.Instance;
        var header = game.scoreText.GetComponentInParent<SafeAreaTopPanel>();
        var panel = (RectTransform)header.transform;
        var canvas = header.GetComponentInParent<Canvas>();
        var viewport = canvas.pixelRect;
        var originalScorePosition = game.scoreText.rectTransform.anchoredPosition;
        foreach (float inset in new[] { 0f, 80f, 180f })
        {
            var safe = new Rect(viewport.xMin + 20, viewport.yMin, viewport.width - 40, viewport.height - inset);
            SafeAreaTopPanel.GetTopAnchors(safe, viewport, out var min, out var max);
            panel.anchorMin = min; panel.anchorMax = max;
            panel.anchoredPosition = new Vector2(0, -header.topPadding);
            Canvas.ForceUpdateCanvases();
            header.RefreshBackground();
            Canvas.ForceUpdateCanvases();
            Assert.NotNull(header.BackgroundImage);
            Assert.False(header.GetComponent<UnityEngine.UI.Image>().enabled);
            Assert.False(header.BackgroundImage.raycastTarget);
            Assert.AreEqual(originalScorePosition, game.scoreText.rectTransform.anchoredPosition);
            var corners = new Vector3[4];
            header.BackgroundImage.rectTransform.GetWorldCorners(corners);
            Assert.AreEqual(viewport.xMin, RectTransformUtility.WorldToScreenPoint(null, corners[1]).x, .1f);
            Assert.AreEqual(viewport.yMax, RectTransformUtility.WorldToScreenPoint(null, corners[1]).y, .1f);
            Assert.AreEqual(viewport.xMax, RectTransformUtility.WorldToScreenPoint(null, corners[2]).x, .1f);
            game.scoreText.rectTransform.GetWorldCorners(corners);
            Assert.Less(RectTransformUtility.WorldToScreenPoint(null, corners[1]).y, safe.yMax);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator StackTowerBackgroundFollowsAltitudeCoversViewportAndResets()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadStackTowerForTests();
        while (!load.isDone) yield return null;
        yield return null;
        var game = StackTowerGame.Instance;
        var background = game.background;
        Assert.NotNull(background);
        Assert.AreEqual(5, background.zoneSprites.Length);
        var renderer = background.primaryLayer;
        Assert.NotNull(renderer.sprite);
        Assert.Less(renderer.sortingOrder, game.CurrentBlock.GetComponentInChildren<SpriteRenderer>().sortingOrder);
        float baseline = game.gameCamera.transform.position.y;
        background.ResetAltitude(baseline);
        foreach (float aspect in new[] { .45f, .5625f, .75f })
        {
            game.gameCamera.aspect = aspect;
            foreach (float ascent in new[] { -10f, 0f, 24f, 30f, 54f, 60f, 90f, 120f, 240f })
            {
                game.gameCamera.transform.position = new Vector3(0, baseline + ascent, -10);
                background.Refresh();
                Assert.AreEqual(Mathf.Clamp01(ascent / background.heightToSpace), background.AltitudeProgress, .001f);
                var bounds = renderer.bounds;
                float halfHeight = game.gameCamera.orthographicSize;
                Assert.LessOrEqual(bounds.min.x, -halfHeight * aspect + .001f);
                Assert.GreaterOrEqual(bounds.max.x, halfHeight * aspect - .001f);
                Assert.LessOrEqual(bounds.min.y, baseline + ascent - halfHeight + .001f);
                Assert.GreaterOrEqual(bounds.max.y, baseline + ascent + halfHeight - .001f);
                Assert.AreEqual(renderer.transform.localScale.x, renderer.transform.localScale.y, .001f);
                if (background.transitionLayer.enabled)
                {
                    Assert.Greater(background.TransitionAmount, 0);
                    Assert.LessOrEqual(background.TransitionAmount, 1);
                    Assert.AreEqual(background.TransitionAmount, background.transitionLayer.color.a, .001f);
                    Assert.AreEqual(background.transitionLayer.transform.localScale.x, background.transitionLayer.transform.localScale.y, .001f);
                }
            }
        }
        for (int i = 0; i < 5; i++)
        {
            background.SetPreview(i / 4f);
            Assert.AreEqual(i, background.CurrentZoneIndex);
            Assert.AreSame(background.zoneSprites[i], background.primaryLayer.sprite);
            Assert.AreEqual(0, background.TransitionAmount);
            Assert.AreEqual(0, game.Score);
        }
        background.SetPreview(.2f);
        Assert.Greater(background.TransitionAmount, 0);
        Assert.Less(background.TransitionAmount, 1);
        background.ClearPreview();
        Assert.False(background.IsPreviewing);
        game.StartGame();
        Assert.AreEqual(0, background.AltitudeProgress, .001f);
        Assert.AreEqual(0, game.Score);
        yield return new ExitPlayMode();
    }

    [TestCase(1080, 270, 0, 1)]
    [TestCase(1440, 360, 96, 1.333333f)]
    [TestCase(720, 180, 48, .666667f)]
    public void StackTowerControlsClearBannerAndBottomSafeArea(float width, float banner, float inset, float scale)
    {
        float center = StackTowerGame.GetControlsCenterY(banner, inset, scale, 160, 40);
        Assert.AreEqual(banner + inset + 40 * scale, (center - 80) * scale, .01f);
        float fallback = StackTowerGame.GetControlsCenterY(width * 90 / 320, inset, scale, 160, 40);
        Assert.Greater((fallback - 80) * scale, inset);
    }

    [UnityTest]
    public IEnumerator StackTowerRotationHoldsTurnSmoothlyAndStopOnReleasePauseAndDrop()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadStackTowerForTests();
        while (!load.isDone) yield return null;
        yield return null;
        var game = StackTowerGame.Instance;
        var piece = game.CurrentBlock;
        var controlsCanvas = game.dropButton.GetComponentInParent<UnityEngine.Canvas>();
        var dropRect = (RectTransform)game.dropButton.transform;
        Assert.GreaterOrEqual((dropRect.anchoredPosition.y - dropRect.rect.height * .5f) * controlsCanvas.scaleFactor,
            Screen.width * 90f / 320f + Screen.safeArea.yMin);
        Assert.AreEqual(dropRect.anchoredPosition.y, ((RectTransform)game.rotateClockwiseButton.transform).anchoredPosition.y);
        Assert.Greater(game.statusText.rectTransform.anchoredPosition.y, dropRect.anchoredPosition.y + dropRect.rect.height * .5f);
        Assert.Less(game.statusBackdrop.GetSiblingIndex(), game.statusText.transform.GetSiblingIndex());
        Assert.Greater(game.gameCamera.WorldToScreenPoint(new Vector3(0, game.baseTop)).y,
            (game.statusText.rectTransform.anchoredPosition.y + game.statusText.rectTransform.rect.height * .5f) * controlsCanvas.scaleFactor);
        game.rotationSpeed = 120;
        var cw = game.rotateClockwiseButton.GetComponent<StackTowerRotationHold>();
        var ccw = game.rotateCounterClockwiseButton.GetComponent<StackTowerRotationHold>();
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerId = 1 };
        float before = piece.Body.rotation;
        var cells = piece.GetComponentsInChildren<BoxCollider2D>();
        cw.OnPointerDown(pointer);
        yield return new WaitForSecondsRealtime(.2f);
        float turn = Mathf.DeltaAngle(before, piece.Body.rotation);
        Assert.Less(turn, -3);
        Assert.Greater(turn, -80);
        CollectionAssert.AreEqual(cells, piece.GetComponentsInChildren<BoxCollider2D>());
        cw.OnPointerUp(pointer);
        float released = piece.Body.rotation;
        yield return new WaitForSecondsRealtime(.1f);
        Assert.AreEqual(released, piece.Body.rotation, .01f);
        ccw.OnPointerDown(pointer);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.Greater(Mathf.DeltaAngle(released, piece.Body.rotation), 3);
        ccw.OnPointerExit(pointer);
        Assert.False(ccw.IsHeld);
        // Release near each cardinal orientation, including wraparound at zero.
        foreach (float angle in new[] { 6f, 84f, 96f, 174f, 186f, 264f, 276f, 354f })
        {
            cw.OnPointerDown(pointer);
            piece.Body.rotation = angle;
            cw.OnPointerUp(pointer);
            Assert.AreEqual(0, Mathf.DeltaAngle(Mathf.Round(angle / 90) * 90, piece.Body.rotation), .01f);
        }
        cw.OnPointerDown(pointer);
        piece.Body.rotation = 45;
        cw.OnPointerUp(pointer);
        Assert.AreEqual(45, piece.Body.rotation, .01f);
        game.rotationSnapTolerance = 0;
        cw.OnPointerDown(pointer);
        piece.Body.rotation = 86;
        cw.OnPointerUp(pointer);
        Assert.AreEqual(86, piece.Body.rotation, .01f);
        game.rotationSnapTolerance = 10;
        cw.OnPointerDown(pointer);
        cw.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerId = 2 });
        Assert.True(cw.IsHeld);
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>();
        menu.Open();
        yield return new WaitForSecondsRealtime(.1f);
        Assert.False(cw.IsHeld);
        Assert.False(game.CanRotate);
        Assert.False(game.rotateClockwiseButton.interactable);
        menu.Close();
        yield return new WaitForSecondsRealtime(.15f);
        Assert.True(game.CanRotate);
        Assert.False(cw.IsHeld);
        cw.OnPointerDown(pointer);
        float droppedAngle = piece.Body.rotation;
        Assert.True(game.TryDrop());
        Assert.AreEqual(droppedAngle, piece.Body.rotation, .01f);
        Assert.False(cw.IsHeld);
        Assert.False(game.CanRotate);
        Assert.False(game.rotateCounterClockwiseButton.interactable);
        cw.OnPointerDown(pointer);
        Assert.False(cw.IsHeld);
        piece.Body.position = new Vector2(12, -5);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(StackTowerGame.RunState.GameOver, game.State);
        Assert.AreEqual(0, game.Score);
        yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator StackTowerAllowsSideLandingsAndSpeedsUpByHeightUntilAPlacedPieceFallsOut()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadStackTowerForTests();
        while (!load.isDone) yield return null;
        yield return null;
        var game = StackTowerGame.Instance;
        game.shapePool = new[] { AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/Traditional/O.asset") };
        game.StartGame();
        float initialSpeed = game.CurrentMoveSpeed;
        float firstHeight = 0, firstSpeed = 0;
        for (int i = 0; i < 3; i++)
        {
            game.CurrentBlock.Body.position = new Vector2(i == 1 ? 1.8f : 0, game.CurrentBlock.Body.position.y);
            Assert.True(game.TryDrop());
            float deadline = Time.realtimeSinceStartup + 5;
            while (game.Score <= i && game.State != StackTowerGame.RunState.GameOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(i + 1, game.Score);
            Assert.AreEqual(StackTowerGame.RunState.Moving, game.State);
            if (i == 0) { firstHeight = game.TowerHeight; firstSpeed = game.CurrentMoveSpeed; Assert.Greater(firstSpeed, initialSpeed); }
            if (i == 1)
            {
                Assert.AreEqual(firstHeight, game.TowerHeight, .04f, "A piece on the foundation beside the tower must be allowed");
                Assert.AreEqual(firstSpeed, game.CurrentMoveSpeed, .02f, "More score without more height must not increase speed");
                game.PlacedBlocks[1].Body.rotation = 90;
            }
        }
        Assert.Greater(game.TowerHeight, firstHeight + .5f);
        Assert.Greater(game.CurrentMoveSpeed, firstSpeed + .1f);
        Assert.LessOrEqual(game.CurrentMoveSpeed, game.maximumMoveSpeed);
        game.gameCamera.transform.position = new Vector3(0, 50, -10);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(StackTowerGame.RunState.Moving, game.State, "Off-camera or rotated blocks must not cause game over");
        game.PlacedBlocks[1].Body.position = new Vector2(12, -4);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(StackTowerGame.RunState.GameOver, game.State, "An already placed piece falling below the foundation must end the run");
        Assert.True(game.gameOverPanel.activeSelf);
        yield return new ExitPlayMode();
    }

    [Test]
    public void StackTowerExcludesDiagonalOnlyPiecesButKeepsConnectedSZShapes()
    {
        Assert.False(StackTowerBlock.IsConnectedShape(AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/dot_s.asset")));
        Assert.False(StackTowerBlock.IsConnectedShape(AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/dot_s_s.asset")));
        foreach (var name in new[] { "S", "Z", "L", "T" })
            Assert.True(StackTowerBlock.IsConnectedShape(AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/Traditional/" + name + ".asset")));
        EditorSceneManager.OpenScene("Assets/Scenes/StackTower.unity");
        var game = UnityEngine.Object.FindFirstObjectByType<StackTowerGame>();
        Assert.Greater(game.shapePool.Length, 5);
        Assert.True(game.shapePool.All(StackTowerBlock.IsConnectedShape));
        Assert.False(game.shapePool.Any(s => s.name == "dot_s" || s.name == "dot_s_s"));
    }

    [Test]
    public void StackTowerCollidersMatchEveryOccupiedBlockdokuCellInAllRotations()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/StackTower.unity");
        var game = UnityEngine.Object.FindFirstObjectByType<StackTowerGame>();
        var go = new GameObject("Piece geometry test", typeof(Rigidbody2D), typeof(StackTowerBlock));
        try
        {
            var block = go.GetComponent<StackTowerBlock>(); block.cellSprite = game.blockPrefab.cellSprite; block.grip = game.blockPrefab.grip;
            foreach (var data in game.shapePool)
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    block.Configure(data, rotation, game.cellSize, Color.cyan);
                    var shape = data.GetShape(rotation);
                    var colliders = go.GetComponentsInChildren<BoxCollider2D>();
                    Assert.IsNull(go.GetComponent<Collider2D>(), "A bounding box would incorrectly fill concave gaps");
                    Assert.AreEqual(shape.Count, colliders.Length);
                    Assert.AreEqual(shape.Count, block.CellCount);
                    Vector2 min = shape[0], max = shape[0];
                    foreach (var cell in shape) { min = Vector2.Min(min, cell); max = Vector2.Max(max, cell); }
                    var center = (min + max) * .5f;
                    for (int x = (int)min.x; x <= max.x; x++)
                        for (int y = (int)min.y; y <= max.y; y++)
                        {
                            var expected = new Vector2Int(x, y);
                            bool hasCell = colliders.Any(c => Vector2.Distance(c.transform.localPosition, (expected - center) * game.cellSize) < .001f);
                            Assert.AreEqual(shape.Contains(expected), hasCell, data.name + "/" + rotation + "/" + expected);
                        }
                    foreach (var collider in colliders)
                    {
                        Assert.AreEqual(Vector2.one * game.cellSize, collider.size);
                        Assert.AreSame(block.Body, collider.attachedRigidbody);
                    }
                }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void StackTowerIsArchivedButKeepsItsSceneAndSharedSettings()
    {
        Assert.True(Array.Exists(EditorBuildSettings.scenes, s => !s.enabled && s.path == "Assets/Scenes/StackTower.unity"));
        var title = EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        UnityEngine.UI.Button entry = null;
        foreach (var root in title.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                if (button.name == "StackTowerBtn") entry = button;
        Assert.NotNull(entry);
        Assert.False(entry.gameObject.activeSelf);
        Assert.AreEqual("StackTowerBtnOnClicked", entry.onClick.GetPersistentMethodName(0));
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/StackTower.unity");
        var game = UnityEngine.Object.FindFirstObjectByType<StackTowerGame>();
        Assert.NotNull(game.blockPrefab);
        Assert.NotNull(game.dropButton);
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>();
        Assert.AreEqual(GameMenuPrefabTools.PrefabPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(menu.popup));
        Assert.NotNull(game.scoreText.GetComponentInParent<SafeAreaTopPanel>());
    }

    [UnityTest]
    public IEnumerator StackTowerScoresStableFloorsPausesAndEndsOnAMissedDrop()
    {
        EnableStackTowerRestartForTests();
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = LoadStackTowerForTests();
        while (!load.isDone) yield return null;
        yield return null;
        var game = StackTowerGame.Instance;
        game.shapePool = new[] {
            AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/Traditional/O.asset"),
            AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/dot_s.asset"),
            AssetDatabase.LoadAssetAtPath<BlockArray>("Assets/ScriptableObjects/BlockArrays/dot_s_s.asset")
        };
        game.StartGame();
        Assert.NotNull(game.CurrentBlock);
        for (int floor = 1; floor <= 3; floor++)
        {
            Assert.AreEqual("O", game.CurrentBlock.ShapeData.name, "Runtime filtering must also reject diagonal assets added to the Inspector pool");
            game.CurrentBlock.Body.position = new Vector2(0, game.CurrentBlock.Body.position.y);
            Assert.True(game.TryDrop());
            Assert.False(game.TryDrop(), "Repeated taps must not drop multiple blocks");
            float deadline = Time.realtimeSinceStartup + 5;
            while (game.Score < floor && game.State != StackTowerGame.RunState.GameOver && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(floor, game.Score, "A centered block must land and score exactly one floor");
            Assert.AreEqual(floor, game.BestScore);
            Assert.AreEqual(floor, game.PlacedBlocks.Count);
        }
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>();
        menu.Open();
        yield return null;
        var pausedPosition = game.CurrentBlock.Body.position;
        yield return new WaitForSecondsRealtime(.2f);
        Assert.AreEqual(pausedPosition, game.CurrentBlock.Body.position);
        Assert.False(game.TryDrop());
        Assert.False(game.PlacedBlocks[0].Body.simulated);
        menu.Close();
        yield return null;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(StackTowerGame.RunState.Moving, game.State);
        Assert.True(game.PlacedBlocks[0].Body.simulated, "Physics must resume after the popup closes");
        game.CurrentBlock.Body.position = new Vector2(12, game.CurrentBlock.Body.position.y);
        Assert.True(game.TryDrop());
        float endDeadline = Time.realtimeSinceStartup + 5;
        while (game.State != StackTowerGame.RunState.GameOver && Time.realtimeSinceStartup < endDeadline) yield return null;
        Assert.AreEqual(StackTowerGame.RunState.GameOver, game.State);
        Assert.AreEqual(3, game.Score);
        Assert.True(game.gameOverPanel.activeSelf);
        Assert.AreEqual(3, SaveManager.LoadData<StackTowerGame.Record>("StackTower.json").bestFloors);
        game.RestartGame();
        float restartDeadline = Time.realtimeSinceStartup + 5;
        while ((ReferenceEquals(StackTowerGame.Instance, game) || StackTowerGame.Instance == null || StackTowerGame.Instance.CurrentBlock == null) && Time.realtimeSinceStartup < restartDeadline) yield return null;
        Assert.AreEqual(0, StackTowerGame.Instance.Score);
        Assert.AreEqual(3, StackTowerGame.Instance.BestScore);
        Assert.AreEqual(StackTowerGame.RunState.Moving, StackTowerGame.Instance.State);
        yield return new ExitPlayMode();
    }

    [Test]
    public void SharedGameMenuAssetKeepsActionsAndSettingsInsideThePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPrefabTools.PrefabPath);
        Assert.NotNull(prefab);
        Assert.IsNull(prefab.GetComponent<GameMenu>(), "The scene owner must not be saved in the prefab");
        var actions = prefab.GetComponent<GameMenuPopup>();
        var settings = prefab.GetComponent<SettingsUI>();
        Assert.NotNull(actions); Assert.NotNull(settings);
        foreach (var slider in new[] { settings.bgmVolumeSlider, settings.sfxVolumeSlider, settings.vibrationVolumeSlider })
            Assert.True(slider.transform.IsChildOf(prefab.transform));
        foreach (var button in prefab.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                Assert.AreSame(actions, button.onClick.GetPersistentTarget(i), button.name + " has a non-local or missing event target");
    }

    [TestCase("Title", LeaderboardTarget.All)]
    [TestCase("Blockdoku_2D", LeaderboardTarget.Blockdoku)]
    [TestCase("10SUM", LeaderboardTarget.TenSum)]
    [TestCase("MineSweeper", LeaderboardTarget.MineSweeper)]
    public void SceneRankingButtonsHaveCorrectScopeAndHeaderPlacement(string sceneName, LeaderboardTarget target)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
        LeaderboardButton ranking = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var candidate = root.GetComponentInChildren<LeaderboardButton>(true);
            if (candidate != null) ranking = candidate;
        }
        Assert.NotNull(ranking);
        Assert.AreEqual(target, ranking.target);
        var button = ranking.GetComponent<UnityEngine.UI.Button>();
        Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
        Assert.AreSame(ranking, button.onClick.GetPersistentTarget(0));
        Assert.AreEqual("Open", button.onClick.GetPersistentMethodName(0));
        var rect = (RectTransform)ranking.transform;
        Assert.AreEqual(Vector2.one, rect.anchorMax);
        Assert.AreEqual(Vector2.one, rect.anchorMin);
        Assert.GreaterOrEqual(rect.rect.width, 128);
        Assert.AreEqual(sceneName == "Title" ? "Canvas" : "TopUIPanel", ranking.transform.parent.name);
        if (sceneName != "Title")
        {
            var header = ranking.transform.parent;
            var safeArea = header.GetComponent<SafeAreaTopPanel>();
            Assert.NotNull(safeArea);
            Assert.AreEqual(16, safeArea.topPadding);
            Assert.AreEqual(-16, ((RectTransform)header).anchoredPosition.y);
            Assert.IsNull(header.Find("BackToTitleBtn"));
            Assert.IsNull(header.Find("ResetBtn"));
            var menu = header.GetComponentInParent<GameMenu>();
            Assert.NotNull(menu);
            var sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameMenuPrefabTools.PrefabPath);
            Assert.NotNull(sharedPrefab);
            Assert.AreSame(sharedPrefab, PrefabUtility.GetCorrespondingObjectFromSource(menu.popup));
            var popupActions = menu.popup.GetComponent<GameMenuPopup>();
            Assert.NotNull(popupActions);
            Assert.False(menu.popup.activeSelf);
            var card = menu.popup.transform.Find("SettingsCard");
            Assert.IsNull(card.Find("Title"));
            Assert.IsNull(card.Find("Restart"));
            var settings = menu.popup.GetComponent<SettingsUI>();
            Assert.NotNull(settings.bgmVolumeSlider);
            Assert.NotNull(settings.sfxVolumeSlider);
            Assert.NotNull(settings.vibrationVolumeSlider);
            var popupButtons = menu.popup.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            foreach (var actionName in new[] { "BackToTitleBtn", "ResetBtn" })
            {
                var action = Array.Find(popupButtons, b => b.name == actionName);
                Assert.NotNull(action);
                Assert.AreSame(popupActions, action.onClick.GetPersistentTarget(0));
                Assert.AreEqual(actionName == "ResetBtn" ? "Restart" : "ReturnToTitle", action.onClick.GetPersistentMethodName(0));
                Assert.Less(((RectTransform)action.transform).anchoredPosition.y, settings.vibrationVolumeSlider.GetComponent<RectTransform>().anchoredPosition.y);
            }
            var settingsRect = (RectTransform)header.Find("SettingBtn");
            Assert.Greater(settingsRect.anchoredPosition.x, rect.anchoredPosition.x);
            TextMeshProUGUIForHeaderCheck(sceneName, header);
            var buttons = Array.FindAll(header.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.transform.parent == header);
            for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                {
                    var a = (RectTransform)buttons[i].transform;
                    var b = (RectTransform)buttons[j].transform;
                    float ax = a.anchorMin.x * 1080 + a.anchoredPosition.x;
                    float bx = b.anchorMin.x * 1080 + b.anchoredPosition.x;
                    Assert.GreaterOrEqual(Mathf.Abs(ax - bx), (a.rect.width + b.rect.width) / 2, "Header buttons overlap");
                }
        }
    }

    [TestCase(1080, 1920, 0)]
    [TestCase(1080, 2340, 100)]
    [TestCase(1440, 3200, 160)]
    [TestCase(720, 1600, 80)]
    public void HeaderAnchorsFollowCameraSafeAreaAtDifferentResolutions(int width, int height, int cameraInset)
    {
        SafeAreaTopPanel.GetTopAnchors(new Rect(0, 0, width, height - cameraInset), new Rect(0, 0, width, height), out var min, out var max);
        Assert.AreEqual(0, min.x);
        Assert.AreEqual(1, max.x);
        Assert.AreEqual(height - cameraInset, max.y * height, .01f);
        Assert.AreEqual(min.y, max.y);
        // Padding and the existing first-row inset place the score strictly below the safe top.
        float scale = width / 1080f;
        float scoreTop = max.y * height - (16 + 85 - 60) * scale;
        Assert.Less(scoreTop, height - cameraInset);
    }

    [Test]
    public void HeaderSafeAreaUsesCanvasViewportAndHandlesUninitializedDimensions()
    {
        SafeAreaTopPanel.GetTopAnchors(new Rect(110, 210, 780, 920), new Rect(100, 200, 800, 1000), out var min, out var max);
        Assert.AreEqual(.0125f, min.x, .0001f);
        Assert.AreEqual(.9875f, max.x, .0001f);
        Assert.AreEqual(.93f, max.y, .0001f);
        SafeAreaTopPanel.GetTopAnchors(Rect.zero, Rect.zero, out min, out max);
        Assert.AreEqual(new Vector2(0, 1), min);
        Assert.AreEqual(Vector2.one, max);
    }

    private static void TextMeshProUGUIForHeaderCheck(string sceneName, Transform header)
    {
        TMPro.TextMeshProUGUI score;
        TMPro.TextMeshProUGUI best;
        if (sceneName == "Blockdoku_2D")
        {
            var ui = header.GetComponentInParent<UIManager_2D>(); score = ui.scoreText; best = ui.bestScoreText;
        }
        else if (sceneName == "10SUM")
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<UIManager>(); score = ui.scoreText; best = ui.bestScoreMainGameText;
        }
        else
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<MineSweeper_UIManager>(); score = ui.mineCountText; best = ui.headerBestTimeText;
        }
        var scoreSlot = sceneName == "MineSweeper" ? (RectTransform)score.transform.parent : score.rectTransform;
        Assert.AreEqual(new Vector2(.5f, 1), scoreSlot.anchorMin);
        Assert.AreEqual(-85, scoreSlot.anchoredPosition.y);
        if (sceneName != "MineSweeper")
        {
            var bestSlot = (RectTransform)best.transform.parent;
            Assert.AreEqual(1, bestSlot.anchorMin.x);
            Assert.AreEqual(-225, bestSlot.anchoredPosition.y);
            Assert.AreEqual(UnityEngine.TextAnchor.MiddleRight, bestSlot.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment);
        }
        else
        {
            Assert.AreEqual(1, best.rectTransform.anchorMin.x);
            Assert.AreEqual(-225, best.rectTransform.anchoredPosition.y);
            Assert.AreEqual(TMPro.TextAlignmentOptions.MidlineRight, best.alignment);
        }
    }

    [TestCase("Blockdoku_2D")]
    [TestCase("10SUM")]
    [TestCase("MineSweeper")]
    public void HeaderIconsStayNextToChangingValuesWithinAvailableWidth(string sceneName)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity");
        var groups = new System.Collections.Generic.List<UnityEngine.UI.HorizontalLayoutGroup>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var group in root.GetComponentsInChildren<UnityEngine.UI.HorizontalLayoutGroup>(true))
                if (group.name == "BestScoreDisplay" || group.name == "TimerDisplay" || group.name == "MineCountDisplay") groups.Add(group);
        Assert.AreEqual(sceneName == "Blockdoku_2D" ? 1 : 2, groups.Count);
        foreach (var group in groups)
        {
            var icon = group.transform.GetChild(0).GetComponent<UnityEngine.UI.Image>();
            var text = group.transform.GetChild(1).GetComponent<TMPro.TextMeshProUGUI>();
            Assert.NotNull(icon); Assert.NotNull(text);
            Assert.AreSame(icon.transform.parent, text.transform.parent);
            Assert.AreEqual(2, group.transform.childCount);
            var rect = (RectTransform)group.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            foreach (float width in new[] { 300f, 330f, 440f })
            {
                rect.sizeDelta = new Vector2(width, 110);
                foreach (var value in new[] { "1", "100", "2147483647" })
                {
                    text.text = value;
                    text.ForceMeshUpdate();
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    text.ForceMeshUpdate();
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                    var imageRect = (RectTransform)icon.transform;
                    float iconRight = imageRect.anchoredPosition.x + imageRect.rect.xMax;
                    float textLeft = text.rectTransform.anchoredPosition.x + text.rectTransform.rect.xMin;
                    float textRight = text.rectTransform.anchoredPosition.x + text.rectTransform.rect.xMax;
                    Assert.AreEqual(12, textLeft - iconRight, .1f, sceneName + "/" + group.name + "/" + value);
                    Assert.LessOrEqual(textRight, width + .1f);
                    Assert.GreaterOrEqual(imageRect.anchoredPosition.x + imageRect.rect.xMin, -.1f);
                    if (group.childAlignment == TextAnchor.MiddleRight) Assert.AreEqual(width, textRight, .1f);
                }
            }
        }
    }

    [UnityTest]
    public IEnumerator MuteButtonsDoNotTintOrCloseTheSettingsPanel()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var load = SceneManager.LoadSceneAsync("10SUM");
        while (!load.isDone) yield return null;
        yield return null;
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>();
        menu.Open();
        yield return null;
        var card = menu.popup.transform.Find("SettingsCard").gameObject;
        var backdropImage = menu.popup.GetComponent<UnityEngine.UI.Image>();
        var cardImage = card.GetComponent<UnityEngine.UI.Image>();
        Color backdropTint = backdropImage.canvasRenderer.GetColor();
        Color cardTint = cardImage.canvasRenderer.GetColor();
        var settings = menu.popup.GetComponent<SettingsUI>();
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        foreach (var surface in new[] { menu.popup, card })
            UnityEngine.EventSystems.ExecuteEvents.Execute(surface, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
        var buttons = new[] { settings.bgmMuteButton, settings.sfxMuteButton, settings.vibrationMuteButton };
        for (int channel = 0; channel < buttons.Length; channel++)
        {
            var backgroundSprite = buttons[channel].GetComponent<UnityEngine.UI.Image>().sprite;
            var stateIcon = channel == 0 ? settings.bgmMuteButtonIcon : channel == 1 ? settings.sfxMuteButtonIcon : settings.vibrationMuteButtonIcon;
            Assert.AreNotSame(buttons[channel].targetGraphic, stateIcon, "Mute state must change the icon, not the button face");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                bool wasMuted = channel == 0 ? SettingsManager.Instance.BgmMute : channel == 1 ? SettingsManager.Instance.SfxMute : SettingsManager.Instance.VibrationMute;
                var button = buttons[channel];
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.AreEqual(!wasMuted, channel == 0 ? SettingsManager.Instance.BgmMute : channel == 1 ? SettingsManager.Instance.SfxMute : SettingsManager.Instance.VibrationMute);
                Assert.True(menu.popup.activeInHierarchy);
                Assert.True(card.activeInHierarchy);
                Assert.AreSame(backgroundSprite, button.GetComponent<UnityEngine.UI.Image>().sprite);
                Assert.AreEqual(backdropTint, backdropImage.canvasRenderer.GetColor(), "Backdrop changed tint while muting");
                Assert.AreEqual(cardTint, cardImage.canvasRenderer.GetColor(), "Card changed tint while muting");
            }
        }
        var closeButton = Array.Find(menu.popup.GetComponentsInChildren<UnityEngine.UI.Button>(true), b => b.name == "CloseBtn");
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(closeButton.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Assert.False(GameMenu.IsOpen, "The shared prefab must route Close to its scene's GameMenu");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator TenSumSettingsKeepTimerRunningWhileRankingsPauseAndTimeoutClosesSettings()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var operation = SceneManager.LoadSceneAsync("10SUM");
        while (!operation.isDone) yield return null;
        yield return null;
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>();
        menu.Open();
        Assert.True(GameMenu.IsOpen);
        var settings = menu.popup.GetComponent<SettingsUI>();
        settings.bgmVolumeSlider.value = .35f;
        Assert.AreEqual(.35f, SettingsManager.Instance.BgmVolume, .001f);
        settings.bgmMuteButton.onClick.Invoke();
        Assert.True(SettingsManager.Instance.BgmMute);
        settings.bgmMuteButton.onClick.Invoke();
        Assert.False(SettingsManager.Instance.BgmMute);
        Assert.AreEqual(.35f, SettingsManager.Instance.BgmVolume, .001f);
        var field = typeof(TENSUM_GameManager).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
        float settingsTime = (float)field.GetValue(TENSUM_GameManager.Instance);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.Less((float)field.GetValue(TENSUM_GameManager.Instance), settingsTime);
        Assert.True(GameMenu.TryClose());
        Assert.False(GameMenu.IsOpen);
        RankingsPanel.OpenForLeaderboard(LeaderboardKind.TenSum);
        Assert.True(RankingOverlay.IsOpen);
        var timerField = typeof(TENSUM_GameManager).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
        float before = (float)timerField.GetValue(TENSUM_GameManager.Instance);
        yield return new WaitForSecondsRealtime(.15f);
        Assert.AreEqual(before, (float)timerField.GetValue(TENSUM_GameManager.Instance));
        RankingsPanel.TryClose();
        yield return new WaitForSecondsRealtime(.15f);
        Assert.False(RankingOverlay.IsOpen);
        Assert.Less((float)timerField.GetValue(TENSUM_GameManager.Instance), before);
        menu.Open();
        timerField.SetValue(TENSUM_GameManager.Instance, .03f);
        yield return new WaitForSecondsRealtime(.2f);
        Assert.True(TENSUM_GameManager.Instance.IsGameOver());
        Assert.False(GameMenu.IsOpen);
        Assert.True(TENSUM_GameManager.Instance.uiManager.gameOverPanel.activeInHierarchy);
        yield return new ExitPlayMode();
    }

    [Test]
    public void AdThresholdKeepsDueCountUntilAdIsConsumed()
    {
        var frequency = new AdFrequency();
        Assert.False(frequency.Record(MinigameType.TenSum, 3));
        Assert.False(frequency.Record(MinigameType.TenSum, 3));
        Assert.True(frequency.Record(MinigameType.TenSum, 3));
        for (int i = 0; i < 20; i++) Assert.True(frequency.Record(MinigameType.TenSum, 3));
        frequency.Consume(MinigameType.TenSum);
        Assert.False(frequency.Record(MinigameType.TenSum, 3));
        frequency.KeepDue(MinigameType.TenSum, 3);
        Assert.True(frequency.Record(MinigameType.TenSum, 3));
    }

    [Test]
    public void AdCountersAreIndependentBetweenGames()
    {
        var frequency = new AdFrequency();
        Assert.False(frequency.Record(MinigameType.MineSweeper, 2));
        Assert.True(frequency.Record(MinigameType.Blockdoku, 1));
        frequency.Consume(MinigameType.Blockdoku);
        Assert.True(frequency.Record(MinigameType.MineSweeper, 2));
        Assert.False(frequency.Record(MinigameType.TenSum, 3));
    }

    [Test]
    public void ScoreOutboxPreservesBestScoresAndAccountOwnership()
    {
        var box = new ScoreOutbox();
        box.Enqueue("A", LeaderboardKind.Blockdoku, 100);
        box.Enqueue("A", LeaderboardKind.Blockdoku, 50);
        box.Enqueue("B", LeaderboardKind.Blockdoku, 200);
        box.Enqueue("A", LeaderboardKind.MineSweeperBeginner, 30000);
        box.Enqueue("A", LeaderboardKind.MineSweeperBeginner, 20000);
        box.Enqueue("A", LeaderboardKind.MineSweeperBeginner, 40000);
        Assert.AreEqual(3, box.scores.Count);
        Assert.AreEqual(100, box.scores.Find(s => s.playerId == "A" && s.kind == LeaderboardKind.Blockdoku).value);
        Assert.AreEqual(20000, box.scores.Find(s => s.kind == LeaderboardKind.MineSweeperBeginner).value);
        box.Enqueue("A", LeaderboardKind.Blockdoku, 300);
        box.Acknowledge("A", LeaderboardKind.Blockdoku, 100);
        Assert.AreEqual(3, box.scores.Count, "An old callback must retain the new personal record.");
        box.Acknowledge("A", LeaderboardKind.Blockdoku, 300);
        Assert.AreEqual(2, box.scores.Count);
        Assert.True(box.scores.Exists(s => s.playerId == "B"));
    }

    [Test]
    public void RetiredGameIsNotInBuildOrTitleMenu()
    {
        Assert.False(Array.Exists(EditorBuildSettings.scenes, s => s.path.Contains("2048")));
        Assert.False(File.Exists("Assets/Scenes/2048.unity"));
        string title = File.ReadAllText("Assets/Scenes/Title.unity");
        StringAssert.DoesNotContain("TwoZeroFourEight", title);
        StringAssert.DoesNotContain("2048Btn", title);
    }

    [Test]
    public void SaveReplacementAndMalformedDataRecovery()
    {
        UseIsolatedSaves();
        try
        {
            SaveManager.SaveData("test.json", new PersonalData { bestScore = 10 });
            SaveManager.SaveData("test.json", new PersonalData { bestScore = 20 });
            Assert.AreEqual(20, SaveManager.LoadData<PersonalData>("test.json").bestScore);
            Assert.False(File.Exists(Path.Combine(testDirectory, "test.json.tmp")));
            File.WriteAllText(Path.Combine(testDirectory, "test.json"), "{invalid-json");
            Assert.False(SaveManager.TryLoadData<PersonalData>("test.json", out _));
        }
        finally { CleanIsolatedSaves(); }
    }

    [UnityTest]
    public IEnumerator BlockdokuFullClearRequiresAnEmptyBoard()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        new GameObject("Test Settings").AddComponent<SettingsManager>();
        var load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        var grid = GridManager_2D.Instance;
        var cells = Cells(grid);
        for (int c = 0; c < 8; c++) cells[0, c].SetOccupied(Color.red);
        grid.PlaceBlock(new Vector2Int(8, 0), new System.Collections.Generic.List<Vector2Int> { Vector2Int.zero }, Color.red);
        Assert.AreEqual(237, GameManager_2D.Instance.GetScore(), "Full clear gives its bonus and clear/placement points.");
        for (int c = 0; c < 9; c++) Assert.True(cells[0, c].IsEmpty);

        SaveManager.Delete("save.json");
        load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        grid = GridManager_2D.Instance;
        cells = Cells(grid);
        for (int c = 0; c < 8; c++) cells[0, c].SetOccupied(Color.red);
        for (int c = 0; c < 8; c++) cells[4, c].SetOccupied(Color.blue);
        cells[5, 0].SetOccupied(Color.blue);
        grid.PlaceBlock(new Vector2Int(8, 0), new System.Collections.Generic.List<Vector2Int> { Vector2Int.zero }, Color.red);
        Assert.AreEqual(19, GameManager_2D.Instance.GetScore(), "Nine remaining cells must not trigger FULL CLEAR.");
        Assert.False(cells[5, 0].IsEmpty);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BlockdokuSavesNextHandBeforeClearAnimationCompletes()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        new GameObject("Test Settings").AddComponent<SettingsManager>();
        var load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        var spawner = BlockSpawner_2D.Instance;
        var hand = spawner.GetSpawnedBlocks();
        for (int i = hand.Count - 1; i >= 1; i--) { UnityEngine.Object.Destroy(hand[i]); hand.RemoveAt(i); }
        var single = ScriptableObject.CreateInstance<BlockArray>();
        single.shapeRows = new System.Collections.Generic.List<string> { "1" };
        var block = hand[0].GetComponent<Block_2D>();
        block.Initialize(single, 0, Color.red);
        var cells = Cells(GridManager_2D.Instance);
        for (int c = 0; c < 8; c++) cells[0, c].SetOccupied(Color.red);
        var observations = new SaveObservation();
        SaveManager.OnSavedForTests += observations.Saved;
        try { spawner.BlockPlaced(block.gameObject, new Vector2Int(8, 0), block.GetShape(), Color.red); }
        finally { SaveManager.OnSavedForTests -= observations.Saved; }
        Assert.AreEqual(1, observations.BoardWrites, "The last-piece placement saves the resolved next hand exactly once.");
        var saved = SaveManager.LoadData<GridManager_2D.SaveData_2D>("save.json");
        Assert.NotNull(saved.blocks);
        Assert.Greater(saved.blocks.blocks.Count, 0, "A new hand must already be in the save while visuals run.");
        Assert.True(spawner.IsProcessingPlacement);
        Assert.False(spawner.TryBeginDrag(spawner.GetSpawnedBlocks()[0].GetComponent<Block_2D>()));
        UnityEngine.Object.Destroy(block.gameObject);
        UnityEngine.Object.Destroy(single);
        load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        Assert.Greater(BlockSpawner_2D.Instance.GetSpawnedBlocks().Count, 0);
        Assert.AreEqual(237, GameManager_2D.Instance.GetScore());
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        if (restoreArchivedStackTower)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var scene in scenes) if (scene.path == "Assets/Scenes/StackTower.unity") scene.enabled = false;
            EditorBuildSettings.scenes = scenes;
            restoreArchivedStackTower = false;
        }
        CleanIsolatedSaves();
    }

    [UnityTest]
    public IEnumerator RefreshButtonRecordsOnlyOneAdBoundaryWhileWaiting()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        new GameObject("Test Settings").AddComponent<SettingsManager>();
        var load = LoadBlockdoku();
        while (load.MoveNext()) yield return load.Current;
        var menu = UnityEngine.Object.FindFirstObjectByType<GameMenu>();
        menu.Open();
        var restart = Array.Find(menu.popup.GetComponentsInChildren<UnityEngine.UI.Button>(true), b => b.name == "ResetBtn");
        restart.onClick.Invoke();
        Assert.False(GameMenu.IsOpen);
        Assert.True(GameManager_2D.Instance.uiManager.gameResetPanel.activeSelf, "The prefab restart must retain Blockdoku's confirmation");
        GameManager_2D.Instance.uiManager.gameResetPanel.SetActive(false);
        var ui = UI_Functions.Instance != null ? UI_Functions.Instance : new GameObject("Test Navigation").AddComponent<UI_Functions>();
        if (AdManager.Instance != null)
        {
            AdManager.Instance.EnableAds = false;
            AdManager.Instance.enabled = false;
        }
        boundaryCount = 0;
        pendingBoundary = null;
        AdEventBus.OnGamePlayEnded += RecordBoundary;
        try
        {
            ui.TriggerGameRestart();
            ui.TriggerGameRestart();
            Assert.AreEqual(1, boundaryCount);
            Assert.True(GameNavigation.IsTransitioning);
            Assert.NotNull(pendingBoundary);
            pendingBoundary();
            yield return null;
            for (int frame = 0; frame < 100 && (GameManager_2D.Instance == null || GameNavigation.IsTransitioning); frame++) yield return null;
            Assert.False(GameNavigation.IsTransitioning);
            Assert.NotNull(GameManager_2D.Instance);
            Assert.AreEqual(0, GameManager_2D.Instance.GetScore());
        }
        finally { AdEventBus.OnGamePlayEnded -= RecordBoundary; }
        yield return new ExitPlayMode();
    }

    private void RecordBoundary(MinigameType game, Action callback)
    {
        Assert.AreEqual(MinigameType.Blockdoku, game);
        boundaryCount++;
        pendingBoundary = callback;
    }

    [UnityTest]
    public IEnumerator FailedScoreUploadIsRetriedUntilServerConfirmation()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var uploader = new FakeScoreUploader { Success = false };
        var box = new ScoreOutbox();
        var service = PrepareScoreUploadTest(uploader, box);
        service.SubmitScore(LeaderboardKind.TenSum, 20);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.AreEqual(1, uploader.Calls);
        Assert.AreEqual(1, box.scores.Count, "A failed or unconfirmed upload must stay pending.");
        Assert.AreEqual(1, SaveManager.LoadData<ScoreOutbox>("pendingScores.json").scores.Count);
        StringAssert.Contains("pending", service.ScoreStatus);
        uploader.Success = true;
        service.RetryPendingScores();
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.AreEqual(2, uploader.Calls);
        Assert.AreEqual(0, box.scores.Count);
        Assert.AreEqual(0, SaveManager.LoadData<ScoreOutbox>("pendingScores.json").scores.Count);
        StringAssert.Contains("complete", service.ScoreStatus);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ScoreEarnedDuringUploadRemainsPendingAfterOldConfirmation()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var uploader = new FakeScoreUploader { Success = true };
        var box = new ScoreOutbox();
        var service = PrepareScoreUploadTest(uploader, box);
        service.SubmitScore(LeaderboardKind.Blockdoku, 100);
        service.SubmitScore(LeaderboardKind.Blockdoku, 200);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.AreEqual(1, box.scores.Count);
        Assert.AreEqual(200, box.scores[0].value);
        service.RetryPendingScores();
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.AreEqual(0, box.scores.Count);
        Assert.AreEqual(2, uploader.Calls);
        yield return new ExitPlayMode();
    }

    private static PlayGamesService PrepareScoreUploadTest(ILeaderboardScoreUploader uploader, ScoreOutbox box)
    {
        var service = PlayGamesService.Instance;
        Assert.NotNull(service);
        var config = ScriptableObject.CreateInstance<LeaderboardSettings>();
        config.blockdoku = "test-blockdoku";
        config.tenSum = "test-tensum";
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(PlayGamesService).GetField("settings", flags).SetValue(service, config);
        typeof(PlayGamesService).GetField("outbox", flags).SetValue(service, box);
        typeof(PlayGamesService).GetField("playerId", flags).SetValue(service, "test-player");
        typeof(PlayGamesService).GetField("scoreUploader", flags).SetValue(service, uploader);
        typeof(PlayGamesService).GetField("nextRetry", flags).SetValue(service, Time.unscaledTime + 1000f);
        typeof(PlayGamesService).GetField("<IsAuthenticated>k__BackingField", flags).SetValue(service, true);
        return service;
    }

    private sealed class FakeScoreUploader : ILeaderboardScoreUploader
    {
        public bool Success;
        public int Calls;

        public IEnumerator Upload(string leaderboardId, long value, Action<ScoreUploadResult> completed)
        {
            Calls++;
            yield return null;
            completed(Success ? ScoreUploadResult.Confirmed() : ScoreUploadResult.Failed("Simulated server failure."));
        }
    }

    [UnityTest]
    public IEnumerator GuestChoiceNeverEnablesProfileAccessOrScoreUploads()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        var service = PlayGamesService.Instance;
        Assert.NotNull(service);
        for (int frame = 0; frame < 3; frame++) yield return null;
        Assert.False(service.IsAuthenticated);
        Assert.False(service.IsSigningIn);
        Assert.AreEqual("NotAttempted", service.LastAuthenticationResult);
        service.ContinueAsGuest();
        Assert.True(SaveManager.LoadData<PlayGamesLoginChoice>("playGamesChoice.json").hasChosen);
        service.SubmitScore(LeaderboardKind.TenSum, 30);
        service.ShowLeaderboard(LeaderboardKind.TenSum);
        service.RetryPendingScores();
        for (int frame = 0; frame < 3; frame++) yield return null;
        Assert.False(service.IsAuthenticated);
        Assert.AreEqual("NotAttempted", service.LastAuthenticationResult);
        Assert.False(SaveManager.Exists("pendingScores.json"), "Guest play must not enqueue or upload scores.");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator FirstRunLoginPromptCanBeDismissedWithoutAuthentication()
    {
        yield return new EnterPlayMode();
        UseIsolatedSaves();
        RankingsPanel.OpenLoginChoice();
        yield return null;
        Assert.NotNull(UnityEngine.Object.FindFirstObjectByType<RankingsPanel>());
        Assert.True(RankingsPanel.TryClose());
        yield return null;
        Assert.IsNull(UnityEngine.Object.FindFirstObjectByType<RankingsPanel>());
        Assert.True(SaveManager.LoadData<PlayGamesLoginChoice>("playGamesChoice.json").hasChosen);
        Assert.False(PlayGamesService.Instance.IsAuthenticated);
        Assert.AreEqual("NotAttempted", PlayGamesService.Instance.LastAuthenticationResult);
        yield return new ExitPlayMode();
    }

    private IEnumerator LoadBlockdoku()
    {
        var operation = SceneManager.LoadSceneAsync("Blockdoku_2D");
        while (!operation.isDone) yield return null;
        yield return null;
    }

    private static Cell_2D[,] Cells(GridManager_2D grid) => (Cell_2D[,])typeof(GridManager_2D)
        .GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(grid);

    private void UseIsolatedSaves()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "CasualPartyTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);
        SaveManager.TestDataDirectory = testDirectory;
    }

    private void CleanIsolatedSaves()
    {
        SaveManager.TestDataDirectory = null;
        if (string.IsNullOrEmpty(testDirectory)) return;
        string resolved = Path.GetFullPath(testDirectory);
        string temp = Path.GetFullPath(Path.GetTempPath());
        if (resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(resolved).StartsWith("CasualPartyTests-", StringComparison.Ordinal))
            Directory.Delete(resolved, true);
        testDirectory = null;
    }
}
