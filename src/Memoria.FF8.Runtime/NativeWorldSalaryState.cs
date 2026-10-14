namespace Memoria.FF8.Runtime;

/// <summary>Transaction-local owners accumulated over actual world callbacks; never a saved-state or replay shortcut.</summary>
public sealed record NativeWorldSalaryState
{
    private NativeWorldSalaryState(GameState departure)
    {
        Departure = departure;
        SeeD = departure.SeeD;
        Economy = departure.Economy;
        Presentation = departure.SalaryPresentation;
        SoundProcess = departure.GlobalSoundProcess;
        EventRandom = departure.EventRandom;
        VibrationProcess = departure.VibrationProcess;
    }

    public GameState Departure { get; }
    public long Callbacks { get; private init; }
    public uint MovementUnits { get; private init; }
    public NativeSeeDState? SeeD { get; private init; }
    public NativeEconomy? Economy { get; private init; }
    public NativeSalaryPresentation? Presentation { get; private init; }
    public NativeGlobalSoundProcess? SoundProcess { get; private init; }
    public NativeEventRandomState? EventRandom { get; private init; }
    public NativeVibrationProcessState? VibrationProcess { get; private init; }

    public static NativeWorldSalaryState Begin(GameState departure)
    {
        ArgumentNullException.ThrowIfNull(departure);
        return new NativeWorldSalaryState(departure);
    }

    public NativeWorldSalaryState Advance(NativeSalaryResult? result, NativeSalaryPresentation? presentation, NativeGlobalSoundProcess? soundProcess, uint movementUnits, int uiTicks)
    {
        result?.SeeD.Validate();
        result?.Economy?.Validate();
        presentation?.Validate();
        soundProcess?.Validate();
        EventRandom?.Validate();
        NativeVibrationProcessState? vibration = VibrationProcess?.Advance(uiTicks);
        if (movementUnits > 5792 || Presentation is not null && presentation is null || SoundProcess is not null && soundProcess is null || Economy is not null && result is not null && result.Economy is null)
        {
            throw new InvalidDataException("World callback lost a current salary process owner or its native movement bound.");
        }
        return this with { Callbacks = checked(Callbacks + 1), MovementUnits = checked(MovementUnits + movementUnits), SeeD = result?.SeeD ?? SeeD, Economy = result?.Economy ?? Economy, Presentation = presentation, SoundProcess = soundProcess, VibrationProcess = vibration };
    }

    public GameState ApplyTo(GameState candidate, WorldState finalWorld)
    {
        if (Departure.Scene != SceneKind.WorldMap || Departure.World is null || Callbacks != finalWorld.Frame - Departure.World.Frame || MovementUnits > (ulong)Callbacks * 5792 || finalWorld.NativeScriptedRuntime is not null && MovementUnits != 0)
        {
            throw new InvalidDataException("World salary owners do not belong to the actual callback sequence.");
        }
        SeeD?.Validate();
        Economy?.Validate();
        Presentation?.Validate();
        SoundProcess?.Validate();
        EventRandom?.Validate();
        VibrationProcess?.Validate();
        if (Economy is not null && Departure.CharacterVitals is not null && Economy.InitOutSha256 != Departure.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("World salary and character owners have different authentic initialization.");
        }
        return candidate with { SeeD = SeeD, Economy = Economy?.DeepCopy(), SalaryPresentation = Presentation, GlobalSoundProcess = SoundProcess, EventRandom = EventRandom, VibrationProcess = VibrationProcess };
    }

    public NativeWorldSalaryState PrepareDrawPointAging(uint movementUnits)
    {
        NativeEventRandomState random = EventRandom ?? throw new NotSupportedException("Native world6519D0 requires its actual shared random process and saved draw-point bank.");
        return this with { EventRandom = random.AdvanceMovement(movementUnits) };
    }

    public NativeWorldSalaryState PrepareCarVibration(int entityType, NativeVibrationBank bank)
    {
        NativeVibrationProcessState vibration = VibrationProcess ?? throw new NotSupportedException("Native car vibration requires its current process queue and prior graphics-speed latch.");
        return this with { VibrationProcess = vibration.SampleCarStart(entityType, bank) };
    }

    public NativeWorldSalaryState WithGraphicsSpeed(int magnitude)
    {
        NativeVibrationProcessState vibration = VibrationProcess ?? throw new NotSupportedException("Native world graphics magnitude has no current process owner.");
        NativeVibrationProcessState next = vibration with { GraphicsSpeed20409F0 = magnitude };
        next.Validate();
        return this with { VibrationProcess = next };
    }
}
