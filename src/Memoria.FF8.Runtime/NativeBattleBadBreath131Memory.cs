using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBadBreath131State? BadBreath131
    {
        get => effectState.BadBreath131;
        init => effectState = effectState with { BadBreath131 = value };
    }
}
