using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleOilBlast137State? OilBlast137
    {
        get => effectState.OilBlast137;
        init => effectState = effectState with { OilBlast137 = value };
    }
}
