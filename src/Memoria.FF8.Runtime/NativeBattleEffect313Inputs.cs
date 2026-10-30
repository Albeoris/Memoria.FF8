namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect313Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect313State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect313Step(NativeBattleEffect313Frame Frame, int Result);
public sealed record NativeBattleEffect313PointStep(NativeBattleEffect313Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect313Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect313Frame, uint> ReadFlags, Func<NativeBattleEffect313Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect313Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect313Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect313Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect313Frame, short> ReadCameraRoll, Func<NativeBattleEffect313Frame, NativeBattleEffectArena> ReadCameraEye8, Func<NativeBattleEffect313Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect313PointStep> Point502170, Func<NativeBattleEffect313Frame, uint, NativeBattleEffectArena, NativeBattleEffect313PointStep> Anchor571400, Func<NativeBattleEffect313Frame, ushort, NativeBattleEffectArena, NativeBattleEffect313PointStep> TargetCenter5020A0, Func<NativeBattleEffect313Frame, uint, byte> ReadSequenceHeaderByte0, Func<NativeBattleEffect313Frame, NativeBattleEffect313Operation, NativeBattleEffect313Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313UploadTim505E30(uint SourceAddress) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313SelectSequence505C00(uint ModelAddress, byte Sequence) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313Contact506690(int HeaderRelativeIndex) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313Stage5713E0(int Intensity, int Mode) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313AppendTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect313Operation;
public sealed record NativeBattleEffect313DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect313Operation;
