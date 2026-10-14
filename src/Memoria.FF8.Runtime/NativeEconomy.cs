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
    }

    public NativeEconomy DeepCopy()
    {
        Validate();
        return this with { Items = Items.ToArray() };
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
