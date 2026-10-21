using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect20State? Effect20 { get; init; }

    public NativeBattleEffect20State RequireEffect20()
    {
        return Effect20 ?? throw new NotSupportedException("Effect20 requires its actual retained module state.");
    }

    public NativeBattleEffectMemory WithEffect20(NativeBattleEffect20State state)
    {
        state.Validate();
        return this with { Effect20 = state };
    }
}
