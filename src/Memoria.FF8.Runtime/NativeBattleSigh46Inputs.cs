namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSigh46Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleSigh46State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleSigh46PointStep(NativeBattleSigh46Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleSigh46Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleSigh46Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleSigh46Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleSigh46Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleSigh46Frame, int, int, int, NativeBattleSigh46PointStep> ReadBone502170, Func<NativeBattleSigh46Frame, short> ReadCameraRoll, Func<NativeBattleSigh46Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleSigh46Frame, NativeBattleSigh46Operation, NativeBattleSigh46Frame> Execute);

public abstract record NativeBattleSigh46Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleSigh46UpdateCaster8DC740(uint TaskAddress) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46UpdateTargetBounds8DC610_8DC870(uint TaskAddress) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46Sound501330(uint Packed, uint Flags, byte Pan) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46Contact506690(int ContactIndex) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46StageColorDepth(int Group, short Depth) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46StageRgb(int Group, byte R, byte G, byte B) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46CommonTextureLoad508630(uint DestinationAddress, uint CompletionAddress) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46DrawMesh88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleSigh46Operation;
public sealed record NativeBattleSigh46DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleSigh46Operation;
