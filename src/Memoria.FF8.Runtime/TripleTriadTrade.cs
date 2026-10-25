namespace Memoria.FF8.Runtime;

/// <summary>Native 1DCD766 values consumed by 534BC0 and 535320.</summary>
public enum TripleTriadTradeRule
{
    One = 1,
    Diff = 2,
    Direct = 3,
    All = 4
}

public sealed record TripleTriadTradeResult(NativeCardInventory Inventory, IReadOnlyList<int> GainedCardIds, IReadOnlyList<int> LostCardIds, IReadOnlyList<int> OverflowCardIds)
{
    public bool TookOpponentInstance { get; init; }
}

/// <summary>Atomic equivalent of native deck withdrawal (537380) and final acquisition (535AE0).</summary>
public static class TripleTriadTrade
{
    /// <param name="finalOwners">Ten owners, indexed by original side * 5 + original hand slot. Player is 0, opponent is 1; include the unplayed card.</param>
    /// <param name="selectedLosingHandSlots">For One, Diff and All, the distinct slots in the losing original hand which the winner takes, including after Sudden Death.</param>
    public static TripleTriadTradeResult Settle(NativeCardInventory inventory, IReadOnlyList<int> playerHand, IReadOnlyList<int> opponentHand, IReadOnlyList<int> finalOwners, TripleTriadTradeRule rule, int opponentOwner, IReadOnlyList<int>? selectedLosingHandSlots = null)
    {
        return SettleAfterSuddenDeath(inventory, playerHand, opponentHand, playerHand, opponentHand, finalOwners, rule, opponentOwner, selectedLosingHandSlots);
    }

    /// <summary>Sudden Death redistributes play instances; One/Diff/All still trade the original selection, while Direct uses final ownership.</summary>
    public static TripleTriadTradeResult SettleAfterSuddenDeath(NativeCardInventory inventory, IReadOnlyList<int> originalPlayerHand, IReadOnlyList<int> originalOpponentHand, IReadOnlyList<int> playerHand, IReadOnlyList<int> opponentHand, IReadOnlyList<int> finalOwners, TripleTriadTradeRule rule, int opponentOwner, IReadOnlyList<int>? selectedLosingHandSlots = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(playerHand);
        ArgumentNullException.ThrowIfNull(opponentHand);
        ArgumentNullException.ThrowIfNull(finalOwners);
        ArgumentNullException.ThrowIfNull(originalPlayerHand);
        ArgumentNullException.ThrowIfNull(originalOpponentHand);
        inventory.Validate();
        if (!Enum.IsDefined(rule) || (uint)opponentOwner > byte.MaxValue || opponentOwner == 0xF0)
        {
            throw new ArgumentException("Settlement requires a native trade rule and a non-player NPC ownership byte.");
        }
        if (originalPlayerHand.Count != 5 || originalOpponentHand.Count != 5 || playerHand.Count != 5 || opponentHand.Count != 5 || finalOwners.Count != 10 || finalOwners.Any(owner => owner is not (0 or 1)))
        {
            throw new ArgumentException("Settlement requires two original five-card hands and all ten final owners.");
        }
        int[] cards = playerHand.Concat(opponentHand).ToArray();
        if (!cards.Order().SequenceEqual(originalPlayerHand.Concat(originalOpponentHand).Order()))
        {
            throw new ArgumentException("Sudden Death must preserve exactly the original ten card instances.");
        }
        if (cards.Any(card => (uint)card >= NativeCardInventory.CardCount))
        {
            throw new ArgumentException("A trade contains an invalid native card ID.");
        }
        if (cards.Where(card => card >= NativeCardInventory.CommonCardCount).GroupBy(card => card).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A unique rare card cannot occur twice in a match.");
        }
        foreach (IGrouping<int, int> group in originalPlayerHand.GroupBy(card => card))
        {
            if (inventory.AvailableCount(group.Key) < group.Count())
            {
                throw new InvalidOperationException("The original player hand is not available in the saved inventory.");
            }
        }
        foreach (int card in originalOpponentHand.Where(card => card >= NativeCardInventory.CommonCardCount))
        {
            if (inventory.Location(card) != opponentOwner)
            {
                throw new InvalidOperationException("An opponent rare card must actually belong to that saved NPC owner.");
            }
        }
        int playerScore = finalOwners.Count(owner => owner == 0);
        int winner = playerScore > 5 ? 0 : playerScore < 5 ? 1 : -1;
        int count = rule switch
        {
            TripleTriadTradeRule.One when winner >= 0 => 1,
            TripleTriadTradeRule.Diff when winner >= 0 => Math.Min(5, Math.Abs(playerScore * 2 - 10)),
            TripleTriadTradeRule.All when winner >= 0 => 5,
            _ => 0
        };
        int[] selected = selectedLosingHandSlots?.ToArray() ?? [];
        if (rule == TripleTriadTradeRule.All && winner >= 0 && selected.Length == 0)
        {
            selected = [0, 1, 2, 3, 4];
        }
        if (selected.Length != count || selected.Any(slot => (uint)slot >= 5) || selected.Distinct().Count() != selected.Length)
        {
            throw new ArgumentException("The selected losing-hand slots must match the native trade count and must be distinct.");
        }
        bool[] keep = Enumerable.Range(0, 10).Select(slot => slot < 5).ToArray();
        if (rule == TripleTriadTradeRule.Direct)
        {
            keep = finalOwners.Select(owner => owner == 0).ToArray();
        }
        else if (winner >= 0)
        {
            foreach (int slot in selected)
            {
                keep[(winner == 0 ? 5 : 0) + slot] = winner == 0;
            }
        }
        NativeCardInventory next = inventory.DeepCopy();
        foreach (int card in originalPlayerHand)
        {
            next = next.Transfer(card, opponentOwner, out int result);
            if (result != 0)
            {
                throw new InvalidOperationException("The validated original player hand could not be withdrawn.");
            }
        }
        // Native534BC0 builds trade selection from unchanged1DCD76C. Only Direct5358F0 maps final instance ownership back to those original cards.
        int[] settlementCards = rule == TripleTriadTradeRule.Direct ? cards : originalPlayerHand.Concat(originalOpponentHand).ToArray();
        List<int> overflow = [];
        for (int slot = 0; slot < settlementCards.Length; slot++)
        {
            if (keep[slot])
            {
                next = next.Acquire(settlementCards[slot], out int result);
                if (result != 0)
                {
                    overflow.Add(settlementCards[slot]);
                }
            }
        }
        int[] keptCards = Enumerable.Range(0, 10).Where(slot => keep[slot]).Select(slot => settlementCards[slot]).ToArray();
        int[] gains = Difference(keptCards, originalPlayerHand);
        int[] losses = Difference(originalPlayerHand, keptCards);
        return new TripleTriadTradeResult(next, gains, losses, overflow.ToArray()) { TookOpponentInstance = keep.Skip(5).Any(value => value) };
    }

    private static int[] Difference(IEnumerable<int> source, IEnumerable<int> subtract)
    {
        Dictionary<int, int> remaining = subtract.GroupBy(card => card).ToDictionary(group => group.Key, group => group.Count());
        List<int> result = [];
        foreach (int card in source)
        {
            if (remaining.GetValueOrDefault(card) > 0)
            {
                remaining[card]--;
            }
            else
            {
                result.Add(card);
            }
        }
        return result.ToArray();
    }
}
