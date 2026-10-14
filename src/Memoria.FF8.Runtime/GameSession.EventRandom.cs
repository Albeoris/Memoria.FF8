using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeEventRandomState? EventRandom => State.EventRandom;
    public bool CanStoreEventRandom => true;

    public void SetEventRandom(NativeEventRandomState random)
    {
        ArgumentNullException.ThrowIfNull(random);
        random.Validate();
        if (State.EventRandom?.DrawPoints is not null && random.DrawPoints is null)
        {
            throw new InvalidDataException("Native random publication cannot erase executed draw-point bank history.");
        }
        ValidateEventRandomAliases(State.Variables, State.NativeVariableValues);
        State = State with { EventRandom = random };
    }

    private static void ValidateEventRandomAliases(IReadOnlyDictionary<int, int> variables, IReadOnlyDictionary<int, NativeMemoryValue>? memory)
    {
        if (variables.Keys.Any(address => address < 182 && (long)address + 4 > 116) || memory?.Any(pair => pair.Key < 182 && (long)pair.Key + pair.Value.Width > 116) == true)
        {
            throw new InvalidDataException("Canonical draw-point memory overlaps a separate native event-bank alias.");
        }
    }

    private static void ValidateEventRandomCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.EventRandom?.Validate();
        if (state.EventRandom is not null)
        {
            ValidateEventRandomAliases(state.Variables, state.NativeVariableValues);
        }
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("EventRandom", out JsonElement element))
        {
            throw new InvalidDataException("Saved VM lacks its required native event random process; recreate authentic inputs.");
        }
        NativeEventRandomState? captured = element.ValueKind == JsonValueKind.Null ? null : element.Deserialize<NativeEventRandomState>(JsonOptions);
        captured?.Validate();
        if (state.Scene != SceneKind.WorldMap && !EventRandomEquals(captured, state.EventRandom))
        {
            throw new InvalidDataException("Root and VM native random process owners disagree.");
        }
    }

    private static bool EventRandomEquals(NativeEventRandomState? left, NativeEventRandomState? right)
    {
        return left is null ? right is null : right is not null && left.State == right.State && left.FieldCallbackFlagsCF10 == right.FieldCallbackFlagsCF10 && (left.DrawPoints is null ? right.DrawPoints is null : right.DrawPoints is not null && left.DrawPoints.RegenerationCounter180 == right.DrawPoints.RegenerationCounter180 && left.DrawPoints.Depletion.SequenceEqual(right.DrawPoints.Depletion));
    }
}
