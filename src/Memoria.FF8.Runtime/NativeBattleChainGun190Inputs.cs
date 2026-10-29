namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChainGun190Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleChainGun190State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleChainGun190PointStep(NativeBattleChainGun190Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleChainGun190Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleChainGun190Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleChainGun190Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleChainGun190Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleChainGun190Frame, int, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattleChainGun190Frame, int, int> ReadBoneCount, Func<NativeBattleChainGun190Frame, int, int, int, NativeBattleChainGun190PointStep> ReadBone502170, Func<NativeBattleChainGun190Frame, NativeBattleChainGun190Operation, NativeBattleChainGun190Frame> Execute);

public abstract record NativeBattleChainGun190Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleChainGun190Sound501330(uint Packed, uint Flags, int Pan) : NativeBattleChainGun190Operation;
public sealed record NativeBattleChainGun190Contacts506BA0(int HeaderIndex) : NativeBattleChainGun190Operation;
public sealed record NativeBattleChainGun190DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleChainGun190Operation;
public sealed record NativeBattleChainGun190DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleChainGun190Operation;
