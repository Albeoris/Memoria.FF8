using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAcid317State? Acid317
    {
        get => effectState.Acid317;
        init => effectState = effectState with { Acid317 = value };
    }
}
