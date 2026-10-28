using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStorm11State([property: JsonRequired] ImmutableArray<byte>? StreamVertices)
{
    public static NativeBattleStorm11State FromColdProcess() => new(StreamVertices: null);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStorm11State? Storm11
    {
        get => effectState.Storm11;
        init => effectState = effectState with { Storm11 = value };
    }
}
