namespace Memoria.FF8.Runtime;

public sealed record NativeBattleResonance49Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleResonance49State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleResonance49PointStep(NativeBattleResonance49Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleResonance49Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleResonance49Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleResonance49Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleResonance49Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleResonance49Frame, int, int, int, NativeBattleResonance49PointStep> ReadBone502170, Func<NativeBattleResonance49Frame, int, NativeBattleEffectArena> ReadEnvironment92, Func<NativeBattleResonance49Frame, byte> CurrentPrimitiveBufferByte, Func<NativeBattleResonance49Frame, NativeBattleResonance49Operation, NativeBattleResonance49Frame> Execute);

public abstract record NativeBattleResonance49Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleResonance49UpdateCaster8DC740(uint TaskAddress) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49UpdateTargetBounds8DC610_8DC870(uint TaskAddress) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49SelectSequence505CE0(int Actor, int Sequence) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49Contact506690(int ContactIndex) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49CommonTextureLoad508630(uint DestinationAddress, uint CompletionAddress) : NativeBattleResonance49Operation;
public sealed record NativeBattleResonance49DrawMesh104(uint TemporaryAddress, uint SourceAddress, uint VertexAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleResonance49Operation;
