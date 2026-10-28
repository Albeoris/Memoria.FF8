using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect9State? Effect9
    {
        get => effectState.Effect9;
        init => effectState = effectState with { Effect9 = value };
    }
}
