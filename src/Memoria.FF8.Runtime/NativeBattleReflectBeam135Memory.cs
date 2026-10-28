using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleReflectBeam135State? ReflectBeam135
    {
        get => effectState.ReflectBeam135;
        init => effectState = effectState with { ReflectBeam135 = value };
    }
}
