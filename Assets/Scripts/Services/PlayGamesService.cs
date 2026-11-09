using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

public sealed class PlayGamesService : MonoBehaviour
{
    public static PlayGamesService Instance { get; private set; }
    public event Action Changed;
    public bool IsSigningIn { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public string LastAuthenticationResult { get; private set; } = "NotAttempted";
    public string PlayerName { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Sign in to use rankings.";
    public string ScoreStatus { get; private set; } = string.Empty;
    private string playerId;
    private LeaderboardSettings settings;
    private ScoreOutbox outbox;
    private bool flushing;
    private float nextRetry;
    private int authenticationRequest;
    private ILeaderboardScoreUploader scoreUploader = new LeaderboardScoreUploader();
    private bool openingLeaderboard;
    public bool IsLeaderboardVisible { get; private set; }
    public bool IsLeaderboardBusy => openingLeaderboard || IsLeaderboardVisible;
    public bool SupportsNativeLeaderboards
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("Play Games Service").AddComponent<PlayGamesService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        settings = Resources.Load<LeaderboardSettings>("LeaderboardSettings");
        outbox = SaveManager.LoadData<ScoreOutbox>("pendingScores.json");
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // The native SDK may authenticate its platform identity independently.
    // This app does not access that identity or enable ranking uploads until SignIn is chosen.
    private void Start() => OfferLoginChoice();

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => OfferLoginChoice();

    private void OfferLoginChoice()
    {
        if (SceneManager.GetActiveScene().name != "Title" || IsAuthenticated || IsSigningIn) return;
        if (!SaveManager.LoadData<PlayGamesLoginChoice>("playGamesChoice.json").hasChosen)
            RankingsPanel.OpenLoginChoice();
    }

    public void ContinueAsGuest()
    {
        SaveManager.SaveData("playGamesChoice.json", new PlayGamesLoginChoice { hasChosen = true });
        authenticationRequest++;
        IsSigningIn = false;
        IsAuthenticated = false;
        playerId = null;
        PlayerName = string.Empty;
        ScoreStatus = string.Empty;
        SetStatus("Play locally. Sign in to use rankings.");
    }

    public bool IsConfigured(LeaderboardKind kind) => settings != null && !string.IsNullOrWhiteSpace(settings.GetId(kind));

    public void SignIn()
    {
        SaveManager.SaveData("playGamesChoice.json", new PlayGamesLoginChoice { hasChosen = true });
        BeginUserSignIn();
    }

    private void BeginUserSignIn()
    {
        if (IsSigningIn || IsAuthenticated) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        var platformSettings = PlayGamesSettings.LoadInstance();
        if (platformSettings == null || string.IsNullOrWhiteSpace(platformSettings.AppId))
        {
            SetStatus("Rankings are not available yet.");
            return;
        }
        IsSigningIn = true;
        LastAuthenticationResult = "Pending";
        if (Debug.isDebugBuild) PlayGamesPlatform.DebugLogEnabled = true;
        Debug.Log($"[PlayGames] Authentication started. Flow=manual, appId={platformSettings.AppId}, package={Application.identifier}");
        SetStatus("Signing in...");
        int request = ++authenticationRequest;
        Action<SignInStatus> callback = result =>
        {
            if (this == null || request != authenticationRequest) return;
            IsSigningIn = false;
            LastAuthenticationResult = result.ToString();
            if (result == SignInStatus.Success) Debug.Log("[PlayGames] Authentication succeeded.");
            else Debug.LogWarning($"[PlayGames] Authentication failed. Status={result}, code={(int)result}, flow=manual. Check the installed signing certificate and PGS tester access; this status alone does not identify the cause.");
            IsAuthenticated = result == SignInStatus.Success;
            playerId = IsAuthenticated ? PlayGamesPlatform.Instance.GetUserId() : null;
            PlayerName = IsAuthenticated ? PlayGamesPlatform.Instance.GetUserDisplayName() : string.Empty;
            SetStatus(IsAuthenticated ? "Signed in as " + PlayerName : "Sign-in did not complete. You can try again.");
            if (IsAuthenticated) Flush();
        };
        PlayGamesPlatform.Instance.ManuallyAuthenticate(callback);
        StartCoroutine(SignInTimeout(request));
#else
        SetStatus("Rankings are available in the Android version.");
#endif
    }

    private IEnumerator SignInTimeout(int request)
    {
        yield return new WaitForSecondsRealtime(30f);
        if (IsSigningIn && request == authenticationRequest)
        {
            authenticationRequest++;
            IsSigningIn = false;
            LastAuthenticationResult = "Timeout";
            Debug.LogWarning("[PlayGames] Authentication timed out.");
            SetStatus("Sign-in timed out. Please try again.");
        }
    }

    public void ShowLeaderboard(LeaderboardKind kind)
    {
        if (!IsAuthenticated) { RankingsPanel.OpenForLeaderboard(kind); return; }
        if (!IsConfigured(kind)) { SetStatus("This ranking is not available yet."); RankingsPanel.OpenForLeaderboard(kind); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsLeaderboardBusy) StartCoroutine(OpenLeaderboardAfterUpload(kind));
#else
        SetStatus("Rankings are available in the Android version.");
        RankingsPanel.OpenForLeaderboard(kind);
#endif
    }

    public void ShowAllLeaderboards()
    {
        if (!IsAuthenticated) { RankingsPanel.OpenForLeaderboard(null); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!IsLeaderboardBusy) StartCoroutine(OpenLeaderboardAfterUpload(null));
#else
        SetStatus("Rankings are available in the Android version.");
        RankingsPanel.OpenForLeaderboard(null);
#endif
    }

    private IEnumerator OpenLeaderboardAfterUpload(LeaderboardKind? kind)
    {
        openingLeaderboard = true;
        try
        {
            Flush();
            while (flushing) yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAuthenticated) yield break;
            IsLeaderboardVisible = true;
            // A null ID opens Google's built-in list of all leaderboards.
            try
            {
                PlayGamesPlatform.Instance.ShowLeaderboardUI(kind.HasValue ? settings.GetId(kind.Value) : null, status =>
                {
                    if (this == null) return;
                    IsLeaderboardVisible = false;
                    if (status != UIStatus.Valid) SetStatus("Could not open rankings. Please try again.");
                });
            }
            catch (Exception exception)
            {
                IsLeaderboardVisible = false;
                Debug.LogWarning("[PlayGames] Could not open rankings: " + exception.Message);
                SetStatus("Could not open rankings. Please try again.");
            }
#endif
        }
        finally { openingLeaderboard = false; }
    }

    public void SubmitScore(LeaderboardKind kind, long value)
    {
        // Guest records stay local. Pending uploads belong to the account that earned them.
        if (!IsAuthenticated) { Debug.Log($"[PlayGames] Local-only score. Game={kind}, score={value}"); return; }
        if (value <= 0) { Debug.Log($"[PlayGames] Score skipped: zero/invalid score. Game={kind}"); return; }
        if (BlockdokuAutoTester.Instance != null && BlockdokuAutoTester.Instance.isTesting) return;
        if (!IsConfigured(kind)) Debug.LogWarning($"[PlayGames] Score queued without configured leaderboard. Game={kind}");
        outbox.Enqueue(playerId, kind, value);
        SaveManager.SaveData("pendingScores.json", outbox);
        Debug.Log($"[PlayGames] Score queued. Game={kind}, score={value}");
        SetScoreStatus("Score upload pending.");
        Flush();
    }

    public void SubmitTime(MineSweeperDifficulty difficulty, float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0 || seconds == float.MaxValue) return;
        SubmitScore(LeaderboardSettings.ForDifficulty(difficulty), Math.Max(1L, (long)Math.Round(seconds * 1000d)));
    }

    private void Update()
    {
        if (IsAuthenticated && !flushing && Time.unscaledTime >= nextRetry)
        {
            nextRetry = Time.unscaledTime + 30f;
            Flush();
        }
    }

    private void Flush()
    {
        if (!flushing && IsAuthenticated && outbox.scores != null &&
            outbox.scores.Exists(s => s != null && s.playerId == playerId && IsConfigured(s.kind)))
            StartCoroutine(FlushRoutine());
    }

    public void RetryPendingScores() => Flush();

    private IEnumerator FlushRoutine()
    {
        flushing = true;
        bool failed = false;
        try
        {
            var snapshot = outbox.scores == null ? new PendingScore[0] : outbox.scores.ToArray();
            foreach (var item in snapshot)
            {
                if (item == null || item.playerId != playerId || !IsAuthenticated || !IsConfigured(item.kind)) continue;
                string owner = item.playerId;
                long sentValue = item.value;
                string boardId = settings.GetId(item.kind);
                ScoreUploadResult result = null;
                SetScoreStatus("Uploading score...");
                Debug.Log($"[PlayGames] Upload started. Game={item.kind}, board={boardId}, score={sentValue}");
                yield return StartCoroutine(scoreUploader.Upload(boardId, sentValue, response => result = response));
                if (result != null && result.Success)
                {
                    outbox.Acknowledge(owner, item.kind, sentValue);
                    SaveManager.SaveData("pendingScores.json", outbox);
                    Debug.Log($"[PlayGames] Server confirmed score. Game={item.kind}, board={boardId}, score={sentValue}");
                }
                else
                {
                    failed = true;
                    Debug.LogWarning($"[PlayGames] Upload unconfirmed; keeping pending record. Game={item.kind}, board={boardId}, score={sentValue}, error={result?.Error ?? "No result"}");
                }
            }
            bool remaining = outbox.scores.Exists(s => s != null && s.playerId == playerId);
            SetScoreStatus(failed || remaining ? "Score upload pending. Will retry." : "Score upload complete.");
        }
        finally
        {
            nextRetry = Time.unscaledTime + 30f;
            flushing = false;
        }
    }

    private void OnApplicationFocus(bool focused)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (focused && IsAuthenticated)
        {
            IsAuthenticated = PlayGamesPlatform.Instance.IsAuthenticated();
            if (!IsAuthenticated) { playerId = null; PlayerName = string.Empty; SetStatus("Sign in to use rankings."); }
            else
            {
                playerId = PlayGamesPlatform.Instance.GetUserId();
                PlayerName = PlayGamesPlatform.Instance.GetUserDisplayName();
                SetStatus("Signed in as " + PlayerName);
                Flush();
            }
        }
#endif
    }

    private void SetStatus(string status) { Status = status; Changed?.Invoke(); }
    private void SetScoreStatus(string status) { ScoreStatus = status; Changed?.Invoke(); }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }
}

[Serializable]
public sealed class PlayGamesLoginChoice
{
    // Records that the introduction was dismissed, not permission to log in automatically.
    public bool hasChosen;
}
