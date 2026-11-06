using System;
using System.Collections;
using System.IO;
using System.Reflection;
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
    public IEnumerator RankingOverlayPausesTenSumTimerAndRestoresItOnClose()
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
        float paused = (float)field.GetValue(TENSUM_GameManager.Instance);
        yield return null;
        yield return null;
        Assert.AreEqual(paused, (float)field.GetValue(TENSUM_GameManager.Instance));
        Assert.True(GameMenu.TryClose());
        Assert.False(GameMenu.IsOpen);
        RankingsPanel.OpenForLeaderboard(LeaderboardKind.TenSum);
        Assert.True(RankingOverlay.IsOpen);
        var timerField = typeof(TENSUM_GameManager).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
        float before = (float)timerField.GetValue(TENSUM_GameManager.Instance);
        yield return null;
        yield return null;
        Assert.AreEqual(before, (float)timerField.GetValue(TENSUM_GameManager.Instance));
        RankingsPanel.TryClose();
        yield return null;
        yield return null;
        Assert.False(RankingOverlay.IsOpen);
        Assert.Less((float)timerField.GetValue(TENSUM_GameManager.Instance), before);
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
        spawner.BlockPlaced(block.gameObject, new Vector2Int(8, 0), block.GetShape(), Color.red);
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
