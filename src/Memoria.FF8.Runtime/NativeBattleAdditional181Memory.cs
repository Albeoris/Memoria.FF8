using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAdditional181State? Additional181
    {
        get => effectState.Additional181;
        init => effectState = effectState with { Additional181 = value };
    }
}
