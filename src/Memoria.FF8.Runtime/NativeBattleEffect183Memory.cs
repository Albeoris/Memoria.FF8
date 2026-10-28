using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect183State? Effect183
    {
        get => effectState.Effect183;
        init => effectState = effectState with { Effect183 = value };
    }
}
