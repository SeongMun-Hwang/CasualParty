using System;

public enum MinigameType
{
    Blockdoku,
    TenSum,
    MineSweeper
}

public static class AdEventBus
{
    /// <summary>
    /// Event triggered when a game play session ends.
    /// Param 1: Type of the minigame.
    /// Param 2: Callback to execute after the ad is closed (or if no ad is shown).
    /// </summary>
    public static event Action<MinigameType, Action> OnGamePlayEnded;

    public static void TriggerGamePlayEnded(MinigameType gameType, Action onComplete)
    {
        if (OnGamePlayEnded != null)
        {
            OnGamePlayEnded.Invoke(gameType, onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }
}
