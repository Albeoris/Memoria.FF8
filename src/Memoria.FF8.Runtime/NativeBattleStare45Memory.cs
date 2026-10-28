using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleStare45State? Stare45
    {
        get => effectState.Stare45;
        init => effectState = effectState with { Stare45 = value };
    }
}
