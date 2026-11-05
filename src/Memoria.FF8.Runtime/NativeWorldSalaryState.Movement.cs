namespace Memoria.FF8.Runtime;

public sealed partial record NativeWorldSalaryState
{
    private NativeMovementProgressResult? MovementProgress { get; init; }
    public bool LearnedAngelo => MovementProgress?.LearnedAngelo == true;

    public NativeWorldSalaryState PrepareMovementProgress(uint units)
    {
        NativeMovementProgressResult progress = NativeMovementProgress.Sample(MovementProgress?.Memory ?? Departure.EventMemory, MovementProgress?.Vitals ?? Departure.CharacterVitals, MovementProgress?.Limits ?? Departure.LimitProgress, FieldParty(), Departure.SavedBattleFlags, units);
        return this with { MovementProgress = progress };
    }

    private byte[]? FieldParty()
    {
        return NativeWorldSaveCompatibility.CurrentFieldParty(Departure);
    }

    private GameState ApplyMovementProgress(GameState candidate)
    {
        if (MovementProgress is not { } progress)
        {
            return candidate;
        }
        NativeEventMemory memory = candidate.EventMemory;
        foreach (int address in new[] { 4, 12, 244 })
        {
            NativeScalarValue value = progress.Memory.Read(address, 4);
            if (value != memory.Read(address, 4))
            {
                memory = memory.Write(address, 4, value);
            }
        }
        return candidate with { EventMemory = memory, CharacterVitals = progress.Vitals, LimitProgress = progress.Limits };
    }
}
