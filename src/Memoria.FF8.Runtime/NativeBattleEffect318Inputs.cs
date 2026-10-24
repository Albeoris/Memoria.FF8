namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect318Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect318State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect318Step(NativeBattleEffect318Frame Frame, int Result);
public sealed record NativeBattleEffect318PointStep(NativeBattleEffect318Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect318PollStep(NativeBattleEffect318Frame Frame, bool Complete);
public sealed record NativeBattleEffect318Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect318Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect318Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect318Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect318Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect318Frame, short> ReadCameraRoll, Func<NativeBattleEffect318Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect318PointStep> Point502170, Func<NativeBattleEffect318Frame, uint, NativeBattleEffectArena, NativeBattleEffect318PointStep> Anchor571400, Func<NativeBattleEffect318Frame, uint, int, uint> ResolveBoneRecord, Func<NativeBattleEffect318Frame, int, NativeBattleEffect318PollStep> IoSelect5342C0, Func<NativeBattleEffect318Frame, NativeBattleEffect318Operation, NativeBattleEffect318Frame> Execute);

public abstract record NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318UploadTim505E30(uint SourceAddress) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318MeshMask(uint ModelAddress, uint AndMask, uint OrMask) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318ScaleBone56BEF0(uint BoneRecordAddress, short XY, short Z) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318Contact506690(int OriginalIndex) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318Initialize534110(uint Address) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318Advance534210 : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318Queue5341D0(short FileId, uint Destination, short Type) : NativeBattleEffect318Operation;
public sealed record NativeBattleEffect318DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect318Operation;
