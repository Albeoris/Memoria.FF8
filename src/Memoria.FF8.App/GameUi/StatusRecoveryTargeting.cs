namespace Memoria.FF8.GameUi;

/// <summary>Ranks removable negative conditions for the initial cursor only. Callers supply eligible, actually removable masks.</summary>
public static class StatusRecoveryTargeting
{
    private static readonly (uint Mask, int Weight)[] SavedWeights = [(1, 1000), (4, 15), (32, 13), (64, 7), (2, 6), (16, 4), (8, 3)];
    private static readonly (uint Mask, int Weight)[] BattleWeights = [(8, 16), (16384, 14), (4096, 12), (1024, 11), (0x400000, 10), (1, 9), (512, 8), (4, 2)];

    public static int Score(uint saved, uint battle)
    {
        int score = 0;
        foreach (var condition in SavedWeights)
        {
            if ((saved & condition.Mask) != 0)
            {
                score += condition.Weight;
            }
        }
        foreach (var condition in BattleWeights)
        {
            if ((battle & condition.Mask) != 0)
            {
                score += condition.Weight;
            }
        }
        return score;
    }
}
