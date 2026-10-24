using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect207Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect207State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect207Step(NativeBattleEffect207Frame Frame, int Result);
public sealed record NativeBattleEffect207PointStep(NativeBattleEffect207Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect207Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect207Frame, uint> ReadFlags, Func<NativeBattleEffect207Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect207Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect207Frame, int, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect207Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect207Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect207Frame, short> ReadCameraRoll, Func<NativeBattleEffect207Frame, int, NativeBattleEffectArena, NativeBattleEffect207PointStep> Anchor571400, Func<NativeBattleEffect207Frame, NativeBattleEffect207Operation, NativeBattleEffect207Frame> Execute);

public abstract record NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207UploadTim505E30(uint SourceAddress) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207Clear45BEE0(int X, int Y, int Width, int Height, byte Red, byte Green, byte Blue) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207SetOrigin45B5E0(int X, int Y) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207AllContacts506BA0(int FirstContactIndex, int Count) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207AppendCommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect207Operation;
public sealed record NativeBattleEffect207DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect207Operation;
