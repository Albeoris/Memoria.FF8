using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBreath129State? Breath129
    {
        get => effectState.Breath129;
        init => effectState = effectState with { Breath129 = value };
    }
}
