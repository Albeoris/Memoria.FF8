namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWindBlast29Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleWindBlast29State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        Require();
        NativeBattleCuragaState nodes = State.Nodes ?? throw new NotSupportedException("Wind Blast generic nodes require its executed source pointer.");
        NativeBattlePackedMatrix32 camera = Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Wind Blast requires its actual shared effect view.");
        return new(Memory, Effect, nodes with { CopiedCamera = camera.ReadMatrix() });
    }

    public NativeBattleWindBlast29Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        Require();
        if (generic.Effect.Binding.Effect != 29 || generic.Effect.Binding.Entry != NativeBattleWindBlast29Profile.Entry)
        {
            throw new InvalidDataException("Wind Blast cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State with { CopiedCamera = null } } };
    }

    private void Require()
    {
        if (Effect.Binding.Effect != 29 || Effect.Binding.Entry != NativeBattleWindBlast29Profile.Entry)
        {
            throw new InvalidDataException("Wind Blast requires its original module binding.");
        }
        State.Validate();
    }
}

public sealed record NativeBattleWindBlast29LeafStep(NativeBattleWindBlast29Frame Frame, int Result);
public abstract record NativeBattleWindBlast29Operation;
public sealed record NativeBattleWindBlast29Contact(int Index) : NativeBattleWindBlast29Operation;
public sealed record NativeBattleWindBlast29Sound(NativeBattleModuleSound Sound) : NativeBattleWindBlast29Operation;
public sealed record NativeBattleWindBlast29Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleWindBlast29Frame, NativeBattleWindBlast29Operation, NativeBattleWindBlast29Frame> Execute, Func<NativeBattleWindBlast29Frame, uint, NativeBattleWindBlast29LeafStep> AdvanceGeneric);
