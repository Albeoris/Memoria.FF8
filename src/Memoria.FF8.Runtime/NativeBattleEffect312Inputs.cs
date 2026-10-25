namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect312Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect312State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    public Func<uint, int, NativeBattleEffectArena>? ReadPhysical { get; init; }
}

public sealed record NativeBattleEffect312Step(NativeBattleEffect312Frame Frame, int Result);
public sealed record NativeBattleEffect312PointStep(NativeBattleEffect312Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect312Inputs(
    NativeBattleSummonColors Colors,
    Func<NativeBattleEffect312Frame, uint> ReadFlags,
    Func<NativeBattleEffect312Frame, NativeBattleActionPacketsState> ReadPackets,
    Func<NativeBattleEffect312Frame, uint, int, NativeBattleEffectArena> ReadPhysical,
    Func<NativeBattleEffect312Frame, uint, NativeBattleEffectArena> ReadModelArena9C,
    Func<NativeBattleEffect312Frame, uint, NativeBattlePackedMatrix32> ReadWorld,
    Func<NativeBattleEffect312Frame, uint, int, NativeBattlePackedMatrix32> ReadBoneMatrix59D3C0,
    Func<NativeBattleEffect312Frame, ushort, NativeBattleEffectArena, NativeBattleEffect312PointStep> TargetCenter5020A0,
    Func<NativeBattleEffect312Frame, uint, NativeBattleEffectArena, NativeBattleEffect312PointStep> Point571400,
    Func<NativeBattleEffect312Frame, uint, byte> ReadCurrentSequenceByte,
    Func<NativeBattleEffect312Frame, NativeBattlePackedMatrix32> ReadView,
    Func<NativeBattleEffect312Frame, NativeBattleCameraProjection> ReadProjection,
    Func<NativeBattleEffect312Frame, short> ReadCameraRoll,
    Func<NativeBattleEffect312Frame, NativeBattleEffect312Operation, NativeBattleEffect312Frame> Execute);

public abstract record NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312UploadTim505E30(uint SourceAddress) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312Flash5712C0(int Rise, int Hold, int Fall, byte Color) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312Stage5713E0(int Intensity, uint Color) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312SetModelAnimation505C00(uint ModelAddress, short Mode) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312Contact506690(uint TargetArgumentAddress) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal = NativeBattleEffect312Profile.CursorGlobal, ushort Flags = 0) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312CommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect312Operation;
public sealed record NativeBattleEffect312CaptureStage59D400(uint TargetAddress) : NativeBattleEffect312Operation;
