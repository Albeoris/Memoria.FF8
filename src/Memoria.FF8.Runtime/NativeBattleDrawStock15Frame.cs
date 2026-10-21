using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawStock15Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleDrawStock15State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        RequireEffect();
        return new(Memory, Effect, State.Nodes);
    }

    public NativeBattleDrawStock15Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        RequireEffect();
        if (generic.Effect.Binding.Effect != 15 || generic.Effect.Binding.Entry != NativeBattleDrawStock15Profile.Entry)
        {
            throw new InvalidDataException("Draw Stock cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State } };
    }

    private void RequireEffect()
    {
        if (Effect.Binding.Effect != 15 || Effect.Binding.Entry != NativeBattleDrawStock15Profile.Entry)
        {
            throw new InvalidDataException("Draw Stock requires its original effect binding.");
        }
        State.Validate();
    }
}

public abstract record NativeBattleDrawStock15Operation;
public sealed record NativeBattleDrawStock15Sound(NativeBattleModuleSound Sound) : NativeBattleDrawStock15Operation;
public sealed record NativeBattleDrawStock15Contact(int Index) : NativeBattleDrawStock15Operation;
public sealed record NativeBattleDrawStock15GenericTask(uint Address) : NativeBattleDrawStock15Operation;
public sealed record NativeBattleDrawStock15Particle(NativeBattleDrawStock15ParticleOperation Operation) : NativeBattleDrawStock15Operation;
public sealed record NativeBattleDrawStock15Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<byte, NativeBattleModuleAnchor> ReadBone241, Func<NativeBattleDrawStock15Frame, NativeBattleDrawStock15Operation, NativeBattleDrawStock15Frame> Execute)
{
    public ushort? CameraPadding18 { get; init; }
    public NativeBattlePackedMatrix32? PackedCamera { get; init; }
}
