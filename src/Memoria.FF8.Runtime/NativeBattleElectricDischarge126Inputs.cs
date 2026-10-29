namespace Memoria.FF8.Runtime;

public sealed record NativeBattleElectricDischarge126Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleElectricDischarge126State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
    public uint? BoneDirectionWord { get; init; }
}

public sealed record NativeBattleElectricDischarge126PointStep(NativeBattleElectricDischarge126Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleElectricDischarge126AnchorStep(NativeBattleElectricDischarge126Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleElectricDischarge126TaskStep(NativeBattleElectricDischarge126Frame Frame, int Result);
public abstract record NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126LoadView : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126ComposeCaster(byte Actor) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126RestoreCaster(byte Actor) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126ActorFlags(byte Actor, ushort ClearMask, ushort SetMask) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126BlendActorColor(byte Actor, uint FirstColor, uint SecondColor, short FirstFactor, short SecondFactor) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126Stage(short Depth, uint ColorFlags) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126SpatialSound(uint Value, int Volume, NativeBattleModuleAnchor Position) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126Flash(int Argument0, int Argument1, int Argument2, int Argument3) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126ContactAll(int First, int Count) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126ProjectWorld(uint HeadAddress, short Count) : NativeBattleElectricDischarge126Operation;
public sealed record NativeBattleElectricDischarge126DrawWorld(uint HeadAddress, short Count, int TextureVOffset) : NativeBattleElectricDischarge126Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleElectricDischarge126DrawBoneTrail(NativeBattleResourceRowReference Binding, uint HeadAddress, short Count) : NativeBattleElectricDischarge126Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleElectricDischarge126DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase) : NativeBattleElectricDischarge126Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}

public sealed record NativeBattleElectricDischarge126Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, int> ReadBoneCount, Func<int, NativeBattleResourceRowReference> ReadBindingReference, Func<NativeBattleElectricDischarge126Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleElectricDischarge126Frame, int, NativeBattleElectricDischarge126AnchorStep> ReadAnchor571400, Func<NativeBattleElectricDischarge126Frame, int, short, short, NativeBattleElectricDischarge126PointStep> ReadBone502170, Func<NativeBattleElectricDischarge126Frame, uint, NativeBattleEffectArena, NativeBattleElectricDischarge126TaskStep> AdvanceAct, Func<NativeBattleElectricDischarge126Frame, NativeBattleElectricDischarge126Operation, NativeBattleElectricDischarge126Frame> Execute);
