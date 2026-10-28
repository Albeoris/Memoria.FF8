using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleAcid170State? Acid170
    {
        get => effectState.Acid170;
        init => effectState = effectState with { Acid170 = value };
    }
}
