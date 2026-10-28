using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBadBreath300State? BadBreath300
    {
        get => effectState.BadBreath300;
        init => effectState = effectState with { BadBreath300 = value };
    }
}
