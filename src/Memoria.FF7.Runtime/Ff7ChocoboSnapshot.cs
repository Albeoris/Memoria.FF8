using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed record Ff7ChocoboSimulationState(Ff7ChocoboRacerState[] Racers, uint RandomSeed, int FinishTimer, int FirstFinisher, bool SecondFinished, int LastRacer, int[] ConsumedCameraEvents)
{
    internal void Validate(int trackLength)
    {
        if (Racers is not { Length: 6 } || FinishTimer < 0 || FirstFinisher is < -1 or > 5 || LastRacer is < 0 or > 5 || ConsumedCameraEvents is null || ConsumedCameraEvents.Any(index => index < -1 || index > trackLength))
        {
            throw new InvalidDataException("The FFVII Chocobo simulation has invalid native owners.");
        }
        foreach (Ff7ChocoboRacerState racer in Racers)
        {
            if (racer is null || racer.Name is not { Length: 8 } || racer.Path < 0 || racer.Path >= trackLength || racer.NextPath < 0 || racer.NextPath >= trackLength || racer.Guide is < 0 or > 3 || racer.Type is < 0 or > 3 || racer.Animation is < 0 or > 5 || racer.Jockey is < 0 or > 5 || racer.Placement is < 0 or > 6 || racer.Rank is < 0 or > 5)
            {
                throw new InvalidDataException("The FFVII Chocobo racer has invalid native execution indices.");
            }
        }
    }
}

public enum Ff7ChocoboPhase
{
    Preparing,
    Racing,
    Results,
    Completed
}

public sealed record Ff7ChocoboCarryState(Ff7ChocoboSimulationState Simulation, bool ShortCourse, bool RecoverySide, int RecoveryCount)
{
    public Ff7ChocoboCameraSnapshot? Camera { get; init; }
    public void Validate()
    {
        if (Simulation is null || RecoveryCount is < 0 or > 10)
        {
            throw new InvalidDataException("The FFVII Chocobo retained module state is corrupt.");
        }
        Simulation.Validate(ShortCourse ? 945 : 1181);
        Camera?.Validate(ShortCourse ? 945 : 1181);
    }
}

public sealed record Ff7ChocoboSnapshot(FieldHostRequest Request, byte[] Parameters, Ff7ChocoboSimulationState Simulation, long Tick, int Countdown, bool Started, int FadeAlpha, int FadeStep, bool RecoverySide, int RecoveryCount, ushort Held, ushort PreviousInput, ushort Input, Ff7ChocoboPhase Phase, bool AutomaticFadeRequested, int FrameCounter)
{
    public Ff7ChocoboCameraSnapshot? Camera { get; init; }
    public int[]? RiderAnimations { get; init; }
    public Ff7ChocoboSceneryState? Scenery { get; init; }
    public int? DisplayedFadeAlpha { get; init; }
    public void Validate()
    {
        if (Request is null || Request.Command is not FieldMinigameCommand { Game: 1 } || Parameters is not { Length: 29 } || Simulation is null || Tick < 0 || Countdown < 0 || FadeAlpha is < 0 or > 256 || FadeStep is not (-16 or 0 or 16) || RecoveryCount is < 0 or > 10 || Parameters[23] > 3 || FrameCounter < 0 || FrameCounter > Countdown || Tick < Countdown || Started != (Countdown >= 60) || !Enum.IsDefined(Phase))
        {
            throw new InvalidDataException("The FFVII Chocobo snapshot has invalid frame or departure ownership.");
        }
        Simulation.Validate(Parameters[7] == 0 ? 1181 : 945);
        Camera?.Validate(Parameters[7] == 0 ? 1181 : 945);
        if (DisplayedFadeAlpha is < 0 or > 256)
        {
            throw new InvalidDataException("The FFVII Chocobo displayed fade alpha is corrupt.");
        }
        if (RiderAnimations is not null && (RiderAnimations.Length != 6 || RiderAnimations.Any(animation => animation is < 0 or > 5)))
        {
            throw new InvalidDataException("The FFVII Chocobo rider animation bindings are corrupt.");
        }
        if (Phase == Ff7ChocoboPhase.Completed && Simulation.Racers.Any(racer => racer.Placement == 0))
        {
            throw new InvalidDataException("The finished FFVII Chocobo race has unplaced racers.");
        }
    }
}
