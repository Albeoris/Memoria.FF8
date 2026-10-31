using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect290Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect290State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect290Step(NativeBattleEffect290Frame Frame, int Result);
public sealed record NativeBattleEffect290PointStep(NativeBattleEffect290Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect290PollStep(NativeBattleEffect290Frame Frame, bool Complete);
public sealed record NativeBattleEffect290RandomStep(NativeBattleEffect290Frame Frame, int Value);
public sealed record NativeBattleEffect290Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect290Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect290Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect290Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect290Frame, short> ReadCameraRoll, Func<NativeBattleEffect290Frame, uint, int, int, NativeBattleEffectArena, NativeBattleEffect290PointStep> Point502170, Func<NativeBattleEffect290Frame, int, NativeBattleEffect290PollStep> IoSelect5342C0, Func<NativeBattleEffect290Frame, NativeBattleEffect290RandomStep> ProcessRandom7059E0, Func<NativeBattleEffect290Frame, NativeBattleEffect290Operation, NativeBattleEffect290Frame> Execute)
{
    public bool CapturePresentation { get; init; }
    public Func<NativeBattleEffect290Frame, uint, NativeBattleEffect290Resources, NativeBattleEffect290Inputs, NativeBattleEffect290Step>? AdvanceLower { get; init; }
    public Func<NativeBattleEffect290Frame, uint, int, NativeBattleEffectArena, NativeBattleEffect290Resources, NativeBattleEffect290Inputs, NativeBattleEffect290Step>? AdvanceProgramme { get; init; }
}

public abstract record NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290UploadTim505E30(uint SourceAddress) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290Music47E3C0(int Duration) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290VibrationStart4A29A0(uint SourceAddress, byte Kind, byte Strength, uint DestinationAddress) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290VibrationStop4A2940(int Handle) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290InitializeIo534110(int ArenaOffset) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290QueueFile5341D0(short FileId, uint DestinationAddress, short Type) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290AdvanceIo534210 : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290InitializeProgramme7016B0(uint WorkspaceAddress, uint SourceAddress, int ClearBytes) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290DrawBody676B40(uint ModelAddress, uint OffsetRowsAddress) : NativeBattleEffect290Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleEffect290SelectFragments675AA0(uint ModelAddress, uint OffsetRowsAddress, ImmutableArray<short> BoneSelectors, short ChunkLimit, short NormalSpeed, short ScaleBase, short RandomThreshold) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290BirthTarget676340(uint ModelAddress, uint OffsetRowsAddress, short Bone, short Fraction, short Scale) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290DrawFragments676EF0(uint MainAddress) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290DrawSmoke677690(uint MainAddress) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290AdvanceFragments6774C0(uint MainAddress) : NativeBattleEffect290Operation;
public sealed record NativeBattleEffect290DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect290Operation;
