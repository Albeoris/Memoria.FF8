using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect174Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect174State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect174Step(NativeBattleEffect174Frame Frame, int Result);
public sealed record NativeBattleEffect174PointStep(NativeBattleEffect174Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect174Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect174Frame, uint> ReadFlags, Func<NativeBattleEffect174Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect174Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect174Frame, int, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect174Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect174Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect174Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect174Frame, short> ReadCameraRoll, Func<NativeBattleEffect174Frame, short> ReadScalarFocal, Func<NativeBattleEffect174Frame, uint, uint> ReadColor, Func<NativeBattleEffect174Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect174PointStep> Point502170, Func<NativeBattleEffect174Frame, int, NativeBattleEffectArena, NativeBattleEffect174PointStep> Anchor571400, Func<NativeBattleEffect174Frame, int, ImmutableArray<NativeBattlePackedMatrix32>> ReadCurrentBoneMatrices32, Func<NativeBattleEffect174Frame, int, ImmutableArray<NativeBattlePackedMatrix32>, NativeBattleEffect174Frame> WriteCurrentBoneMatrices32, Func<NativeBattleEffect174Frame, NativeBattleEffect174Operation, NativeBattleEffect174Frame> Execute);

public abstract record NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174UploadTim505E30(uint SourceAddress) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174SetResourceFlags44(int Actor, ushort Value) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174WriteModelBytes(int Actor, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174StageGroupFlags(int Group, byte AndMask, byte OrMask) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174Stage5713E0(int Amount, uint Color) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174FirstContact506690(int ContactIndex) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174AppendCommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174DrawModelHistory5120(int Actor) : NativeBattleEffect174Operation;
public sealed record NativeBattleEffect174DrawBody136(uint TemporaryAddress, int Actor) : NativeBattleEffect174Operation;
