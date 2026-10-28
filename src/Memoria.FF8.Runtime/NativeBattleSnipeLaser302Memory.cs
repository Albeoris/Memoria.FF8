using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSnipeLaser302State? SnipeLaser302
    {
        get => effectState.SnipeLaser302;
        init => effectState = effectState with { SnipeLaser302 = value };
    }
}
