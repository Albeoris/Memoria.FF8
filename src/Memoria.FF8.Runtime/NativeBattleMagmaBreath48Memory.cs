using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMagmaBreath48State? MagmaBreath48
    {
        get => effectState.MagmaBreath48;
        init => effectState = effectState with { MagmaBreath48 = value };
    }
}
