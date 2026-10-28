using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePoisonGas171State? PoisonGas171
    {
        get => effectState.PoisonGas171;
        init => effectState = effectState with { PoisonGas171 = value };
    }
}
