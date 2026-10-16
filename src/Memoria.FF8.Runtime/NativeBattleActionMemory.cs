using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Seven physical actor B98..B9F byte ranges, retained independently of allocation identities.</summary>
public sealed record NativeBattleActionMemory([property: JsonRequired] ImmutableArray<ImmutableArray<byte?>> Rows)
{
    public static NativeBattleActionMemory Unknown() => new(Enumerable.Range(0, 7).Select(_ => ImmutableArray.CreateRange(new byte?[8])).ToImmutableArray());

    public static NativeBattleActionMemory FromLoaderBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 56)
        {
            throw new ArgumentException("Native action memory requires seven complete eight-byte actor ranges.", nameof(bytes));
        }
        byte[] copied = bytes.ToArray();
        return new(Enumerable.Range(0, 7).Select(index => copied.AsSpan(index * 8, 8).ToArray().Select(value => (byte?)value).ToImmutableArray()).ToImmutableArray());
    }

    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length != 7 || Rows.Any(row => row.IsDefault || row.Length != 8 || row[1] > 3 || row[6] > 1))
        {
            throw new InvalidDataException("Native action memory requires seven owned B98..B9F ranges and valid class/healing bytes.");
        }
    }

    public NativeBattleLastAction? Action(int index)
    {
        if (index is < 0 or >= 7)
        {
            return null;
        }
        ImmutableArray<byte?> row = Rows[index];
        return row[0] is null && row[1] is null && row[3] is null && row[4] is null && row[5] is null && row[7] is null ? null : new(row[1], row[5], row[3], row[7], row[4], row[0]);
    }

    public byte? Healing(int index) => index is >= 0 and < 7 ? Rows[index][6] : null;

    public NativeBattleActionMemory WithAction(int index, NativeBattleLastAction? action)
    {
        ValidateIndex(index);
        ImmutableArray<byte?> row = Rows[index].SetItem(0, action?.AttackerIndex).SetItem(1, action?.AttackType).SetItem(3, action?.Command).SetItem(4, action?.ElementMask).SetItem(5, action?.AttackerModelId).SetItem(7, action?.ActionId);
        NativeBattleActionMemory result = this with { Rows = Rows.SetItem(index, row) };
        result.Validate();
        return result;
    }

    public NativeBattleActionMemory WithHealing(int index, byte? healing)
    {
        ValidateIndex(index);
        NativeBattleActionMemory result = this with { Rows = Rows.SetItem(index, Rows[index].SetItem(6, healing)) };
        result.Validate();
        return result;
    }

    public NativeBattleActionMemory InitializeStatistics(int index)
    {
        ValidateIndex(index);
        //48B5F0/48BBD0 clear B98/B99/B9A only; command, element, model, healing and ID retain their bytes.
        return this with { Rows = Rows.SetItem(index, Rows[index].SetItem(0, 0).SetItem(1, 0).SetItem(2, 0)) };
    }

    private static void ValidateIndex(int index)
    {
        if (index is < 0 or >= 7)
        {
            throw new InvalidOperationException("Native action memory write requires a current physical actor slot.");
        }
    }
}

/// <summary>One mutable reference to the immutable physical bank, shared by the combatant projections.</summary>
internal sealed class NativeBattleActionMemoryOwner
{
    public NativeBattleActionMemory State { get; set; } = NativeBattleActionMemory.Unknown();
}
