namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect301Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect301State State)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }

    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect301Step(NativeBattleEffect301Frame Frame, int Result);
public sealed record NativeBattleEffect301PointStep(NativeBattleEffect301Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect301Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect301Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect301Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect301Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect301Frame, short> ReadCameraRoll, Func<NativeBattleEffect301Frame, uint, NativeBattleEffectArena, NativeBattleEffect301PointStep> Anchor571400, Func<NativeBattleEffect301Frame, NativeBattleEffect301Operation, NativeBattleEffect301Frame> Execute);

public abstract record NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301UploadTim505E30(uint SourceAddress) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301ApplyCamera50A730 : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301DrawBody66E300(uint ModelAddress, int Age, int ThresholdY, uint Scratch) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301VibrationStart4A29A0(uint SourceAddress, byte Kind, byte Strength, uint DestinationAddress) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301VibrationStop4A2940(int Handle) : NativeBattleEffect301Operation;
public sealed record NativeBattleEffect301DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect301Operation;
