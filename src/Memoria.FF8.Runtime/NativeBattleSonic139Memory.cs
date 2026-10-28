using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSonic139State? Sonic139
    {
        get => effectState.Sonic139;
        init => effectState = effectState with { Sonic139 = value };
    }
}
