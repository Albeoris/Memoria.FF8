namespace Memoria.FF8.Runtime;

public sealed record NativeBattleOilShot136Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleOilShot136State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleOilShot136AnchorStep(NativeBattleOilShot136Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleOilShot136PointStep(NativeBattleOilShot136Frame Frame, NativeBattleVector Point);
public sealed record NativeBattleOilShot136ActStep(NativeBattleOilShot136Frame Frame, int Remaining);
public sealed record NativeBattleOilShot136Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<NativeBattleOilShot136Frame, BattleModelOwner, NativeBattleOilShot136AnchorStep> ReadAnchor571400, Func<NativeBattleOilShot136Frame, NativeBattleOilShot136PointStep> ReadMouth, Func<BattleModelOwner, short> ReadModelYaw, Func<NativeBattleOilShot136Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleOilShot136Frame, uint, NativeBattleGfAnimationSource, int, int, NativeBattleEffectArena, NativeBattleOilShot136ActStep> AdvanceAct, Func<NativeBattleOilShot136Frame, NativeBattleOilShot136Operation, NativeBattleOilShot136Frame> Execute);

public abstract record NativeBattleOilShot136Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleOilShot136Sound(uint Value, uint Flags, byte Pan) : NativeBattleOilShot136Operation;
public sealed record NativeBattleOilShot136Contact(BattleContactOccurrence Occurrence) : NativeBattleOilShot136Operation;
public sealed record NativeBattleOilShot136Stage(int Depth, uint ColorFlags) : NativeBattleOilShot136Operation;
public sealed record NativeBattleOilShot136DrawCustom92(uint TemporaryAddress) : NativeBattleOilShot136Operation;
public sealed record NativeBattleOilShot136DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleOilShot136Operation;
