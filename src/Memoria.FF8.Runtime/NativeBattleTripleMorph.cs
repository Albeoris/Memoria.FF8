using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Triple's current1627970 mesh, retained independently from Double's163FFD0.</summary>
public sealed record NativeBattleTripleMorph([property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        NativeBattleEnhancementMorph.Validate(Bytes);
    }

    public NativeBattleTripleMorph Morph89E6E0(ImmutableArray<byte> first, ImmutableArray<byte> second, short ratio)
    {
        return new(NativeBattleEnhancementMorph.Morph(Bytes, first, second, ratio));
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleTripleMorph? TripleMorph
    {
        get => effectState.TripleMorph;
        init => effectState = effectState with { TripleMorph = value };
    }
}
