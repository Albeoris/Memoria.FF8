using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect210Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect210State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect210Step(NativeBattleEffect210Frame Frame, int Result);
public sealed record NativeBattleEffect210PointStep(NativeBattleEffect210Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect210IoStep(NativeBattleEffect210Frame Frame, bool Busy);
public sealed record NativeBattleEffect210IoCompletionStep(NativeBattleEffect210Frame Frame, bool Complete);
public sealed record NativeBattleEffect210Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect210Frame, uint> ReadFlags, Func<NativeBattleEffect210Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect210Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect210Frame, int, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect210Frame, int, ImmutableArray<NativeBattlePackedMatrix32>> ReadCurrentBoneMatrices32, Func<NativeBattleEffect210Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect210Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect210Frame, short> ReadCameraRoll, Func<NativeBattleEffect210Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect210PointStep> Point502170, Func<NativeBattleEffect210Frame, int, NativeBattleEffectArena, NativeBattleEffect210PointStep> Anchor571400, Func<NativeBattleEffect210Frame, NativeBattleEffect210IoStep> Poll534270, Func<NativeBattleEffect210Frame, int, NativeBattleEffect210IoCompletionStep> Select534300, Func<NativeBattleEffect210Frame, NativeBattleEffect210Operation, NativeBattleEffect210Frame> Execute)
{
    public Func<NativeBattleEffect210Frame, int, NativeBattleModuleResource>? ReadCommonSprite5710B0 { get; init; }
}

public abstract record NativeBattleEffect210Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect210UploadTim505E30(uint SourceAddress) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210WriteModelBytes(int Actor, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210StageFlags(byte AndMask, byte OrMask) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210Stage5713E0(int Amount, uint Color) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210AllContacts506BA0(int FirstContactIndex, int Count) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210IoInitialize534110(uint ControlAddress) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210IoQueue5341D0(short FileId, uint DestinationAddress, byte Type) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210IoAdvance534210 : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210Capture9440(uint PointAddress, NativeBattleEffectArena Bounds12) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210Bounds9800(uint ModelAddress, uint DestinationAddress) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210DrawModel9960(uint ModelAddress, NativeBattleEffectArena Rotation8, NativeBattleEffectArena Pivot8) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210DrawEcho7EE0(int Actor, uint HeaderAddress) : NativeBattleEffect210Operation;
public sealed record NativeBattleEffect210CaptureEcho8200(int Actor, uint HeaderAddress) : NativeBattleEffect210Operation;
