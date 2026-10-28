using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEnergyBomber217State? EnergyBomber217
    {
        get => effectState.EnergyBomber217;
        init => effectState = effectState with { EnergyBomber217 = value };
    }
}
