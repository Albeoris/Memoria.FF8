namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltingBubble43Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMeltingBubble43State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        Require();
        NativeBattleCuragaState nodes = State.Nodes ?? throw new NotSupportedException("Melting Bubble generic nodes require its executed source pointer.");
        NativeBattlePackedMatrix32 camera = Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Melting Bubble requires its actual shared effect view.");
        return new(Memory.WithMeltingBubble43(State), Effect, nodes with { CopiedCamera = camera.ReadMatrix() });
    }

    public NativeBattleMeltingBubble43Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        Require();
        if (generic.Effect.Binding.Effect != 43 || generic.Effect.Binding.Entry != NativeBattleMeltingBubble43Profile.Entry)
        {
            throw new InvalidDataException("Melting Bubble cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State with { CopiedCamera = null } } };
    }

    private void Require()
    {
        if (Effect.Binding.Effect != 43 || Effect.Binding.Entry != NativeBattleMeltingBubble43Profile.Entry)
        {
            throw new InvalidDataException("Melting Bubble requires its original module binding.");
        }
        State.Validate();
    }
}

public sealed record NativeBattleMeltingBubble43LeafStep(NativeBattleMeltingBubble43Frame Frame, int Result);
public abstract record NativeBattleMeltingBubble43Operation;
public sealed record NativeBattleMeltingBubble43Contact(int Index) : NativeBattleMeltingBubble43Operation;
public sealed record NativeBattleMeltingBubble43Sound(NativeBattleModuleSound Sound) : NativeBattleMeltingBubble43Operation;
public sealed record NativeBattleMeltingBubble43Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleMeltingBubble43Frame, NativeBattleMeltingBubble43Operation, NativeBattleMeltingBubble43Frame> Execute, Func<NativeBattleMeltingBubble43Frame, uint, NativeBattleMeltingBubble43LeafStep> AdvanceGeneric);
