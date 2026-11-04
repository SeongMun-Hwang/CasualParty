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
        StringAssert.Contains("ShowRankings", title);
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
