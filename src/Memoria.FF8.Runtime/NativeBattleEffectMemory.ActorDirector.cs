using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector16 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector61 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector16Executable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector61Executable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector81 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector82 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector81Executable { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector82Executable { get; init; }

    public NativeBattleActorDirectorState RequireActorDirector(ushort effect) => (effect switch { 16 => ActorDirector16, 61 => ActorDirector61, 81 => ActorDirector81, 82 => ActorDirector82, _ => throw new NotSupportedException("The actor director module is unsupported.") }) ?? throw new NotSupportedException("The actor director requires its retained module state.");

    public NativeBattleEffectMemory WithActorDirector(NativeBattleActorDirectorState state)
    {
        state.Validate();
        return state.Effect switch { 16 => this with { ActorDirector16 = state }, 61 => this with { ActorDirector61 = state }, 81 => this with { ActorDirector81 = state }, 82 => this with { ActorDirector82 = state }, _ => throw new NotSupportedException("The actor director module is unsupported.") };
    }
}
