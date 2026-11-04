using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameNavigation
{
    public static bool IsTransitioning { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        IsTransitioning = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => IsTransitioning = false;

    public static void Load(string scene, Action beforeLoad = null, MinigameType? endedGame = null)
    {
        if (IsTransitioning || (AdManager.Instance != null && AdManager.Instance.IsShowingAd)) return;
        IsTransitioning = true;
        Action complete = () =>
        {
            try
            {
                beforeLoad?.Invoke();
                SceneManager.LoadScene(scene);
            }
            catch
            {
                IsTransitioning = false;
                throw;
            }
        };
        if (endedGame.HasValue) AdEventBus.TriggerGamePlayEnded(endedGame.Value, complete);
        else complete();
    }
}
