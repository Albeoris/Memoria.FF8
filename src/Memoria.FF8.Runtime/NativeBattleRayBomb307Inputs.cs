namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRayBomb307Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleRayBomb307BindCaster(BattleModelOwner Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307RingSequence(BattleModelOwner Actor, byte Sequence) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307ActorFlags(BattleModelOwner Actor, ushort ClearMask, ushort SetMask) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307ActorWrite8(BattleModelOwner Actor, NativeBattleEffectArena? Rotation8, NativeBattleEffectArena? Position8) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307StageFade(short Depth, uint ColorFlags) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307SpatialSound(uint DescriptorAddress, uint Value, uint Flags, NativeBattleEffectArena Position8) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307Contact(int RelativeContactIndex) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307RestoreTexture(uint Destination, uint CompletionAddress) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307DrawMesh88(uint TemporaryAddress, uint SourceAddress) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, ushort Flags) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307PointStep(NativeBattleRayBomb307Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleRayBomb307AnchorStep(NativeBattleRayBomb307Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleRayBomb307TaskStep(NativeBattleRayBomb307Frame Frame, int Result);
public sealed record NativeBattleRayBomb307Inputs(NativeBattleActionPacketsState Packets, uint GraphicsFlags, Func<BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRayBomb307Frame, BattleModelOwner, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleRayBomb307Frame, BattleModelTargets, NativeBattleRayBomb307PointStep> ReadTargetCenter5020A0, Func<NativeBattleRayBomb307Frame, BattleModelOwner, NativeBattleRayBomb307AnchorStep> ReadAnchor571400, Func<BattleModelOwner, NativeBattleEffectArena> ReadRotation8, Func<BattleModelOwner, NativeBattleEffectArena> ReadPosition8, Func<NativeBattleRayBomb307Frame, NativeBattleRayBomb307Operation, NativeBattleRayBomb307Frame> Execute)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CapturePresentation { get; init; }
    public Func<NativeBattleRayBomb307Frame, NativeBattlePackedMatrix32>? ReadView { get; init; }
    public Func<NativeBattleRayBomb307Frame, NativeBattleVector>? ReadCameraEye { get; init; }
}
