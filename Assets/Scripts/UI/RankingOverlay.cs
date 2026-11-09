public static class RankingOverlay
{
    public static bool IsOpen => GameMenu.IsOpen || IsRankingOpen;
    public static bool IsRankingOpen => RankingsPanel.IsOpen ||
        (PlayGamesService.Instance != null && PlayGamesService.Instance.IsLeaderboardBusy);
}
