using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? Rush91MorphScratch
    {
        get => effectState.Rush91MorphScratch;
        init => effectState = effectState with { Rush91MorphScratch = value };
    }
}
