using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEerieSoundWave130State? EerieSoundWave130
    {
        get => effectState.EerieSoundWave130;
        init => effectState = effectState with { EerieSoundWave130 = value };
    }
}
