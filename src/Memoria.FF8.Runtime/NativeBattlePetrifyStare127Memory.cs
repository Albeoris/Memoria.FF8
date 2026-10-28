using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePetrifyStare127State? PetrifyStare127
    {
        get => effectState.PetrifyStare127;
        init => effectState = effectState with { PetrifyStare127 = value };
    }
}
