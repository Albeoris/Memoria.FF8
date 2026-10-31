using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect324Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect324State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public sealed record NativeBattleEffect324Step(NativeBattleEffect324Frame Frame, int Result);
public sealed record NativeBattleEffect324PointStep(NativeBattleEffect324Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect324BoolStep(NativeBattleEffect324Frame Frame, bool Result);
public sealed record NativeBattleEffect324Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect324Frame, uint> ReadFlags, Func<NativeBattleEffect324Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect324Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect324Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect324Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect324Frame, short> ReadCameraRoll, Func<NativeBattleEffect324Frame, uint, short, short, NativeBattleEffectArena, NativeBattleEffect324PointStep> Point502170, Func<NativeBattleEffect324Frame, uint, ImmutableArray<NativeBattlePackedMatrix32>> ReadCurrentBoneMatrices32, Func<NativeBattleEffect324Frame, uint, uint> ReadColor, Func<NativeBattleEffect324Frame, int, NativeBattleEffect324BoolStep> VibrationQuery4A2900, Func<NativeBattleEffect324Frame, NativeBattleEffect324Operation, NativeBattleEffect324Frame> Execute);

public abstract record NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324UploadTim505E30(uint SourceAddress) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324Sound5013A0(uint SourceAddress, uint Flags, NativeBattleEffectArena Point8) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324Contact506690(int OriginalIndex) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324VibrationStart4A29A0(uint SourceAddress, byte Kind, byte Strength, uint DestinationAddress) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324VibrationStop4A2940(int Handle) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324WriteCurrentBoneMatrices32(uint ModelAddress, ImmutableArray<NativeBattlePackedMatrix32> Matrices) : NativeBattleEffect324Operation;
public sealed record NativeBattleEffect324DrawModel5977D0(uint ModelAddress) : NativeBattleEffect324Operation;
