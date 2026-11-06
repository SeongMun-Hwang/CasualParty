using UnityEngine;

public enum LeaderboardTarget { All, Blockdoku, TenSum, MineSweeper }

public sealed class LeaderboardButton : MonoBehaviour
{
    public LeaderboardTarget target;

    public void Open()
    {
        var service = PlayGamesService.Instance;
        if (service == null) return;
        switch (target)
        {
            case LeaderboardTarget.Blockdoku: service.ShowLeaderboard(LeaderboardKind.Blockdoku); break;
            case LeaderboardTarget.TenSum: service.ShowLeaderboard(LeaderboardKind.TenSum); break;
            case LeaderboardTarget.MineSweeper:
                var game = MineSweeper_GameManager.Instance;
                if (game != null && game.HasSelectedDifficulty)
                    service.ShowLeaderboard(LeaderboardSettings.ForDifficulty(game.CurrentDifficulty));
                else RankingsPanel.OpenMinesweeper();
                break;
            default: service.ShowAllLeaderboards(); break;
        }
    }
}
