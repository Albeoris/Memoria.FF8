namespace Memoria.FF8.Runtime;

/// <summary>Transaction-local owners accumulated over actual world callbacks.</summary>
public sealed partial record NativeWorldSalaryState
{
    private NativeWorldSalaryState(GameState departure)
    {
        Departure = departure;
        WorldEntryMemory = departure.WorldEntryMemory;
        SeeD = departure.SeeD;
        Economy = departure.Economy;
        Presentation = departure.SalaryPresentation;
        SoundProcess = departure.GlobalSoundProcess;
        EventRandom = departure.EventRandom;
        RandomEncounters = departure.RandomEncounters;
        VibrationProcess = departure.VibrationProcess;
        FuelDistance112 = departure.SavedWorld?.FuelDistance112;
        SavedWorldEvents = departure.SavedWorld;
        EventUi = departure.World?.EventUi;
        InputHistory = departure.WorldProcess?.InputHistory;
        CarCameraCooldown = departure.World?.NativeCarRuntime is null ? null : departure.WorldProcess?.CameraCooldown ?? throw new NotSupportedException("Native car callbacks require the actual shared camera cooldown owner.");
    }

    public GameState Departure { get; }
    public long Callbacks { get; private init; }
    public uint MovementUnits { get; private init; }
    public NativeSeeDState? SeeD { get; private init; }
    public NativeEconomy? Economy { get; private init; }
    public NativeSalaryPresentation? Presentation { get; private init; }
    public NativeGlobalSoundProcess? SoundProcess { get; private init; }
    public NativeEventRandomState? EventRandom { get; private init; }
    public RandomEncounterSessionState? RandomEncounters { get; init; }
    public NativeVibrationProcessState? VibrationProcess { get; private init; }
    public uint? FuelDistance112 { get; private init; }
    public NativeSavedWorldState? SavedWorldEvents { get; private init; }
    public NativeWorldEventUi? EventUi { get; private init; }
    public int? CarCameraCooldown { get; private init; }
    public NativeWorldInputHistory? InputHistory { get; private init; }

    public static NativeWorldSalaryState Begin(GameState departure)
    {
        ArgumentNullException.ThrowIfNull(departure);
        return new NativeWorldSalaryState(departure);
    }

    public NativeWorldSalaryState BeginWorldEventCallback()
    {
        return this with { EventUi = EventUi?.BeginCallback() };
    }

    public NativeWorldSalaryState WithWorldInputSample(uint buttons)
    {
        return this with { InputHistory = NativeWorldInputHistory.Sample(InputHistory, buttons) };
    }

    public NativeWorldSalaryState WithWorldScriptMode(byte mode)
    {
        return this with { EventUi = RequireWorldEventUi().WithScriptMode(mode) };
    }

    private NativeWorldEventUi RequireWorldEventUi()
    {
        NativeWorldEventUi ui = EventUi ?? throw new NotSupportedException("Native world UI commands require authenticated script-mode and window ownership; an omitted historical owner needs an explicit migration.");
        ui.Validate();
        return ui;
    }

    public NativeWorldSalaryState Advance(NativeSalaryResult? result, NativeSalaryPresentation? presentation, NativeGlobalSoundProcess? soundProcess, uint movementUnits, int uiTicks)
    {
        result?.SeeD.Validate();
        result?.Economy?.Validate();
        presentation?.Validate();
        soundProcess?.Validate();
        EventRandom?.Validate();
        RandomEncounters?.Validate();
        NativeVibrationProcessState? vibration = VibrationProcess?.Advance(uiTicks);
        if (movementUnits > 5792 || Presentation is not null && presentation is null || SoundProcess is not null && soundProcess is null || Economy is not null && result is not null && result.Economy is null)
        {
            throw new InvalidDataException("World callback lost a current salary process owner or its native movement bound.");
        }
        return this with { Callbacks = checked(Callbacks + 1), MovementUnits = checked(MovementUnits + movementUnits), SeeD = result?.SeeD ?? SeeD, Economy = result?.Economy ?? Economy, Presentation = presentation, SoundProcess = soundProcess, VibrationProcess = vibration };
    }

    public GameState ApplyTo(GameState candidate, WorldState finalWorld)
    {
        if (Departure.Scene != SceneKind.WorldMap || Departure.World is null || Callbacks != finalWorld.Frame - Departure.World.Frame || MovementUnits > (ulong)Callbacks * 5792 || (finalWorld.NativeScriptedRuntime is not null || finalWorld.NativeTrainRuntime is not null) && MovementUnits != 0)
        {
            throw new InvalidDataException("World salary owners do not belong to the actual callback sequence.");
        }
        return ApplyOwnersTo(candidate);
    }

    private GameState ApplyOwnersTo(GameState candidate)
    {
        SeeD?.Validate();
        Economy?.Validate();
        Presentation?.Validate();
        SoundProcess?.Validate();
        EventRandom?.Validate();
        VibrationProcess?.Validate();
        EventUi?.Validate();
        InputHistory?.Validate();
        if (Economy is not null && Departure.CharacterVitals is not null && Economy.InitOutSha256 != Departure.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("World salary and character owners have different authentic initialization.");
        }
        WorldEntryMemory?.ValidateEventMemory(candidate.EventMemory);
        NativeWorldProcess? process = candidate.WorldProcess;
        if (InputHistory is not null)
        {
            process = (process ?? throw new NotSupportedException("World input publication requires its persistent process owner.")) with { InputHistory = InputHistory };
        }
        if (CarCameraCooldown is int cooldown)
        {
            process = (process ?? throw new NotSupportedException("Car camera publication requires its persistent process owner.")) with { CameraCooldown = cooldown };
        }
        return candidate with { World = candidate.World is { } world ? world with { EventUi = EventUi } : candidate.World, SeeD = SeeD, Economy = Economy?.DeepCopy(), SalaryPresentation = Presentation, GlobalSoundProcess = SoundProcess, EventRandom = EventRandom, RandomEncounters = RandomEncounters, VibrationProcess = VibrationProcess, WorldEntryMemory = WorldEntryMemory, SavedWorld = ApplySavedWorldOwners(candidate.SavedWorld), WorldProcess = process };
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

    public NativeWorldSalaryState PrepareCarFuel(byte entityType, byte groundType, int rawMagnitude)
    {
        if (entityType is not (32 or 33) || rawMagnitude is < 0 or > 64)
        {
            throw new NotSupportedException("Native car fuel requires the supported entity and actual raw car velocity magnitude.");
        }
        if (entityType == 33)
        {
            return this;
        }
        uint distance = FuelDistance112 ?? throw new NotSupportedException("Native car fuel requires its executed saved-world DWORD112 owner.");
        NativeEconomy economy = Economy ?? throw new NotSupportedException("Native car fuel requires its canonical original inventory pairs.");
        economy.Validate();
        //54FDA0 tests the old unsigned DWORD before adding this callback's raw
        //velocity. The threshold UI/consumption branch is a distinct boundary.
        if (distance >= 0x20000)
        {
            throw new NotSupportedException("Native car fuel consumption reached its unimplemented original fuel-message process.");
        }
        if (!economy.Items.Any(item => item.ItemId == 162 && item.Count != 0))
        {
            return this;
        }
        int increment = groundType == 28 ? rawMagnitude >> 1 : groundType is 27 or 8 ? 2 * rawMagnitude : rawMagnitude;
        return this with { FuelDistance112 = unchecked(distance + (uint)increment) };
    }

    public NativeWorldSalaryState AdvanceCarCameraCooldown(bool throttle, bool steering)
    {
        int cooldown = CarCameraCooldown ?? throw new NotSupportedException("Native car camera update requires its retained shared process cooldown.");
        //557A90's no-collision car pitch stays at the executed loader baseline.
        //Actual throttle BYTE11 and steering BYTE13 still change this DWORD.
        return this with { CarCameraCooldown = unchecked(cooldown - (throttle ? 2 : 0) - (steering ? 10 : 0)) };
    }
}
