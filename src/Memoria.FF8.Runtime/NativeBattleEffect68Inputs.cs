using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect68Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect68State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect68Step(NativeBattleEffect68Frame Frame, int Result);
public sealed record NativeBattleEffect68PointStep(NativeBattleEffect68Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect68ResultStep(NativeBattleEffect68Frame Frame, byte Count);
public sealed record NativeBattleEffect68CameraSnapshot(NativeBattleEffectArena Eye8, NativeBattleEffectArena Target8, ushort Zoom, short Direction7C, short Direction88, NativeBattleEffectArena SavedEye8, NativeBattleEffectArena SavedTarget8, ushort SavedZoom);

public sealed record NativeBattleEffect68Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect68Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect68Frame, uint> ReadFlags, Func<NativeBattleEffect68Frame, ImmutableArray<NativeBattleEffectArena>> ReadModels, Func<NativeBattleEffect68Frame, int, NativeBattleEffectArena> ReadDescriptor208, Func<NativeBattleEffect68Frame, int, byte> ReadResourceSequence, Func<NativeBattleEffect68Frame, NativeBattleEffect68CameraSnapshot> ReadCamera, Func<NativeBattleEffect68Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect68Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect68Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect68PointStep> Point502170, Func<NativeBattleEffect68Frame, ushort> ReadMappedHeld, Func<NativeBattleEffect68Frame, int, NativeBattleEffect68ResultStep> BuildResultsB67EF0, Func<NativeBattleEffect68Frame, byte, uint> ReadPageB68370, Func<NativeBattleEffect68Frame, NativeBattleEffect68Operation, NativeBattleEffect68Frame> Execute);

public abstract record NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68UploadTim505E30(uint Source) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68Sound501330(uint Source, uint Flags, byte Pan) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68ForceCamera50A730 : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68SetMode4A8480(int Mode) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68Contact506690(int Index) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68WriteModelBytes(int Actor, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68PublishCamera(NativeBattleEffectArena Eye8, NativeBattleEffectArena Target8, ushort Zoom) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68WindowText(int Index, uint Pointer) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68WindowFlags(int Index, byte Flags) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68WindowRect(int Index, NativeBattleScanRect Rect) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68WindowValue(int Index, ushort Value) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68OpenWindow(int Index) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68CloseWindow(int Index) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68UpdateCaster8DC740(uint TaskAddress) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68UpdateTarget8DC610(uint TaskAddress) : NativeBattleEffect68Operation;
public sealed record NativeBattleEffect68UpdateBounds8DC870(uint TaskAddress) : NativeBattleEffect68Operation;
