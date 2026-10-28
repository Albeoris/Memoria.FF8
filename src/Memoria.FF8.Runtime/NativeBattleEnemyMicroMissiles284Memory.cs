using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEnemyMicroMissiles284State? EnemyMicroMissiles284
    {
        get => effectState.EnemyMicroMissiles284;
        init => effectState = effectState with { EnemyMicroMissiles284 = value };
    }
}
