using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeItemStock([property: JsonRequired] byte ItemId, [property: JsonRequired] byte Count);

/// <summary>Saved item pairs and live gil globals, plus the distinct native event-bank gil words.</summary>
public sealed record NativeEconomy
{
    public const string InitializationRule = "ff8-en-56d9f0-56da10-52ca90-economy-v1";
    public const int ItemSlotCount = 198;
    public const uint GilCeiling = 99999999;

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required string InitOutSha256 { get; init; }
    [JsonRequired]
    public required NativeItemStock[] Items { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte[]? ItemOrder { get; init; }
    [JsonRequired]
    public required uint Gil { get; init; }
    [JsonRequired]
    public required uint PastGil { get; init; }
    [JsonRequired]
    public required uint EventBankGil72 { get; init; }
    [JsonRequired]
    public required uint EventBankPastGil76 { get; init; }

    public void Validate()
    {
        if (Version != 1 || Rule != InitializationRule || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(character => !Uri.IsHexDigit(character)) || Items is not { Length: ItemSlotCount } || Items.Any(item => item is null))
        {
            throw new InvalidDataException("Native economy requires complete current saved pairs and its executed initialization provenance.");
        }
        if (ItemOrder is { } order && (order.Length != 32 || order.Any(rank => rank >= 32) || order.Distinct().Count() != 32))
        {
            throw new InvalidDataException("Native battle-item order requires its complete saved thirty-two-rank permutation.");
        }
    }

    public NativeEconomy DeepCopy()
    {
        Validate();
        return this with { Items = Items.ToArray(), ItemOrder = ItemOrder?.ToArray() };
    }

    /// <summary>4AD0C0 assigns an unowned battle item the first unused saved display rank.</summary>
    public NativeEconomy UpdateItemOrder(int itemId)
    {
        Validate();
        if (itemId >= 33)
        {
            return this;
        }
        if (itemId <= 0)
        {
            throw new NotSupportedException("Native battle-item ordering would address outside its retained item1..32 permutation.");
        }
        //The original scan returns on an owned matching row, discarding every prior mask read.
        if (Items.Any(item => item.ItemId == itemId && item.Count != 0))
        {
            return this;
        }
        byte[] order = ItemOrder?.ToArray() ?? throw new NotSupportedException("Native battle-item ordering requires its actual saved permutation; migrate this save explicitly before a reached order write.");
        uint used = 0;
        foreach (NativeItemStock item in Items)
        {
            if (item.Count == 0 || item.ItemId == 0 || item.ItemId is >= 33 and < 128)
            {
                continue;
            }
            if (item.ItemId >= 128)
            {
                //4AD0DA sign-extends IDs; those mask reads precede1CFE77C and require separate saved owners.
                throw new NotSupportedException("Native battle-item ordering reached a signed inventory ID whose mask source lies before the retained permutation.");
            }
            used |= 1u << order[item.ItemId - 1];
        }
        int rank = Enumerable.Range(0, 32).First(candidate => (used & 1u << candidate) == 0);
        int displaced = Array.IndexOf(order, (byte)rank);
        (order[itemId - 1], order[displaced]) = (order[displaced], order[itemId - 1]);
        return this with { ItemOrder = order };
    }

    /// <summary>Compacts the saved inventory in ascending item ID order using the last occupied count for each ID.</summary>
    public NativeEconomy SortItems()
    {
        Validate();
        if (Items.Any(item => item.ItemId >= 202 && item.Count != 0))
        {
            throw new NotSupportedException("Item sorting requires IDs within the native quantity lookup.");
        }
        byte[] counts = new byte[202];
        foreach (NativeItemStock item in Items)
        {
            if (item.ItemId != 0 && item.Count != 0)
            {
                counts[item.ItemId] = item.Count;
            }
        }
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), ItemSlotCount).ToArray();
        int index = 0;
        for (int id = 1; id <= ItemSlotCount; id++)
        {
            if (counts[id] != 0)
            {
                items[index++] = new NativeItemStock((byte)id, counts[id]);
            }
        }
        return this with { Items = items, ItemOrder = ItemOrder?.ToArray() };
    }

    public NativeEconomy AddItem(int itemId, int quantity, out int nativeResult)
    {
        NativeEconomy next = DeepCopy();
        nativeResult = 0;
        if (itemId == 0)
        {
            return next;
        }
        int index = Array.FindIndex(next.Items, item => item.ItemId == itemId);
        if (index < 0)
        {
            index = Array.FindIndex(next.Items, item => item.ItemId == 0);
            if (index < 0)
            {
                nativeResult = 1;
                return next;
            }
        }
        byte sum = unchecked((byte)(next.Items[index].Count + quantity));
        nativeResult = sum >= 100 ? 1 : 0;
        next.Items[index] = new NativeItemStock(unchecked((byte)itemId), sum >= 100 ? (byte)100 : sum);
        return next;
    }

    public NativeEconomy AddGil(int quantity, bool past)
    {
        Validate();
        uint sum = unchecked((past ? PastGil : Gil) + (uint)quantity);
        uint value = Math.Min(sum, GilCeiling);
        return past ? this with { PastGil = value, EventBankPastGil76 = value } : this with { Gil = value, EventBankGil72 = value };
    }

    public NativeEconomy CopyGilToEventBank()
    {
        Validate();
        return this with { EventBankGil72 = Gil, EventBankPastGil76 = PastGil };
    }

    public int ReadEventBank(int address, int width)
    {
        ValidateBankRange(address, width);
        ulong bits = EventBankGil72 | (ulong)EventBankPastGil76 << 32;
        ulong mask = (1UL << (8 * width)) - 1;
        return unchecked((int)((bits >> (8 * (address - 72))) & mask));
    }

    public NativeEconomy WriteEventBank(int address, int width, int value)
    {
        ValidateBankRange(address, width);
        int shift = 8 * (address - 72);
        ulong mask = ((1UL << (8 * width)) - 1) << shift;
        ulong bits = EventBankGil72 | (ulong)EventBankPastGil76 << 32;
        bits = (bits & ~mask) | ((ulong)unchecked((uint)value) << shift & mask);
        return this with { EventBankGil72 = unchecked((uint)bits), EventBankPastGil76 = (uint)(bits >> 32) };
    }

    private void ValidateBankRange(int address, int width)
    {
        Validate();
        if (width is not (1 or 2 or 4) || address < 72 || (long)address + width > 80)
        {
            throw new NotSupportedException("Native economy projection cannot consume surrounding unknown event-bank bytes.");
        }
    }
}
