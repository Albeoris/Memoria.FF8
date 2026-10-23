namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWildCannonBlow156Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleWildCannonBlow156State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleWildCannonBlow156PointStep(NativeBattleWildCannonBlow156Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleWildCannonBlow156Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleWildCannonBlow156Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleWildCannonBlow156Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleWildCannonBlow156Frame, int, NativeBattleActionMatrix> ReadModelWorld, Func<NativeBattleWildCannonBlow156Frame, int, int, int, NativeBattleWildCannonBlow156PointStep> ReadBone502170, Func<NativeBattleWildCannonBlow156Frame, ushort, NativeBattleWildCannonBlow156PointStep> ReadTargetCenter, Func<NativeBattleWildCannonBlow156Frame, NativeBattleWildCannonBlow156Operation, NativeBattleWildCannonBlow156Frame> Execute);

public abstract record NativeBattleWildCannonBlow156Operation;
public sealed record NativeBattleWildCannonBlow156Sound(uint Value, uint Flags, byte Pan) : NativeBattleWildCannonBlow156Operation;
public sealed record NativeBattleWildCannonBlow156ContactsAll(int HeaderIndex) : NativeBattleWildCannonBlow156Operation;
public sealed record NativeBattleWildCannonBlow156DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleWildCannonBlow156Operation;
public sealed record NativeBattleWildCannonBlow156DrawMesh88(uint TemporaryAddress, uint SourceAddress) : NativeBattleWildCannonBlow156Operation;
public sealed record NativeBattleWildCannonBlow156ReleaseCommon(uint SourceAddress, uint TaskAddress) : NativeBattleWildCannonBlow156Operation;
public sealed record NativeBattleWildCannonBlow156AdjustShake(short Delta) : NativeBattleWildCannonBlow156Operation;
