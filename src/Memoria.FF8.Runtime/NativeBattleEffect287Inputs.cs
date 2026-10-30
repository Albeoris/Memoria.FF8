namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect287Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect287State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect287Step(NativeBattleEffect287Frame Frame, int Result);
public sealed record NativeBattleEffect287PointStep(NativeBattleEffect287Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect287MacStep(NativeBattleEffect287Frame Frame, NativeBattleEffectArena Output16);
public sealed record NativeBattleEffect287PollStep(NativeBattleEffect287Frame Frame, bool Complete);
public sealed record NativeBattleEffect287RandomStep(NativeBattleEffect287Frame Frame, int Value);
public sealed record NativeBattleEffect287Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect287Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect287Frame, uint, NativeBattleEffectArena> ReadModelArena9C, Func<NativeBattleEffect287Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect287Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect287Frame, short> ReadCameraRoll, Func<NativeBattleEffect287Frame, uint, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleEffect287Frame, NativeBattleEffectArena, NativeBattleEffectArena, NativeBattleEffect287MacStep> PointMac6F3A80, Func<NativeBattleEffect287Frame, uint, int, NativeBattleEffectArena, NativeBattleEffect287PointStep> LocalPoint6F3AD0, Func<NativeBattleEffect287Frame, uint, NativeBattleEffectArena, NativeBattleEffect287PointStep> Anchor571400, Func<NativeBattleEffect287Frame, int, NativeBattleEffect287PollStep> IoSelect5342C0, Func<NativeBattleEffect287Frame, NativeBattleEffect287RandomStep> ProcessRandom7059E0, Func<NativeBattleEffect287Frame, NativeBattleEffect287Operation, NativeBattleEffect287Frame> Execute)
{
    public bool CapturePresentation { get; init; }
}

public abstract record NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287UploadTim505E30(uint SourceAddress) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287ModelFlags(uint ModelAddress, int Width, ushort AndMask, ushort OrMask) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Compose5095B0(uint ModelAddress) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Restore508C90(uint ModelAddress) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Stage5713E0(int Intensity, int Mode) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Contacts506BA0(int FirstContact, int Count) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Initialize534110(uint Address) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Advance534210 : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287Queue5341D0(short FileId, uint Destination, short Type) : NativeBattleEffect287Operation;
public sealed record NativeBattleEffect287DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect287Operation;
