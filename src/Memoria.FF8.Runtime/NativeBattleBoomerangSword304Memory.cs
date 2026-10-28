using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBoomerangSword304State? BoomerangSword304
    {
        get => effectState.BoomerangSword304;
        init => effectState = effectState with { BoomerangSword304 = value };
    }
}
