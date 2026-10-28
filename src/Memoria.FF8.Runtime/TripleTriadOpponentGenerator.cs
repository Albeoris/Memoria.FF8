namespace Memoria.FF8.Runtime;

/// <summary>The independently saved native card RNG at 1CFEFB4, advanced by 534AA0.</summary>
public readonly record struct TripleTriadRandom(uint State)
{
    public TripleTriadRandom Draw(out int value)
    {
        uint next = unchecked(State * 69069u + 1u);
        value = (int)(next >> 17);
        return new TripleTriadRandom(next);
    }
}

public sealed record TripleTriadOpponentHand(IReadOnlyList<int> Cards, uint RandomState, int RandomDraws);

/// <summary>The normal native 537640 deck generator, with explicit source operands and RNG state.</summary>
public static class TripleTriadOpponentGenerator
{
    public static TripleTriadOpponentHand Generate(NativeCardInventory inventory, byte opponentOwner, byte rareChance, byte commonLevelMask, uint randomState, int handSize = 5)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        inventory.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(handSize, 1);
        TripleTriadRandom random = new(randomState);
        int draws = 0;
        int Next()
        {
            random = random.Draw(out int value);
            draws++;
            return value;
        }
        List<int> hand = [];
        int chance = rareChance;
        if (opponentOwner != 0)
        {
            for (int card = NativeCardInventory.CommonCardCount; card < NativeCardInventory.CardCount && hand.Count < handSize; card++)
            {
                if (inventory.Location(card) == opponentOwner && Next() % 100 < chance)
                {
                    hand.Add(card);
                    // Native reloads the original chance, then shifts once after every successful insertion.
                    chance = rareChance >> 1;
                }
            }
        }
        int mask = commonLevelMask == 0 ? 1 : commonLevelMask;
        int[] levels = Enumerable.Range(0, 7).Where(level => (mask & 1 << level) != 0).ToArray();
        if (hand.Count < handSize && levels.Length == 0)
        {
            throw new NotSupportedException("Native common-level mask has no selectable level; the original division-by-zero path is unsupported.");
        }
        while (hand.Count < handSize)
        {
            int level = levels[Next() % levels.Length];
            int card = level * 11 + Next() % 11;
            if (card == 47 || hand.Contains(card) && hand.Count(id => id < NativeCardInventory.CommonCardCount) < levels.Sum(selected => selected == 4 ? 10 : 11))
            {
                continue;
            }
            hand.Add(card);
        }
        return new TripleTriadOpponentHand(hand.ToArray(), random.State, draws);
    }
}
