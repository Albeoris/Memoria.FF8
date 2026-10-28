using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMegaSpark279State? MegaSpark279
    {
        get => effectState.MegaSpark279;
        init => effectState = effectState with { MegaSpark279 = value };
    }
}
