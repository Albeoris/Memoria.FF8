using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCannonBlow151State? CannonBlow151
    {
        get => effectState.CannonBlow151;
        init => effectState = effectState with { CannonBlow151 = value };
    }
}
