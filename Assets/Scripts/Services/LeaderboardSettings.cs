using UnityEngine;

public enum LeaderboardKind { Blockdoku, TenSum, MineSweeperBeginner, MineSweeperIntermediate, MineSweeperAdvanced }

[CreateAssetMenu(menuName = "CasualParty/Leaderboard Settings")]
public sealed class LeaderboardSettings : ScriptableObject
{
    public string blockdoku;
    public string tenSum;
    public string mineSweeperBeginner;
    public string mineSweeperIntermediate;
    public string mineSweeperAdvanced;

    public string GetId(LeaderboardKind kind)
    {
        switch (kind)
        {
            case LeaderboardKind.Blockdoku: return blockdoku;
            case LeaderboardKind.TenSum: return tenSum;
            case LeaderboardKind.MineSweeperBeginner: return mineSweeperBeginner;
            case LeaderboardKind.MineSweeperIntermediate: return mineSweeperIntermediate;
            default: return mineSweeperAdvanced;
        }
    }

    public static bool IsTime(LeaderboardKind kind) => kind >= LeaderboardKind.MineSweeperBeginner;

    public static LeaderboardKind ForDifficulty(MineSweeperDifficulty difficulty)
    {
        switch (difficulty)
        {
            case MineSweeperDifficulty.Beginner: return LeaderboardKind.MineSweeperBeginner;
            case MineSweeperDifficulty.Intermediate: return LeaderboardKind.MineSweeperIntermediate;
            default: return LeaderboardKind.MineSweeperAdvanced;
        }
    }
}
