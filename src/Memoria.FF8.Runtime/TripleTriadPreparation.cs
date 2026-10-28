namespace Memoria.FF8.Runtime;

public sealed record TripleTriadPreparedHand(IReadOnlyList<int> Cards, uint RandomState, int RandomDraws);
public sealed record TripleTriadPreparedBoard(IReadOnlyList<TripleTriadElement> Elements, uint RandomState, int RandomDraws);
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

    /// <summary>Native 536250 coin toss. The normal native human side is 1; the pure match's player side is 0.</summary>
    public static TripleTriadStartingSide TossStartingSide(uint randomState)
    {
        TripleTriadRandom random = new TripleTriadRandom(randomState).Draw(out int value);
        return new TripleTriadStartingSide(value & 1, random.State);
    }
}
