namespace Memoria.FF8.Runtime;

public abstract record NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275BindCaster(BattleModelOwner Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275RingSequence(BattleModelOwner Actor, byte Sequence) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275LoadView : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275ComposeCaster(BattleModelOwner Actor) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275RestoreCaster(BattleModelOwner Actor) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275ActorFlags(BattleModelOwner Actor, ushort ClearMask, ushort SetMask) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275BlendActorColor(BattleModelOwner Actor, uint FirstColor, uint SecondColor, short FirstFactor, short SecondFactor) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275Stage(short Depth, uint ColorFlags) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275SpatialSound(uint Value, int Volume, NativeBattleModuleAnchor Position) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275Flash(int Argument0, int Argument1, int Argument2, int Argument3) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275ContactAll : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275ProjectWorld(uint HeadAddress, short Count) : NativeBattleElectrocute275Operation;
public sealed record NativeBattleElectrocute275DrawWorld(uint HeadAddress, short Count, int TextureVOffset) : NativeBattleElectrocute275Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleElectrocute275DrawBoneTrail(uint BindingAddress, uint HeadAddress, short Count) : NativeBattleElectrocute275Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleElectrocute275DrawSprite180(uint TemporaryAddress, uint SourceAddress, ushort Phase, byte DepthShift = 2) : NativeBattleElectrocute275Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleElectrocute275PointStep(NativeBattleElectrocute275Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleElectrocute275AnchorStep(NativeBattleElectrocute275Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleElectrocute275TaskStep(NativeBattleElectrocute275Frame Frame, int Result);
public sealed record NativeBattleElectrocute275Inputs(NativeBattleActionPacketsState Packets, Func<BattleModelOwner, NativeBattleGraphicsModel> ReadModel, Func<BattleModelOwner, int> ReadBoneCount, Func<BattleModelOwner, uint> ReadBindingAddress, Func<NativeBattleElectrocute275Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleElectrocute275Frame, BattleModelOwner, NativeBattleElectrocute275AnchorStep> ReadAnchor571400, Func<NativeBattleElectrocute275Frame, BattleModelOwner, short, short, NativeBattleElectrocute275PointStep> ReadBone502170, Func<NativeBattleElectrocute275Frame, uint, NativeBattleElectrocute275TaskStep> AdvanceAct, Func<NativeBattleElectrocute275Frame, NativeBattleElectrocute275Operation, NativeBattleElectrocute275Frame> Execute)
{
    public NativeBattleSummonColors? Colors { get; init; }
}
