using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect308Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect308State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect308Step(NativeBattleEffect308Frame Frame, int Result);
public sealed record NativeBattleEffect308PointStep(NativeBattleEffect308Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect308PollStep(NativeBattleEffect308Frame Frame, bool Busy);
public sealed record NativeBattleEffect308RandomStep(NativeBattleEffect308Frame Frame, int Value);
public sealed record NativeBattleEffect308BodySource(NativeBattlePoseSource PoseSource, NativeBattleDecodedPose DecodedPose, ImmutableArray<NativeBattlePackedMatrix32> CurrentBones, uint GeometryAddress, Func<uint, int, NativeBattleEffectArena> ReadGeometry)
{
    public uint? VertexScratchAddress { get; init; }
    public int? VertexScratchLength { get; init; }
}

public sealed record NativeBattleEffect308Inputs(
    NativeBattleSummonColors Colors,
    Func<NativeBattleEffect308Frame, uint, NativeBattleEffectArena> ReadModelArena9C,
    Func<NativeBattleEffect308Frame, uint, NativeBattleGraphicsModel> ReadModel,
    Func<NativeBattleEffect308Frame, uint, NativeBattleEffect308BodySource> ReadBody,
    Func<NativeBattleEffect308Frame, NativeBattlePackedMatrix32> ReadView,
    Func<NativeBattleEffect308Frame, NativeBattleCameraProjection> ReadProjection,
    Func<NativeBattleEffect308Frame, short> ReadCameraRoll,
    Func<NativeBattleEffect308Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect308PointStep> Point502170,
    Func<NativeBattleEffect308Frame, NativeBattleActionPacketsState> ReadPackets,
    Func<NativeBattleEffect308Frame, int, NativeBattleEffect308PollStep> IoPoll5342C0,
    Func<NativeBattleEffect308Frame, NativeBattleEffect308RandomStep> ProcessRandom7059E0,
    Func<NativeBattleEffect308Frame, NativeBattleEffect308Operation, NativeBattleEffect308Frame> Execute);

public abstract record NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308UploadTim505E30(uint SourceAddress) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308VibrationStart4A29A0(uint SourceAddress, byte Kind, byte Strength, uint DestinationAddress) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308VibrationStop4A2940(int Handle) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308InitializeIo534110(int ArenaOffset) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308QueueFile5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308AdvanceIo534210 : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308Intensity5713E0(int Kind, int Strength) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308Music47E3C0(int Duration) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308White7040B0(int Amount) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308ApplyOffsetPose662C00(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308RestorePose508C90(uint ModelAddress) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308DrawOffsetBody669070(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect308Operation;
public sealed record NativeBattleEffect308SelectFragments667DB0(uint ModelAddress, uint OffsetRowsAddress, ImmutableArray<short> BoneSelectors, short SmokeLimit, short NormalSpeed, short UnusedParameter, short RandomThreshold) : NativeBattleEffect308Operation;
