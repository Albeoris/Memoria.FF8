using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleJunk44State? Junk44
    {
        get => effectState.Junk44;
        init => effectState = effectState with { Junk44 = value };
    }
}
