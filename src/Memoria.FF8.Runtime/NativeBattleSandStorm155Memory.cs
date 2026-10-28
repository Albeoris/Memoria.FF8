using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSandStorm155State? SandStorm155
    {
        get => effectState.SandStorm155;
        init => effectState = effectState with { SandStorm155 = value };
    }
}
