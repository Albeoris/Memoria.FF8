using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native card bytes at 1CFEF38 and obtained-rare-card bits at 1CFEFA6, retained across fields.</summary>
public sealed record NativeCardInventory
{
    public const string InitializationRule = "ff8-en-newgame-56da10-8dff20-cards-v2";
    public const int CardCount = 110;
    public const int CommonCardCount = 77;

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required byte[] Cards { get; init; }
    [JsonRequired]
    public required byte[] ObtainedRareCards { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? TripleTriadRandomState { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TripleTriadStatistics? TripleTriadStatistics { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? CollectionFlags { get; init; }

    public static NativeCardInventory CreateNewGame()
    {
        //56DA10 calls8DFF20 AFTER init.out: clear cards/history, initialize rare owners200..232 and RNG1.
        byte[] cards = new byte[CardCount];
        for (int card = CommonCardCount; card < CardCount; card++)
        {
            cards[card] = (byte)(card + 123);
        }
        return new NativeCardInventory { Version = 1, Rule = InitializationRule, Cards = cards, ObtainedRareCards = new byte[5], TripleTriadRandomState = 1, TripleTriadStatistics = new TripleTriadStatistics(0, 0, 0), CollectionFlags = 0 };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != InitializationRule || Cards is not { Length: CardCount } || ObtainedRareCards is not { Length: 5 })
        {
            throw new InvalidDataException("Native cards require their authentic initialization and complete saved byte arrays.");
        }
    }

    public NativeCardInventory DeepCopy()
    {
        Validate();
        return this with { Cards = Cards.ToArray(), ObtainedRareCards = ObtainedRareCards.ToArray() };
    }

    /// <summary>Native535D00 phase9 publishes counters only for the returned final result; Sudden Death draws loop to phase1 without a counter write. Quit only retains its prompt-advanced RNG.</summary>
    public NativeCardInventory CompleteMatch(int nativeResult, uint randomState)
    {
        if (nativeResult is < -1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeResult));
        }
        if (nativeResult >= 0 && TripleTriadStatistics is null)
        {
            throw new NotSupportedException("Native CARDGAME completion requires saved win/loss/draw WORD counters; migrate this standalone explicitly.");
        }
        return DeepCopy() with { TripleTriadRandomState = randomState, TripleTriadStatistics = TripleTriadStatistics?.Complete(nativeResult) };
    }

    /// <summary>Native4C23D0 ->534A40 card-menu query sets saved1CFEFAB bit1 once all110 cards have ever been acquired.</summary>
    public NativeCardInventory NativeVisitNormalMenu()
    {
        Validate();
        byte flags = CollectionFlags ?? throw new NotSupportedException("Native card-menu completion requires saved collection BYTE1CFEFAB; migrate this standalone explicitly.");
        if ((flags & 1) != 0)
        {
            return DeepCopy();
        }
        bool complete = Enumerable.Range(0, CommonCardCount).All(card => (Cards[card] & 0x80) != 0) && Enumerable.Range(0, CardCount - CommonCardCount).All(rare => (ObtainedRareCards[rare / 8] & 1 << (rare & 7)) != 0);
        return DeepCopy() with { CollectionFlags = complete ? (byte)(flags | 1) : flags };
    }

    /// <summary>Native534A20 returns zero for signed IDs below77, otherwise the saved location BYTE without consulting acquisition history.</summary>
    public int Location(int cardId)
    {
        Validate();
        if (cardId < CommonCardCount)
        {
            return 0;
        }
        if (cardId >= CardCount)
        {
            throw new NotSupportedException("WHERECARD ID has no original card record; native out-of-bounds memory is unsupported.");
        }
        return Cards[cardId];
    }

    /// <summary>Native5347F0 transfers a common card toward/from full DWORD locationF0, or writes a rare location BYTE; acquisition bits are unchanged.</summary>
    public NativeCardInventory Transfer(int cardId, int location, out int result)
    {
        Validate();
        if ((uint)cardId >= CardCount)
        {
            throw new NotSupportedException("SETCARD ID has no original card record; native out-of-bounds memory is unsupported.");
        }
        NativeCardInventory next = DeepCopy();
        result = 0;
        if (cardId >= CommonCardCount)
        {
            next.Cards[cardId] = unchecked((byte)location);
        }
        else if (location == 0xF0)
        {
            if ((next.Cards[cardId] & 0x7F) >= 100)
            {
                result = -1;
            }
            else
            {
                next.Cards[cardId]++;
            }
        }
        else if ((next.Cards[cardId] & 0x7F) == 0)
        {
            result = -1;
        }
        else
        {
            next.Cards[cardId]--;
        }
        return next;
    }

    /// <summary>Total playable instances; the native never-acquired sentinel is not a negative quantity.</summary>
    [JsonIgnore]
    public int TotalAvailableCount => Enumerable.Range(0, CardCount).Sum(card => Math.Max(0, AvailableCount(card)));

    /// <summary>Native534950 distinguishes never acquired, common quantity and current rare-card location.</summary>
    public int AvailableCount(int cardId)
    {
        Validate();
        if ((uint)cardId >= CardCount)
        {
            throw new NotSupportedException("HOWMANYCARD ID has no original card record; native out-of-bounds memory is unsupported.");
        }
        if (cardId < CommonCardCount)
        {
            return (Cards[cardId] & 0x80) != 0 ? Cards[cardId] & 0x7F : -1;
        }
        int rareId = cardId - CommonCardCount;
        if ((ObtainedRareCards[rareId / 8] & 1 << (rareId & 7)) == 0)
        {
            return -1;
        }
        return Cards[cardId] == 0xF0 ? 1 : 0;
    }

    /// <summary>Native 534840 marks acquisition, grants a common card up to 100 or assigns rare location F0, and returns 0/-1.</summary>
    public NativeCardInventory Acquire(int cardId, out int result)
    {
        Validate();
        if ((uint)cardId >= CardCount)
        {
            throw new NotSupportedException("GETCARD ID has no original card record; native out-of-bounds memory is unsupported.");
        }
        NativeCardInventory next = DeepCopy();
        result = 0;
        if (cardId >= CommonCardCount)
        {
            int rareId = cardId - CommonCardCount;
            next.ObtainedRareCards[rareId / 8] |= (byte)(1 << (rareId & 7));
            next.Cards[cardId] = 0xF0;
        }
        else
        {
            next.Cards[cardId] |= 0x80;
            if ((next.Cards[cardId] & 0x7F) >= 100)
            {
                result = -1;
            }
            else
            {
                next.Cards[cardId]++;
            }
        }
        return next;
    }
}
