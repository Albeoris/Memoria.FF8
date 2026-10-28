using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleElectricDischarge126State? ElectricDischarge126
    {
        get => effectState.ElectricDischarge126;
        init => effectState = effectState with { ElectricDischarge126 = value };
    }
}
