using System.Collections.Generic;

// Counts completed/abandoned runs, independently of scene reloads and SDK readiness.
public sealed class AdFrequency
{
    private readonly Dictionary<MinigameType, int> counts = new Dictionary<MinigameType, int>();

    public bool Record(MinigameType game, int threshold)
    {
        threshold = System.Math.Max(1, threshold);
        counts.TryGetValue(game, out int count);
        counts[game] = count >= threshold ? threshold : count + 1;
        return counts[game] >= threshold;
    }

    public void Consume(MinigameType game) => counts[game] = 0;

    public void KeepDue(MinigameType game, int threshold) => counts[game] = System.Math.Max(1, threshold);
}
