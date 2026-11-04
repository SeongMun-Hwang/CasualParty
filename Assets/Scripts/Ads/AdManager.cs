using GoogleMobileAds.Api;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AdManager : MonoBehaviour
{
    [SerializeField] private bool _enableAds = true;
    [SerializeField] private string _adUnitId;
    [SerializeField] private string _bannerAdUnitId;
    [SerializeField, Min(1)] private int blockdokuFrequency = 1;
    [SerializeField, Min(1)] private int tenSumFrequency = 3;
    [SerializeField, Min(1)] private int mineSweeperFrequency = 2;

    public static AdManager Instance { get; private set; }
    public bool IsShowingAd { get; private set; }
    private readonly AdFrequency frequency = new AdFrequency();
    private InterstitialAd interstitial;
    private BannerView banner;
    private bool initialized;
    private bool initializing;
    private bool loading;
    private int loadGeneration;
    private int retryAttempt;
    private Coroutine retryCoroutine;
    private Action completion;
    private MinigameType showingGame;
    private bool hasShowingGame;

    public bool EnableAds
    {
        get => _enableAds;
        set
        {
            if (_enableAds == value) return;
            _enableAds = value;
            if (!value)
            {
                HideBanner();
                CancelRetry();
                loadGeneration++;
                loading = false;
                if (!IsShowingAd) { interstitial?.Destroy(); interstitial = null; }
            }
            else
            {
                InitializeAds();
                if (initialized) { LoadInterstitialAd(); LoadBannerAd(); }
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        AdEventBus.OnGamePlayEnded += HandleGamePlayEnded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        AdEventBus.OnGamePlayEnded -= HandleGamePlayEnded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        loadGeneration++;
        interstitial?.Destroy();
        banner?.Destroy();
        Instance = null;
    }

    private void Start() => InitializeAds();

    private void InitializeAds()
    {
        if (!_enableAds || initialized || initializing) return;
        initializing = true;
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ =>
        {
            if (this == null) return;
            initializing = false;
            initialized = true;
            if (!_enableAds) return;
            LoadInterstitialAd();
            LoadBannerAd();
        });
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (initialized && _enableAds) StartCoroutine(RefreshBanner());
    }

    private IEnumerator RefreshBanner()
    {
        yield return null;
        LoadBannerAd();
    }

    public void LoadBannerAd()
    {
        if (!initialized || !_enableAds || string.IsNullOrWhiteSpace(_bannerAdUnitId)) return;
        banner?.Destroy();
        var size = AdSize.GetPortraitAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
        banner = new BannerView(_bannerAdUnitId, size, AdPosition.Bottom);
        banner.LoadAd(new AdRequest());
    }

    public void HideBanner() => banner?.Hide();
    public void ShowBanner() { if (_enableAds) banner?.Show(); }

    public void SetBannerPosition(RectTransform placeholder)
    {
        if (banner == null || placeholder == null || !_enableAds) return;
        var corners = new Vector3[4];
        placeholder.GetWorldCorners(corners);
        banner.SetPosition(Mathf.RoundToInt(corners[1].x), Mathf.RoundToInt(Screen.height - corners[1].y));
    }

    private int Threshold(MinigameType game)
    {
        switch (game)
        {
            case MinigameType.Blockdoku: return Mathf.Max(1, blockdokuFrequency);
            case MinigameType.TenSum: return Mathf.Max(1, tenSumFrequency);
            default: return Mathf.Max(1, mineSweeperFrequency);
        }
    }

    private void HandleGamePlayEnded(MinigameType game, Action onComplete)
    {
        // A repeated click must not replace the callback of the ad already on screen.
        if (IsShowingAd) return;
        if (!_enableAds) { onComplete?.Invoke(); return; }
        bool due = frequency.Record(game, Threshold(game));
        if (due && interstitial != null && interstitial.CanShowAd())
        {
            showingGame = game;
            hasShowingGame = true;
            frequency.Consume(game);
            completion = onComplete;
            ShowInterstitialAd();
            return;
        }

        // Keep a due count if offline/not loaded. Show only at a future game boundary,
        // never asynchronously in the middle of a newly started game.
        LoadInterstitialAd();
        onComplete?.Invoke();
    }

    public void LoadInterstitialAd()
    {
        if (!initialized || !_enableAds || loading || IsShowingAd || retryCoroutine != null ||
            string.IsNullOrWhiteSpace(_adUnitId)) return;
        if (interstitial != null && interstitial.CanShowAd()) return;
        interstitial?.Destroy();
        interstitial = null;
        loading = true;
        int generation = ++loadGeneration;
        InterstitialAd.Load(_adUnitId, new AdRequest(), (ad, error) =>
        {
            if (this == null || generation != loadGeneration || !_enableAds)
            {
                ad?.Destroy();
                return;
            }
            loading = false;
            if (error != null || ad == null)
            {
                ad?.Destroy();
                Debug.LogWarning($"[Ads] Load failed: {error}");
                retryAttempt++;
                retryCoroutine = StartCoroutine(RetryLoad(Math.Pow(2, Math.Min(6, retryAttempt))));
                return;
            }
            interstitial = ad;
            retryAttempt = 0;
            ad.OnAdFullScreenContentClosed += () => FinishAd(ad, false);
            ad.OnAdFullScreenContentFailed += errorOnShow =>
            {
                Debug.LogWarning($"[Ads] Show failed: {errorOnShow}");
                FinishAd(ad, true);
            };
        });
    }

    private IEnumerator RetryLoad(double seconds)
    {
        yield return new WaitForSecondsRealtime((float)seconds);
        retryCoroutine = null;
        LoadInterstitialAd();
    }

    private void CancelRetry()
    {
        if (retryCoroutine != null) StopCoroutine(retryCoroutine);
        retryCoroutine = null;
    }

    public void ShowInterstitialAd()
    {
        if (IsShowingAd) return;
        if (!_enableAds || interstitial == null || !interstitial.CanShowAd())
        {
            if (hasShowingGame) frequency.KeepDue(showingGame, Threshold(showingGame));
            hasShowingGame = false;
            var callback = completion;
            completion = null;
            callback?.Invoke();
            LoadInterstitialAd();
            return;
        }
        IsShowingAd = true;
        interstitial.Show();
    }

    private void FinishAd(InterstitialAd ad, bool failed)
    {
        if (!IsShowingAd || ad != interstitial) return;
        IsShowingAd = false;
        if (failed && hasShowingGame) frequency.KeepDue(showingGame, Threshold(showingGame));
        hasShowingGame = false;
        interstitial = null;
        ad.Destroy();
        var callback = completion;
        completion = null;
        try { callback?.Invoke(); }
        finally { LoadInterstitialAd(); }
    }
}
