using System;
using System.Collections;
using UnityEngine;
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
    }

    private void Start() => SignIn(false);

    public bool IsConfigured(LeaderboardKind kind) => settings != null && !string.IsNullOrWhiteSpace(settings.GetId(kind));

    public void SignIn() => SignIn(true);

    private void SignIn(bool interactive)
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
        Debug.Log($"[PlayGames] Authentication started. Flow={(interactive ? "manual" : "automatic")}, appId={platformSettings.AppId}, package={Application.identifier}");
        SetStatus("Signing in...");
        int request = ++authenticationRequest;
        Action<SignInStatus> callback = result =>
        {
            if (this == null || request != authenticationRequest) return;
            IsSigningIn = false;
            LastAuthenticationResult = result.ToString();
            if (result == SignInStatus.Success) Debug.Log("[PlayGames] Authentication succeeded.");
            else Debug.LogWarning($"[PlayGames] Authentication failed. Status={result}, code={(int)result}, flow={(interactive ? "manual" : "automatic")}. Check the installed signing certificate and PGS tester access; this status alone does not identify the cause.");
            IsAuthenticated = result == SignInStatus.Success;
            playerId = IsAuthenticated ? PlayGamesPlatform.Instance.GetUserId() : null;
            PlayerName = IsAuthenticated ? PlayGamesPlatform.Instance.GetUserDisplayName() : string.Empty;
            SetStatus(IsAuthenticated ? "Signed in as " + PlayerName : "Sign-in did not complete. You can try again.");
            if (IsAuthenticated) Flush();
        };
        if (interactive) PlayGamesPlatform.Instance.ManuallyAuthenticate(callback);
        else PlayGamesPlatform.Instance.Authenticate(callback);
        StartCoroutine(SignInTimeout(request));
#else
        if (interactive) SetStatus("Rankings are available in the Android version.");
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
        if (!IsAuthenticated) { SetStatus("Sign in before opening rankings."); return; }
        if (!IsConfigured(kind)) { SetStatus("This ranking is not available yet."); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!openingLeaderboard) StartCoroutine(OpenLeaderboardAfterUpload(kind));
#endif
    }

    private IEnumerator OpenLeaderboardAfterUpload(LeaderboardKind kind)
    {
        openingLeaderboard = true;
        try
        {
            Flush();
            while (flushing) yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsAuthenticated) yield break;
            PlayGamesPlatform.Instance.ShowLeaderboardUI(settings.GetId(kind), status =>
            {
                if (this != null && status != UIStatus.Valid) SetStatus("Could not open rankings. Please try again.");
            });
#endif
        }
        finally { openingLeaderboard = false; }
    }

    public void SubmitScore(LeaderboardKind kind, long value)
    {
        // Guest records stay local. Pending uploads belong to the account that earned them.
        if (!IsAuthenticated) { Debug.LogWarning($"[PlayGames] Score skipped: not authenticated. Game={kind}, score={value}"); return; }
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
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
