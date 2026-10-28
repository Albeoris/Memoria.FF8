using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSaliva138State? Saliva138
    {
        get => effectState.Saliva138;
        init => effectState = effectState with { Saliva138 = value };
    }
}
