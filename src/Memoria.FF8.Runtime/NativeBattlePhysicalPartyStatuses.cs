using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattlePhysicalStatusDword(uint KnownMask, uint KnownBits)
{
    internal NativeBattlePhysicalStatusDword ClearHighBit()
    {
        Validate();
        return new(KnownMask | 0x80000000, KnownBits & 0x7FFFFFFF);
    }

    internal void Validate()
    {
        if ((KnownBits & ~KnownMask) != 0)
        {
            throw new InvalidDataException("The physical battle-status DWORD has bits outside its observed mask.");
        }
    }
}

/// <summary>The three original party B18 status DWORDs, independent of cached presentation/status mirrors.</summary>
internal sealed record NativeBattlePhysicalPartyStatuses(ImmutableArray<NativeBattlePhysicalStatusDword> Rows)
{
    internal static NativeBattlePhysicalPartyStatuses Unknown() => new([new(0, 0), new(0, 0), new(0, 0)]);

    internal void Validate()
    {
        if (Rows.IsDefault || Rows.Length != 3 || Rows.Any(row => row is null))
        {
            throw new InvalidDataException("Native47DF60 requires all three physical party battle-status DWORD owners.");
        }
        foreach (NativeBattlePhysicalStatusDword row in Rows)
        {
            row.Validate();
        }
    }

    internal NativeBattlePhysicalPartyStatuses Observe(int index, uint value)
    {
        Validate();
        return this with { Rows = Rows.SetItem(index, new(uint.MaxValue, value)) };
    }

    internal NativeBattlePhysicalPartyStatuses ClearHighBits()
    {
        Validate();
        return new(Rows.Select(row => row.ClearHighBit()).ToImmutableArray());
    }

    internal bool SameValues(NativeBattlePhysicalPartyStatuses other)
    {
        Validate();
        other.Validate();
        return Rows.SequenceEqual(other.Rows);
    }
}
