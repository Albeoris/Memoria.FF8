namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRaldo19Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleRaldo19State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        RequireEffect();
        return new(Memory, Effect, State.Nodes ?? throw new NotSupportedException("Raldo requires its executed entry cursors."));
    }

    public NativeBattleRaldo19Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        RequireEffect();
        if (generic.Effect.Binding.Effect != 19 || generic.Effect.Binding.Entry != NativeBattleRaldo19Profile.Entry)
        {
            throw new InvalidDataException("Raldo cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State } };
    }

    private void RequireEffect()
    {
        if (Effect.Binding.Effect != 19 || Effect.Binding.Entry != NativeBattleRaldo19Profile.Entry)
        {
            throw new InvalidDataException("Raldo requires its original module binding.");
        }
        State.Validate();
    }
}

public abstract record NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19Contact(int Index) : NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19Sound(NativeBattleModuleSound Sound) : NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19GenericTask(uint Address) : NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19BallTask(uint Address) : NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19DebrisTask(uint Address) : NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19OverlayTask(uint Address) : NativeBattleRaldo19Operation;
public sealed record NativeBattleRaldo19LeafStep(NativeBattleRaldo19Frame Frame, int Result);
public sealed record NativeBattleRaldo19Inputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleRaldo19Frame, NativeBattleRaldo19Operation, NativeBattleRaldo19Frame> Execute, Func<NativeBattleRaldo19Frame, NativeBattleRaldo19Operation, NativeBattleRaldo19LeafStep> AdvanceLeaf)
{
    public ushort? CameraPadding18 { get; init; }
    public NativeBattlePackedMatrix32? PackedCamera { get; init; }
}
