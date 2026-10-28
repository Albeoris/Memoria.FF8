using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAdditional182State? Additional182
    {
        get => effectState.Additional182;
        init => effectState = effectState with { Additional182 = value };
    }
}
