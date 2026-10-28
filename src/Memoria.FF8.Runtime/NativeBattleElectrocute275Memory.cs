using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleElectrocute275State? Electrocute275
    {
        get => effectState.Electrocute275;
        init => effectState = effectState with { Electrocute275 = value };
    }
}
