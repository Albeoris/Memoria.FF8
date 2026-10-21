namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDevour62Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleDevour62State State);
public abstract record NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62Contact(int Index) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62Sound(NativeBattleModuleSound Sound) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62UploadTim(uint GlobalAddress) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62MusicGain(int Gain) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62StageFade(short Value, bool ClearRgb) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62WriteActorFlags(int Actor, ushort Flags) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62FadeActors(short Value) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62DrawQuads(uint Address) : NativeBattleDevour62Operation;
public sealed record NativeBattleDevour62Inputs(NativeBattleActionPacketsState Packets, NativeBattlePackedMatrix32 Camera, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleDevour62Frame, NativeBattleDevour62Operation, NativeBattleDevour62Frame> Execute);
