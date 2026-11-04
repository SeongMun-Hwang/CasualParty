using System;
using System.Collections.Generic;

[Serializable]
public sealed class PendingScore
{
    public string playerId;
    public LeaderboardKind kind;
    public long value;
}

[Serializable]
public sealed class ScoreOutbox
{
    public List<PendingScore> scores = new List<PendingScore>();

    public void Enqueue(string playerId, LeaderboardKind kind, long value)
    {
        if (string.IsNullOrEmpty(playerId) || value <= 0) return;
        if (scores == null) scores = new List<PendingScore>();
        var existing = scores.Find(s => s != null && s.playerId == playerId && s.kind == kind);
        if (existing == null) scores.Add(new PendingScore { playerId = playerId, kind = kind, value = value });
        else if (LeaderboardSettings.IsTime(kind) ? value < existing.value : value > existing.value) existing.value = value;
    }

    public void Acknowledge(string playerId, LeaderboardKind kind, long sentValue)
    {
        // A callback for an older upload must not discard a newer personal record.
        scores?.RemoveAll(s => s != null && s.playerId == playerId && s.kind == kind && s.value == sentValue);
    }
}
