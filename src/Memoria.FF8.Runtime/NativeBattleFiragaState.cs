using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFiragaState([property: JsonRequired] ImmutableArray<NativeBattleModuleAnchor?> Positions)
{
    public static NativeBattleFiragaState FromUnknown() => new(Enumerable.Repeat<NativeBattleModuleAnchor?>(null, 3).ToImmutableArray());

    public void Validate()
    {
        if (Positions.IsDefault || Positions.Length != 3)
        {
            throw new InvalidDataException("Native Firaga requires its three retained8-byte position/alive cells.");
        }
    }

    public NativeBattleModuleAnchor Read(int index)
    {
        ValidateIndex(index);
        return Positions[index] ?? throw new NotSupportedException("Native Firaga's requested position/alive cell has no observed writer.");
    }

    public NativeBattleFiragaState Write(int index, NativeBattleModuleAnchor position)
    {
        ArgumentNullException.ThrowIfNull(position);
        ValidateIndex(index);
        return this with { Positions = Positions.SetItem(index, position) };
    }

    private void ValidateIndex(int index)
    {
        Validate();
        if ((uint)index >= 3)
        {
            throw new NotSupportedException("Native Firaga's position cache would alias its adjacent arena owner beyond three headers.");
        }
    }
}
