namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGatlingGun150Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleGatlingGun150State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleGatlingGun150PointStep(NativeBattleGatlingGun150Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleGatlingGun150Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleGatlingGun150Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleGatlingGun150Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleGatlingGun150Frame, int, int, int, NativeBattleGatlingGun150PointStep> ReadBone502170, Func<NativeBattleGatlingGun150Frame, int, NativeBattleGatlingGun150PointStep> ReadAnchor571400, Func<NativeBattleGatlingGun150Frame, NativeBattleGatlingGun150Operation, NativeBattleGatlingGun150Frame> Execute);

public abstract record NativeBattleGatlingGun150Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleGatlingGun150Sound(uint Packed, uint Flags, byte Pan) : NativeBattleGatlingGun150Operation;
public sealed record NativeBattleGatlingGun150ContactsAll(int HeaderIndex) : NativeBattleGatlingGun150Operation;
public sealed record NativeBattleGatlingGun150DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleGatlingGun150Operation;
public sealed record NativeBattleGatlingGun150DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleGatlingGun150Operation;
