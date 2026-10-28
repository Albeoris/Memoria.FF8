using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, ImmutableArray<int>>? CinematicBreakupPayload4
    {
        get => effectState.CinematicBreakupPayload4;
        init => effectState = effectState with { CinematicBreakupPayload4 = value };
    }
}
