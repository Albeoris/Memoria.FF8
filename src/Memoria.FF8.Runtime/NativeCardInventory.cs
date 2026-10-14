using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native card bytes at 1CFEF38 and obtained-rare-card bits at 1CFEFA6, retained across fields.</summary>
public sealed record NativeCardInventory
{
    public const string InitializationRule = "ff8-en-newgame-56d9f0-card-zero-v1";
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

    public static NativeCardInventory CreateNewGame()
    {
        // 56D9F0 -> 47E410 clears this save region; the subsequent init.out load ends before it.
        return new NativeCardInventory { Version = 1, Rule = InitializationRule, Cards = new byte[CardCount], ObtainedRareCards = new byte[5] };
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
