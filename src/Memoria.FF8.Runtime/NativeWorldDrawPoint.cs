namespace Memoria.FF8.Runtime;

public enum NativeWorldDrawPointPhase
{
    NoDraw,
    Found,
    Depleted,
    Choose,
    StockFull,
    DrawUnavailable,
    Acquiring,
    Acquired
}

public sealed record NativeWorldDrawPoint(byte PointIndex, byte Definition, NativeWorldDrawPointPhase Phase, int Quantity = 0, int Receiver = -1, int RemainingCallbacks = 0)
{
    public const int AcquisitionCallbacks = 25;

    public int MagicId => Definition & 63;

    internal void Validate(WorldState world)
    {
        if (PointIndex < 128 || !Enum.IsDefined(Phase) || world.NativeFootRuntime is not { EntityType: < 10 or 128 } || world.EventUi is not { ScriptMode: 10 })
        {
            throw new InvalidDataException("The saved World Draw request has no active foot interaction owner.");
        }
        bool acquired = Phase is NativeWorldDrawPointPhase.Acquiring or NativeWorldDrawPointPhase.Acquired;
        bool refused = Phase is NativeWorldDrawPointPhase.StockFull or NativeWorldDrawPointPhase.DrawUnavailable;
        if ((Phase == NativeWorldDrawPointPhase.Acquiring ? RemainingCallbacks is < 1 or > AcquisitionCallbacks : RemainingCallbacks != 0) ||
            (acquired ? Receiver < 0 : refused ? Receiver is < 0 or > 2 : Receiver != -1) ||
            (Phase == NativeWorldDrawPointPhase.Depleted ? Quantity != 0 : Quantity <= 0 || MagicId is < 1 or > 56))
        {
            throw new InvalidDataException("Invalid saved World Draw interaction phase.");
        }
    }
}

public sealed partial record NativeWorldSalaryState
{
    private NativeCharacterVitals? DrawPointVitals { get; init; }

    public NativeWorldSalaryState AcquireFromWorldDrawPoint(NativeWorldDrawPoint point, NativeCharacterVitals vitals)
    {
        NativeEventRandomState random = EventRandom ?? throw new NotSupportedException("World Draw requires its saved depletion bank.");
        NativeDrawPointMemory bank = random.DrawPoints ?? throw new NotSupportedException("World Draw requires its saved depletion bank.");
        return this with { DrawPointVitals = vitals, EventRandom = random with { DrawPoints = bank.Deplete(point.PointIndex, point.Definition) } };
    }
}
