using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleMicroMissiles299State? MicroMissiles299
    {
        get => effectState.MicroMissiles299;
        init => effectState = effectState with { MicroMissiles299 = value };
    }
}
