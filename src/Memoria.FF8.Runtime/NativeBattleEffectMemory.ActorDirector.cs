using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector16
    {
        get => effectState.ActorDirector16;
        init => effectState = effectState with { ActorDirector16 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector61
    {
        get => effectState.ActorDirector61;
        init => effectState = effectState with { ActorDirector61 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector16Executable
    {
        get => effectState.ActorDirector16Executable;
        init => effectState = effectState with { ActorDirector16Executable = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector61Executable
    {
        get => effectState.ActorDirector61Executable;
        init => effectState = effectState with { ActorDirector61Executable = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector81
    {
        get => effectState.ActorDirector81;
        init => effectState = effectState with { ActorDirector81 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActorDirectorState? ActorDirector82
    {
        get => effectState.ActorDirector82;
        init => effectState = effectState with { ActorDirector82 = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector81Executable
    {
        get => effectState.ActorDirector81Executable;
        init => effectState = effectState with { ActorDirector81Executable = value };
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? ActorDirector82Executable
    {
        get => effectState.ActorDirector82Executable;
        init => effectState = effectState with { ActorDirector82Executable = value };
    }

    public NativeBattleActorDirectorState RequireActorDirector(ushort effect) => (effect switch { 16 => ActorDirector16, 61 => ActorDirector61, 81 => ActorDirector81, 82 => ActorDirector82, _ => throw new NotSupportedException("The actor director module is unsupported.") }) ?? throw new NotSupportedException("The actor director requires its retained module state.");

    public NativeBattleEffectMemory WithActorDirector(NativeBattleActorDirectorState state)
    {
        state.Validate();
        return state.Effect switch { 16 => this with { ActorDirector16 = state }, 61 => this with { ActorDirector61 = state }, 81 => this with { ActorDirector81 = state }, 82 => this with { ActorDirector82 = state }, _ => throw new NotSupportedException("The actor director module is unsupported.") };
    }
}
