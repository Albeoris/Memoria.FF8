namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect60Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect60State State)
{
    public NativeBattleCuragaFrame ToGeneric()
    {
        Require();
        NativeBattleCuragaState nodes = State.Nodes ?? throw new NotSupportedException("Effect60 generic nodes require its executed source pointer.");
        NativeBattlePackedMatrix32 camera = Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Effect60 requires its actual shared effect view.");
        return new(Memory, Effect, nodes with { CopiedCamera = camera.ReadMatrix() });
    }

    public NativeBattleEffect60Frame WithGeneric(NativeBattleCuragaFrame generic)
    {
        Require();
        if (generic.Effect.Binding.Effect != 60 || generic.Effect.Binding.Entry != NativeBattleEffect60Profile.Entry)
        {
            throw new InvalidDataException("Effect60 cannot publish another module's generic frame.");
        }
        return this with { Memory = generic.Memory, Effect = generic.Effect, State = State with { Nodes = generic.State with { CopiedCamera = null } } };
    }

    private void Require()
    {
        if (Effect.Binding.Effect != 60 || Effect.Binding.Entry != NativeBattleEffect60Profile.Entry)
        {
            throw new InvalidDataException("Effect60 requires its original module binding.");
        }
        State.Validate();
    }
}

public sealed record NativeBattleEffect60LeafStep(NativeBattleEffect60Frame Frame, int Result);
public abstract record NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60Contact(int Index) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60Sound(NativeBattleModuleSound Sound) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60Rotate(NativeBattleActionMatrix Matrix, char Axis, short Angle, short? Padding18) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60TargetAnchor(uint Address, int Actor, byte Selector, short Distance, int Destination) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60SetYaw(int Actor, short Value) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60SetPackedPosition(int Actor, NativeBattleEffectArena Packed8) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60RestoreRotation(int Actor, NativeBattleEffectArena Packed8) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60StageInitialize : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60StageFade(short Value) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60ActDraw(uint Address) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60DebrisDraw(uint Address) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60TextureLoad(uint RootTaskAddress, int SourceArenaOffset) : NativeBattleEffect60Operation;
public sealed record NativeBattleEffect60Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleEffect60Frame, NativeBattleEffect60Operation, NativeBattleEffect60Frame> Execute, Func<NativeBattleEffect60Frame, uint, NativeBattleEffect60LeafStep> AdvanceGeneric)
{
    public required Func<int, NativeBattleEffectArena> ReadRotation8 { get; init; }
    public required Func<int, NativeBattleEffectArena> ReadPackedPosition8 { get; init; }
    public required Func<int, NativeBattlePackedMatrix32> ReadWorld32 { get; init; }
}
