using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleOilShot136State? OilShot136
    {
        get => effectState.OilShot136;
        init => effectState = effectState with { OilShot136 = value };
    }
}
