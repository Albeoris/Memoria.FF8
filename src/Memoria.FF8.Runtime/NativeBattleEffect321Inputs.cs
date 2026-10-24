namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect321Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect321State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect321Step(NativeBattleEffect321Frame Frame, int Result);
public sealed record NativeBattleEffect321PointStep(NativeBattleEffect321Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect321BoolStep(NativeBattleEffect321Frame Frame, bool Active);
public sealed record NativeBattleEffect321Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect321Frame, uint> ReadFlags, Func<NativeBattleEffect321Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect321Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect321Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect321Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect321Frame, short> ReadCameraRoll, Func<NativeBattleEffect321Frame, uint, NativeBattleEffectArena, NativeBattleEffect321PointStep> Anchor571400, Func<NativeBattleEffect321Frame, ushort, NativeBattleEffectArena, NativeBattleEffect321PointStep> TargetCenter5020A0, Func<NativeBattleEffect321Frame, int, NativeBattleEffect321BoolStep> VibrationActive4A2900, Func<NativeBattleEffect321Frame, NativeBattleEffect321Operation, NativeBattleEffect321Frame> Execute);

public abstract record NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321UploadTim505E30(uint SourceAddress) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321WriteModelBytes(uint ModelAddress, int Offset, NativeBattleEffectArena Data, int Length) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321Contact506690(int ContactIndex) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321PcRender47CF50(int Kind, byte Red, byte Green, byte Blue, int Duration) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321PositionalSound5013A0(uint SourceAddress, uint Flags, NativeBattleEffectArena Point8) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321VibrationStart4A29A0(uint SourceAddress, byte Kind, byte Strength, uint DestinationAddress) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321VibrationStop4A2940(int Handle) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321AppendTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect321Operation;
public sealed record NativeBattleEffect321DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect321Operation;
