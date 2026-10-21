namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePsychoBlast23Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattlePsychoBlast23State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        Require();
        NativeBattleCuragaState nodes = State.Nodes ?? throw new NotSupportedException("Psycho Blast generic nodes require its executed source pointer.");
        NativeBattlePackedMatrix32 camera = Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Psycho Blast requires its actual shared effect view.");
        return new(Memory, Effect, nodes with { CopiedCamera = camera.ReadMatrix() });
    }

    public NativeBattlePsychoBlast23Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        Require();
        if (generic.Effect.Binding.Effect != 23 || generic.Effect.Binding.Entry != NativeBattlePsychoBlast23Profile.Entry)
        {
            throw new InvalidDataException("Psycho Blast cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State with { CopiedCamera = null } } };
    }

    private void Require()
    {
        if (Effect.Binding.Effect != 23 || Effect.Binding.Entry != NativeBattlePsychoBlast23Profile.Entry)
        {
            throw new InvalidDataException("Psycho Blast requires its original module binding.");
        }
        State.Validate();
    }
}

public sealed record NativeBattlePsychoBlast23LeafStep(NativeBattlePsychoBlast23Frame Frame, int Result);
public abstract record NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23Contact(int Index) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23Sound(NativeBattleModuleSound Sound) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23LookBasis(NativeBattleVector Direction, NativeBattleVector Up) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23Rotate(NativeBattleActionMatrix Matrix, char Axis, short Angle, short? Padding18) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23TargetAnchor(int Actor, byte Selector, short Distance) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23ActDraw(uint Address) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23DebrisDraw(uint Address) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23TextureLoad(uint RootTaskAddress, int SourceArenaOffset) : NativeBattlePsychoBlast23Operation;
public sealed record NativeBattlePsychoBlast23Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattlePsychoBlast23Frame, NativeBattlePsychoBlast23Operation, NativeBattlePsychoBlast23Frame> Execute, Func<NativeBattlePsychoBlast23Frame, uint, NativeBattlePsychoBlast23LeafStep> AdvanceGeneric, Func<NativeBattlePsychoBlast23Frame, uint, NativeBattlePsychoBlast23LeafStep> AdvanceOverlay);
