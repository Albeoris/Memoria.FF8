using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGravija214State? Gravija214
    {
        get => effectState.Gravija214;
        init => effectState = effectState with { Gravija214 = value };
    }
}
