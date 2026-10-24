namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect173Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect173State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect173Step(NativeBattleEffect173Frame Frame, int Result);
public sealed record NativeBattleEffect173FactoryStep(NativeBattleEffect173Frame Frame, uint Address);
public sealed record NativeBattleEffect173PointStep(NativeBattleEffect173Frame Frame, NativeBattleEffectArena Output8);
public sealed record NativeBattleEffect173Inputs(NativeBattleSummonColors Colors, Func<NativeBattleEffect173Frame, uint> ReadFlags, Func<NativeBattleEffect173Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect173Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleEffect173Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleEffect173Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleEffect173Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleEffect173Frame, short> ReadCameraRoll, Func<NativeBattleEffect173Frame, int, byte, short, NativeBattleEffectArena, NativeBattleEffect173PointStep> Point502170, Func<NativeBattleEffect173Frame, NativeBattleEffect173Operation, NativeBattleEffect173Frame> Execute);

public abstract record NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173UploadTim505E30(uint SourceAddress) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173TargetCenter5020A0(ushort Selector, uint Destination) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173Stage5713E0(int Amount, uint Color) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173Sound501330(uint SourceAddress, uint Flags, byte Pan) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173AllContacts506BA0(int FirstContactIndex, int Count) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173AppendCommonTextureLoad508630(uint SourceAddress, uint CompletionAddress) : NativeBattleEffect173Operation;
public sealed record NativeBattleEffect173DrawBatch(NativeBattleGfGeometryBatch Batch, uint CursorGlobal, ushort Flags = 0) : NativeBattleEffect173Operation;
