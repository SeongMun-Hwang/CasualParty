using System;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

public sealed class PlayGamesAuthenticationResult
{
    public bool success;
    public string status, playerId, playerName;
}

public interface IPlayGamesAuthenticationClient
{
    bool IsAvailable { get; }
    void Authenticate(bool manual, Action<PlayGamesAuthenticationResult> completed);
}

public sealed class PlayGamesAuthenticationClient : IPlayGamesAuthenticationClient
{
    public bool IsAvailable
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

    public void Authenticate(bool manual, Action<PlayGamesAuthenticationResult> completed)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var configuration = PlayGamesSettings.LoadInstance();
        if (configuration == null || string.IsNullOrWhiteSpace(configuration.AppId))
        {
            completed(new PlayGamesAuthenticationResult { status = "NotConfigured" });
            return;
        }
        Action<SignInStatus> callback = result =>
        {
            bool success = result == SignInStatus.Success;
            completed(new PlayGamesAuthenticationResult
            {
                success = success,
                status = result.ToString(),
                playerId = success ? PlayGamesPlatform.Instance.GetUserId() : null,
                playerName = success ? PlayGamesPlatform.Instance.GetUserDisplayName() : null
            });
        };
        // Authenticate reads the SDK's automatic sign-in result. Only ManuallyAuthenticate requests interactive sign-in.
        if (manual) PlayGamesPlatform.Instance.ManuallyAuthenticate(callback);
        else PlayGamesPlatform.Instance.Authenticate(callback);
#else
        completed(new PlayGamesAuthenticationResult { status = "UnsupportedPlatform" });
#endif
    }
}
