using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDisolvingAcid132State? DisolvingAcid132
    {
        get => effectState.DisolvingAcid132;
        init => effectState = effectState with { DisolvingAcid132 = value };
    }
}
