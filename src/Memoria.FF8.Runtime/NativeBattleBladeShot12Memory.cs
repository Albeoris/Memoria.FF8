using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBladeShot12State? BladeShot12
    {
        get => effectState.BladeShot12;
        init => effectState = effectState with { BladeShot12 = value };
    }
}
