using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBreath157State? Breath157
    {
        get => effectState.Breath157;
        init => effectState = effectState with { Breath157 = value };
    }
}
