namespace Memoria.FF8.Runtime;

public abstract record NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307RingSequence(byte Actor, byte Sequence) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307ActorFlags(byte Actor, ushort ClearMask, ushort SetMask) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307ActorWrite8(byte Actor, NativeBattleEffectArena? Rotation8, NativeBattleEffectArena? Position8) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307StageFade(short Depth, uint ColorFlags) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307SpatialSound(uint DescriptorAddress, uint Value, uint Flags, NativeBattleEffectArena Position8) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307Contact(int RelativeContactIndex) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307RestoreTexture(uint Destination, uint CompletionAddress) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307DrawMesh88(uint TemporaryAddress, uint SourceAddress) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, ushort Flags) : NativeBattleRayBomb307Operation;
public sealed record NativeBattleRayBomb307PointStep(NativeBattleRayBomb307Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleRayBomb307AnchorStep(NativeBattleRayBomb307Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleRayBomb307TaskStep(NativeBattleRayBomb307Frame Frame, int Result);
public sealed record NativeBattleRayBomb307Inputs(NativeBattleActionPacketsState Packets, uint GraphicsFlags, Func<int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleRayBomb307Frame, int, int, NativeBattlePackedMatrix32> ReadCurrentBoneMatrix, Func<NativeBattleRayBomb307Frame, ushort, NativeBattleRayBomb307PointStep> ReadTargetCenter5020A0, Func<NativeBattleRayBomb307Frame, int, NativeBattleRayBomb307AnchorStep> ReadAnchor571400, Func<int, NativeBattleEffectArena> ReadRotation8, Func<int, NativeBattleEffectArena> ReadPosition8, Func<NativeBattleRayBomb307Frame, NativeBattleRayBomb307Operation, NativeBattleRayBomb307Frame> Execute)
{
    public Func<NativeBattleRayBomb307Frame, NativeBattlePackedMatrix32>? ReadView { get; init; }
    public Func<NativeBattleRayBomb307Frame, NativeBattleVector>? ReadCameraEye { get; init; }
}
