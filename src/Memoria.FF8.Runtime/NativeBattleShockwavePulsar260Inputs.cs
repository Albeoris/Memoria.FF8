namespace Memoria.FF8.Runtime;

public abstract record NativeBattleShockwavePulsar260Operation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleShockwavePulsar260BindCaster(byte Actor, uint SaveAddress, uint ResourceAddress) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260RingSequence(byte Actor, byte Sequence) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260ActorFlags(byte Actor, ushort ClearMask, ushort SetMask) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260ActorWrite(byte Actor, int Offset, NativeBattleEffectArena Bytes, int Length) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260StageWrite(ushort GroupMask, short Depth, uint ColorFlags) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260StageFlags(ushort GroupMask, byte ClearMask, byte SetMask) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260SoundPlay(uint DescriptorAddress, uint Value, uint Flags, int Volume) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260Flash(byte Red, byte Green, byte Blue, int Factor) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260ContactAll : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260CameraPose(NativeBattleCameraPose Pose) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260CameraFocal(short Value) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260CameraControl : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260CameraRestore : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260AdvanceIo : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260CaptureBounds(byte Actor, uint DestinationAddress) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260DrawBody(byte Actor, NativeBattleEffectArena Rotation8, NativeBattleEffectArena Offset8, short Scale) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260ActNode(uint SourceAddress, NativeBattleGfAnimationNode Node, NativeBattleEffectArena Context20) : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260DrawPuffs : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260DrawSecondaryParticles : NativeBattleShockwavePulsar260Operation;
public sealed record NativeBattleShockwavePulsar260TaskStep(NativeBattleShockwavePulsar260Frame Frame, int Result);
public sealed record NativeBattleShockwavePulsar260AnchorStep(NativeBattleShockwavePulsar260Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleShockwavePulsar260IoStep(NativeBattleShockwavePulsar260Frame Frame, bool Busy);
public sealed record NativeBattleShockwavePulsar260Inputs(NativeBattleActionPacketsState Packets, ushort FormationId, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattleEffectArena> ReadPosition8, Func<int, int?> ReadLinkedActor, Func<NativeBattleShockwavePulsar260Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleShockwavePulsar260Frame, NativeBattleCameraState> ReadCamera, Func<NativeBattleShockwavePulsar260Frame, int, NativeBattleShockwavePulsar260AnchorStep> ReadAnchor571400, Func<NativeBattleShockwavePulsar260Frame, int, NativeBattleShockwavePulsar260IoStep> PollIo, Func<NativeBattleShockwavePulsar260Frame, NativeBattleShockwavePulsar260Operation, NativeBattleShockwavePulsar260Frame> Execute);
