namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect227Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect227State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PresentationIdentity { get; init; }
}

public sealed record NativeBattleEffect227Step(NativeBattleEffect227Frame Frame, int Result);
public sealed record NativeBattleEffect227PointStep(NativeBattleEffect227Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect227Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect227Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect227Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect227Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect227Frame, short> ReadCameraRoll, Func<NativeBattleEffect227Frame, uint, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect227PointStep> BonePoint693990, Func<NativeBattleEffect227Frame, NativeBattleEffect227Operation, NativeBattleEffect227Frame> Execute);

public abstract record NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227UploadTim505E30(uint SourceAddress) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227Text4876F0(int Actor, int Text, int Duration) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227VibrationStart4A29A0(uint SourceAddress, byte Kind, byte Strength, uint DestinationAddress) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227VibrationStop4A2940(int Handle) : NativeBattleEffect227Operation;
public sealed record NativeBattleEffect227DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect227Operation;
