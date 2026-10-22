namespace Memoria.FF8.Runtime;

public sealed record NativeBattleReflectBeam135Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleReflectBeam135State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleReflectBeam135AnchorStep(NativeBattleReflectBeam135Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleReflectBeam135PointStep(NativeBattleReflectBeam135Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleReflectBeam135ActStep(NativeBattleReflectBeam135Frame Frame, int Remaining);
public sealed record NativeBattleReflectBeam135Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleReflectBeam135Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleReflectBeam135Frame, int, NativeBattleReflectBeam135AnchorStep> ReadAnchor, Func<NativeBattleReflectBeam135Frame, int, short, int, NativeBattleReflectBeam135PointStep> ReadBone, Func<NativeBattleReflectBeam135Frame, int, short, NativeBattlePackedMatrix32> ReadCurrentPublishedBone32, Func<NativeBattleReflectBeam135Frame, uint, NativeBattleGfAnimationSource, int, int, NativeBattleEffectArena, NativeBattleReflectBeam135ActStep> AdvanceAct, Func<NativeBattleReflectBeam135Frame, NativeBattleReflectBeam135Operation, NativeBattleReflectBeam135Frame> Execute);

public abstract record NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135Sound(uint Value, uint Flags, byte Pan) : NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135Contact(int Index) : NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135Stage(short Depth, uint ColorFlags) : NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135ComposeCaster(byte Actor) : NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135RestoreCaster(byte Actor) : NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135DrawStatic88(uint TemporaryAddress, uint SourceAddress) : NativeBattleReflectBeam135Operation;
public sealed record NativeBattleReflectBeam135DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleReflectBeam135Operation;
