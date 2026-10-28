using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSigh46State? Sigh46
    {
        get => effectState.Sigh46;
        init => effectState = effectState with { Sigh46 = value };
    }
}
