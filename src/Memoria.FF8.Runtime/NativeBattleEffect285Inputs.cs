namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect285Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect285State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect285Step(NativeBattleEffect285Frame Frame, int Result);
public sealed record NativeBattleEffect285PointStep(NativeBattleEffect285Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect285IoCompletionStep(NativeBattleEffect285Frame Frame, bool Complete);
public sealed record NativeBattleEffect285CameraSnapshot(NativeBattleEffectArena Eye8, NativeBattleEffectArena Target8, NativeBattleEffectArena SavedEye8, NativeBattleEffectArena SavedTarget8, short? ScalarFocal, short? SavedScalarFocal, short Roll);
public sealed record NativeBattleEffect285Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect285Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect285Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect285Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect285Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect285Frame, NativeBattleEffect285CameraSnapshot> ReadCamera, Func<NativeBattleEffect285Frame, uint, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect285PointStep> BonePoint67B560, Func<NativeBattleEffect285Frame, int, NativeBattleEffect285IoCompletionStep> Select5342C0, Func<NativeBattleEffect285Frame, NativeBattleEffect285Operation, NativeBattleEffect285Frame> Execute);

public abstract record NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285UploadTim505E30(uint SourceAddress) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285AllContacts506BA0(int FirstContactIndex, int Count) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285CameraForce50A730 : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285CameraScalarFocal(short Value) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285CameraRoll(short Value) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285CameraPoint(bool Eye, NativeBattleEffectArena Point8, bool CopyPadding = false) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285StageFlags(byte AndMask, byte OrMask) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285Stage5713E0(int Amount, uint Color) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285Flash5712C0(int Rise, int Hold, int Fall, short Intensity) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285IoInitialize534110(uint Address) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285IoQueue5341D0(short FileId, uint Destination, short Type) : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285IoAdvance534210 : NativeBattleEffect285Operation;
public sealed record NativeBattleEffect285DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect285Operation;
