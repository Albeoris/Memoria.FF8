using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleRaldo19State? Raldo19 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? Raldo19Executable { get; init; }

    public NativeBattleRaldo19State RequireRaldo19()
    {
        return Raldo19 ?? throw new NotSupportedException("Raldo requires its actual retained module memory.");
    }

    public NativeBattleEffectMemory WithRaldo19(NativeBattleRaldo19State state)
    {
        state.Validate();
        return this with { Raldo19 = state };
    }
}
