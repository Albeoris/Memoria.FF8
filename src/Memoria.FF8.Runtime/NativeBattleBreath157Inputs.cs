namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreath157Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBreath157State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleBreath157PointStep(NativeBattleBreath157Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleBreath157Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleBreath157Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleBreath157Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleBreath157Frame, int, NativeBattleActionMatrix> ReadModelWorld, Func<NativeBattleBreath157Frame, int, int, int, NativeBattleBreath157PointStep> ReadBone502170, Func<NativeBattleBreath157Frame, int, NativeBattleBreath157PointStep> ReadAnchor571400, Func<NativeBattleBreath157Frame, NativeBattleBreath157Operation, NativeBattleBreath157Frame> Execute);

public abstract record NativeBattleBreath157Operation;
public sealed record NativeBattleBreath157Sound(uint Value, uint Flags, byte Pan) : NativeBattleBreath157Operation;
public sealed record NativeBattleBreath157ContactFirst(int HeaderIndex) : NativeBattleBreath157Operation;
public sealed record NativeBattleBreath157DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleBreath157Operation;
public sealed record NativeBattleBreath157DrawMesh88(uint TemporaryAddress, uint SourceAddress) : NativeBattleBreath157Operation;
public sealed record NativeBattleBreath157ReleaseCommon(uint SourceAddress, uint TaskAddress) : NativeBattleBreath157Operation;
