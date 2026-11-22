namespace Memoria.FF7.Runtime;

public enum Ff7SubmarinePhase
{
    Introduction,
    Playing,
    Result,
    Returning,
    Completed
}

public sealed record Ff7SubmarineVesselFrame(int Slot, int X4096, int Y4096, int Z4096, short Heading, short Pitch, int Speed, int Health, int Flags, bool Objective);
public sealed record Ff7SubmarineTorpedoFrame(int Slot, int X4096, int Y4096, int Z4096, int TargetSlot, int Flags);
public sealed record Ff7SubmarineFrame(Ff7SubmarinePhase Phase, Ff7SubmarineVesselFrame Player, IReadOnlyList<Ff7SubmarineVesselFrame> Ships, IReadOnlyList<Ff7SubmarineTorpedoFrame> Torpedoes, int RemainingFrames, int Mission, int Outcome, bool CanAcknowledge);
