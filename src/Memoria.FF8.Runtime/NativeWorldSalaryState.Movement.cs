using System.Text.Json;

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
        JsonElement? snapshot = Departure.DepartingFieldState ?? Departure.EventState;
        if (snapshot is JsonElement state && state.TryGetProperty("Scene", out JsonElement scene) && scene.TryGetProperty("Party", out JsonElement party))
        {
            byte[] members = party.EnumerateArray().Select(value => value.GetByte()).ToArray();
            if (members.Length != 3)
            {
                throw new InvalidDataException("Native movement training requires three physical field-party slots.");
            }
            return members;
        }
        return Departure.OriginalSave?.FieldParty;
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
