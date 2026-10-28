using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect298State? Effect298
    {
        get => effectState.Effect298;
        init => effectState = effectState with { Effect298 = value };
    }
}
