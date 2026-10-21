using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffect60State? Effect60 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? Effect60PrimaryExecutable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? Effect60SecondaryExecutable { get; init; }

    public NativeBattleEffect60State RequireEffect60()
    {
        return Effect60 ?? throw new NotSupportedException("Effect60 requires its actual retained module memory.");
    }

    public NativeBattleEffectMemory WithEffect60(NativeBattleEffect60State state)
    {
        state.Validate();
        return this with { Effect60 = state };
    }
}
