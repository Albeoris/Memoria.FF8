using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGastricJuice128State? GastricJuice128
    {
        get => effectState.GastricJuice128;
        init => effectState = effectState with { GastricJuice128 = value };
    }
}
