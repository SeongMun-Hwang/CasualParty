using System;
using System.Collections;
using UnityEngine;

public sealed class ScoreUploadResult
{
    public bool Success;
    public string Error;

    public static ScoreUploadResult Confirmed() => new ScoreUploadResult { Success = true };
    public static ScoreUploadResult Failed(string error) => new ScoreUploadResult { Error = error };
}

public interface ILeaderboardScoreUploader
{
    IEnumerator Upload(string leaderboardId, long value, Action<ScoreUploadResult> completed);
}

public sealed class LeaderboardScoreUploader : ILeaderboardScoreUploader
{
    public IEnumerator Upload(string leaderboardId, long value, Action<ScoreUploadResult> completed)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject task = null;
        string startError = null;
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var games = new AndroidJavaClass("com.google.android.gms.games.PlayGames"))
            using (var client = games.CallStatic<AndroidJavaObject>("getLeaderboardsClient", activity))
                task = client.Call<AndroidJavaObject>("submitScoreImmediate", leaderboardId, value);
        }
        catch (Exception e) { startError = e.Message; }

        if (task == null)
        {
            completed(ScoreUploadResult.Failed(startError ?? "No submission task returned."));
            yield break;
        }

        // The plugin's ReportScore callback acknowledges local queuing, not server acceptance.
        // Poll the immediate API's Task on Unity's main thread and retain pending data on timeout.
        using (task)
        {
            float deadline = Time.realtimeSinceStartup + 20f;
            while (true)
            {
                ScoreUploadResult result = null;
                try
                {
                    if (task.Call<bool>("isComplete"))
                    {
                        if (task.Call<bool>("isSuccessful")) result = ScoreUploadResult.Confirmed();
                        else
                        {
                            using (var error = task.Call<AndroidJavaObject>("getException"))
                                result = ScoreUploadResult.Failed(error != null ? error.Call<string>("toString") : "Submission canceled.");
                        }
                    }
                }
                catch (Exception e) { result = ScoreUploadResult.Failed(e.Message); }
                if (result != null) { completed(result); yield break; }
                if (Time.realtimeSinceStartup >= deadline)
                {
                    completed(ScoreUploadResult.Failed("Server confirmation timed out."));
                    yield break;
                }
                yield return null;
            }
        }
#else
        completed(ScoreUploadResult.Failed("Android device required."));
        yield break;
#endif
    }
}
