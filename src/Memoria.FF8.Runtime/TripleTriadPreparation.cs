namespace Memoria.FF8.Runtime;

public sealed record TripleTriadPreparedHand(IReadOnlyList<int> Cards, uint RandomState, int RandomDraws);
public sealed record TripleTriadPreparedBoard(IReadOnlyList<TripleTriadElement> Elements, uint RandomState, int RandomDraws);
public sealed record TripleTriadPreparedRewardPool(IReadOnlyList<bool> Participants, uint RandomState, int RandomDraws);
public sealed record TripleTriadPreparedBlocks(IReadOnlyList<int> Cells, uint RandomState, int RandomDraws);
public sealed record TripleTriadStartingSide(int NativeSide, uint RandomState);

/// <summary>Original 534AA0 consumers, with explicit saved RNG input/output and no live inventory mutation.</summary>
public static class TripleTriadPreparation
{
    /// <summary>Native 537110's Random player selection. Reservation is private and must not debit the live inventory twice.</summary>
    public static TripleTriadPreparedHand SelectRandomHand(NativeCardInventory inventory, uint randomState, int handSize = 5)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        inventory.Validate();
        ArgumentOutOfRangeException.ThrowIfLessThan(handSize, 1);
        if (Enumerable.Range(0, 110).Sum(card => Math.Max(0, inventory.AvailableCount(card))) < handSize)
        {
            throw new InvalidOperationException("Random requires enough owned card instances for the selected hand size.");
        }
        NativeCardInventory reserved = inventory.DeepCopy();
        TripleTriadRandom random = new(randomState);
        List<int> hand = [];
        int draws = 0;
        while (hand.Count < handSize)
        {
            random = random.Draw(out int value);
            draws++;
            int card = value % 110;
            if (reserved.AvailableCount(card) <= 0)
            {
                continue;
            }
            hand.Add(card);
            // Native temporarily transfers each chosen instance away before selecting the next.
            reserved = reserved.Transfer(card, 0, out _);
        }
        return new TripleTriadPreparedHand(hand.AsReadOnly(), random.State, draws);
    }

    /// <summary>Native 53A690: one draw per row-major cell, with an additional element draw on a 30% test.</summary>
    public static TripleTriadPreparedBoard GenerateElements(TripleTriadRules rules, uint randomState, int width = 3, int height = 3)
    {
        TripleTriadElement[] elements = new TripleTriadElement[checked(width * height)];
        TripleTriadRandom random = new(randomState);
        int draws = 0;
        if (rules.HasFlag(TripleTriadRules.Elemental))
        {
            for (int cell = 0; cell < elements.Length; cell++)
            {
                random = random.Draw(out int chance);
                draws++;
                if (chance % 100 < 30)
                {
                    random = random.Draw(out int element);
                    draws++;
                    // Native bit indices match Fire, Ice, Thunder, Earth, Poison, Wind, Water, Holy.
                    elements[cell] = (TripleTriadElement)((element & 7) + 1);
                }
            }
        }
        return new TripleTriadPreparedBoard(Array.AsReadOnly(elements), random.State, draws);
    }

    /// <summary>Optional blocked cells are sampled after elements and before the starting-side toss. Zero blocks consume no RNG.</summary>
    public static TripleTriadPreparedBlocks GenerateBlockedCells(int count, uint randomState, int width = 3, int height = 3)
    {
        int area = checked(width * height);
        if (width is < 2 or > 5 || height is < 2 or > 5 || count < 0 || count >= area)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        int[] cells = Enumerable.Range(0, area).ToArray();
        TripleTriadRandom random = new(randomState);
        int draws = 0;
        for (int index = 0; index < count; index++)
        {
            int remaining = area - index;
            int limit = 32768 - 32768 % remaining;
            int value;
            do
            {
                random = random.Draw(out value);
                draws++;
            }
            while (value >= limit);
            int selected = index + value % remaining;
            (cells[index], cells[selected]) = (cells[selected], cells[index]);
        }
        return new TripleTriadPreparedBlocks(cells.Take(count).Order().ToArray(), random.State, draws);
    }

    /// <summary>After the final round, each current side contributes up to half the full board area, including every played card. The mask uses original instance identities.</summary>
    public static TripleTriadPreparedRewardPool GenerateRewardPool(TripleTriadSnapshot match, uint randomState)
    {
        ArgumentNullException.ThrowIfNull(match);
        int handSize = match.InitialHands[0].Length;
        int quota = Math.Min(handSize, (match.Width * match.Height + 1) / 2);
        if (match.Board.Where((_, cell) => !match.BlockedCells.Contains(cell)).Any(card => card is null))
        {
            throw new InvalidOperationException("Finish the board before selecting the reward pool.");
        }
        bool[] participants = new bool[handSize * 2];
        TripleTriadRandom random = new(randomState);
        int draws = 0;
        for (int side = 0; side < 2; side++)
        {
            HashSet<int> selected = match.Board.Where(card => card?.SourcePlayer == side).Select(card => card!.SourceHandIndex).ToHashSet();
            if (selected.Count > quota)
            {
                throw new InvalidDataException("Played cards exceed their current-side reward quota.");
            }
            int[] remaining = Enumerable.Range(0, handSize).Where(slot => !selected.Contains(slot)).ToArray();
            int needed = quota - selected.Count;
            if (needed == remaining.Length)
            {
                selected.UnionWith(remaining);
            }
            else
            {
                for (int index = 0; index < needed; index++)
                {
                    int count = remaining.Length - index;
                    int limit = 32768 - 32768 % count;
                    int value;
                    do
                    {
                        random = random.Draw(out value);
                        draws++;
                    }
                    while (value >= limit);
                    int chosen = index + value % count;
                    (remaining[index], remaining[chosen]) = (remaining[chosen], remaining[index]);
                    selected.Add(remaining[index]);
                }
            }
            foreach (int slot in selected)
            {
                int original = Array.IndexOf(match.InstanceOrder, side * handSize + slot);
                if (original < 0)
                {
                    throw new InvalidDataException("Reward pool lost its original card identity.");
                }
                participants[original] = true;
            }
        }
        return new TripleTriadPreparedRewardPool(Array.AsReadOnly(participants), random.State, draws);
    }

    /// <summary>Native 536250 coin toss. The normal native human side is 1; the pure match's player side is 0.</summary>
    public static TripleTriadStartingSide TossStartingSide(uint randomState)
    {
        TripleTriadRandom random = new TripleTriadRandom(randomState).Draw(out int value);
        return new TripleTriadStartingSide(value & 1, random.State);
    }
}
