using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved BYTE1CFE770, independent of the unrelated higher bytes of the native limit word.</summary>
public sealed record NativeShotAvailabilityState([property: JsonRequired] byte Flags770, [property: JsonRequired] ImmutableArray<byte> AmmoItemIds, [property: JsonRequired] string InitOutSha256, [property: JsonRequired] string KernelSha256)
{
    public void Validate()
    {
        if (AmmoItemIds.IsDefault || AmmoItemIds.Length != 8 || InitOutSha256 is not { Length: 64 } || InitOutSha256.Any(value => !Uri.IsHexDigit(value)) || KernelSha256 is not { Length: 64 } || KernelSha256.Any(value => !Uri.IsHexDigit(value)))
        {
            throw new InvalidDataException("Native shot availability requires its actual saved byte and authenticated eight-row ammo table.");
        }
    }

    /// <summary>Native4C0AD0 marks every present inventory ID within the kernel table's minimum/maximum range.</summary>
    public NativeShotAvailabilityState Discover(NativeEconomy economy)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(economy);
        economy.Validate();
        if (economy.InitOutSha256 != InitOutSha256)
        {
            throw new InvalidDataException("Native menu inventory and shot availability have different New Game producers.");
        }
        int minimum = AmmoItemIds.Min();
        int maximum = AmmoItemIds.Max();
        byte flags = Flags770;
        foreach (NativeItemStock stock in economy.Items)
        {
            //4C0B04/4C0B07 sign-extend both inventory bytes; any nonzero count qualifies.
            int item = unchecked((sbyte)stock.ItemId);
            if (stock.Count != 0 && item >= minimum && item <= maximum)
            {
                //4C0B1D shifts DL using x86's five-bit count, then ORs BYTE770 only.
                flags |= unchecked((byte)(1u << ((item - minimum) & 31)));
            }
        }
        return this with { Flags770 = flags };
    }
}
