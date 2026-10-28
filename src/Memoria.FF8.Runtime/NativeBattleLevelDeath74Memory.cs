using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDeathState? LevelDeath74
    {
        get => effectState.LevelDeath74;
        init => effectState = effectState with { LevelDeath74 = value };
    }
}
