namespace Memoria.FF7.Runtime;

public readonly record struct Ff7ChocoboPoint(short X, short Y, short Z)
{
    public static implicit operator Ff7ChocoboPoint((short X, short Y, short Z) point) => new(point.X, point.Y, point.Z);
    public static implicit operator (short X, short Y, short Z)(Ff7ChocoboPoint point) => (point.X, point.Y, point.Z);
}

public sealed record Ff7ChocoboRacerState
{
    public short Path { get; set; }
    public short NextPath { get; set; }
    public short Speed { get; set; }
    public short Type { get; set; }
    public short PreviousY { get; set; }
    public Ff7ChocoboPoint Position { get; set; }
    public int RemainderX { get; set; }
    public int RemainderZ { get; set; }
    public Ff7ChocoboPoint Target { get; set; }
    public short Yaw { get; set; }
    public short TargetYaw { get; set; }
    public Ff7ChocoboPoint Origin { get; set; }
    public short Dash { get; set; }
    public short SmoothedLane { get; set; }
    public short Behavior { get; set; }
    public short DesiredSpeed { get; set; }
    public short LaneBias { get; set; }
    public short Guide { get; set; }
    public short EffectiveSpeed { get; set; }
    public short CruiseSpeed { get; set; }
    public short TopSpeed { get; set; }
    public short Acceleration { get; set; }
    public short HandlingStep { get; set; }
    public short SpeedResponse { get; set; }
    public short Automatic { get; set; }
    public short Obedience { get; set; }
    public short Handling { get; set; }
    public short LaneStep { get; set; }
    public int Stamina { get; set; }
    public int MaxStamina { get; set; }
    public short Placement { get; set; }
    public short AnimationCounter { get; set; }
    public short SecondaryAnimationCounter { get; set; }
    public short AnimationStep { get; set; }
    public short PreviousAnimation { get; set; }
    public short Lane { get; set; }
    public short Finished { get; set; }
    public short Animation { get; set; }
    public short Rank { get; set; }
    public short Stagnation { get; set; }
    public short TerrainMask { get; set; }
    public byte[] Name { get; set; } = new byte[8];
    public short Model { get; set; }
    public short Jockey { get; set; }
    public short SoundCounter { get; set; }
    public short OriginalLane { get; set; }
    public short LastSprint { get; set; }
    public short CanSprint { get; set; }
    public short UnusedAbility { get; set; }
    public short UnusedFlag { get; set; }
    public short EarlySprint { get; set; }
    public short FixedSpeed { get; set; }
}
