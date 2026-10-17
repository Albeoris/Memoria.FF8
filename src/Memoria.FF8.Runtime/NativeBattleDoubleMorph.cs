using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The current163FFD0 mesh;8D57C0 replaces XYZ and preserves every other original byte.</summary>
public sealed record NativeBattleDoubleMorph([property: JsonRequired] ImmutableArray<byte> Bytes)
{
    public void Validate()
    {
        NativeBattleEnhancementMorph.Validate(Bytes);
    }

    public NativeBattleDoubleMorph Morph8D57C0(ImmutableArray<byte> first, ImmutableArray<byte> second, short ratio)
    {
        return new(NativeBattleEnhancementMorph.Morph(Bytes, first, second, ratio));
    }

}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDoubleMorph? DoubleMorph { get; init; }
}
