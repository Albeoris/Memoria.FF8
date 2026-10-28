using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUltraWaves184State? UltraWaves184
    {
        get => effectState.UltraWaves184;
        init => effectState = effectState with { UltraWaves184 = value };
    }
}
