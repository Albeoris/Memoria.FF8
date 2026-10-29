namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect166Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect166State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect166Step(NativeBattleEffect166Frame Frame, int Result);
public sealed record NativeBattleEffect166FactoryStep(NativeBattleEffect166Frame Frame, uint Address);
public sealed record NativeBattleEffect166PointStep(NativeBattleEffect166Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect166Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect166Frame, uint> ReadFlags, Func<NativeBattleEffect166Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect166Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect166Frame, int, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix32, Func<NativeBattleEffect166Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect166Frame, int, byte> ReadSequenceHeaderByte0, Func<NativeBattleEffect166Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect166Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect166Frame, NativeBattleEffect166Operation, NativeBattleEffect166Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    public Func<NativeBattleEffect166Frame, int, NativeBattleEffectArena>? ReadModelArena9C { get; init; }
    public Func<NativeBattleEffect166Frame, int, NativeBattleEffectArena, NativeBattleEffect166PointStep>? ReadAnchor571400 { get; init; }
    public Func<NativeBattleEffect166Frame, short>? ReadCameraRoll { get; init; }
}

public abstract record NativeBattleEffect166Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect166UploadTim505E30(uint SourceAddress) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166TargetCenter5020A0(ushort Mask, uint DestinationAddress) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166Flash5712C0(int Rise, int Hold, int Fall, byte Color) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166Stage5713E0(int Intensity, uint Color) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166Sequence505C00(int Actor, byte Sequence) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166Contact506690(int Index) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166CommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect166Operation;
public sealed record NativeBattleEffect166WriteModelBytes(int Actor, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect166Operation;
