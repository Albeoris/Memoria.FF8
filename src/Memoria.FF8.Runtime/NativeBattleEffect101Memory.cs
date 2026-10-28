using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect101State? Effect101
    {
        get => effectState.Effect101;
        init => effectState = effectState with { Effect101 = value };
    }
}
