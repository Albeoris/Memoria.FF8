using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeWorldEntryMemory? WorldEntryMemory => State.WorldEntryMemory;
    public bool CanStoreWorldEntryMemory => true;

    public void SetWorldEntryMemory(NativeWorldEntryMemory memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        memory.Validate();
        if (State.NativeVariableValues?.Any(pair => NativeWorldEntryMemory.Overlaps(pair.Key, pair.Value.Width)) == true)
        {
            throw new InvalidDataException("Native world-entry publication overlaps uncertain event memory.");
        }
        memory.ProjectVariables(State.Variables);
        State = State with { WorldEntryMemory = memory };
    }

    private static void ValidateWorldEntryMemoryCheckpoint(GameState state, JsonElement? currentVm)
    {
        if (state.SavedWorld is not null && state.WorldEntryMemory is null)
        {
            throw new InvalidDataException("Native saved-world history requires its executed world-entry event bytes.");
        }
        state.WorldEntryMemory?.ValidateVariables(state.Variables, state.NativeVariableValues);
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("WorldEntryMemory", out JsonElement element))
        {
            throw new InvalidDataException("Saved VM lacks its required native world-entry byte owner; recreate authentic inputs.");
        }
        NativeWorldEntryMemory? captured = element.ValueKind == JsonValueKind.Null ? null : element.Deserialize<NativeWorldEntryMemory>(JsonOptions);
        captured?.Validate();
        if (state.Scene != SceneKind.WorldMap && !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(captured), JsonSerializer.SerializeToElement(state.WorldEntryMemory)))
        {
            throw new InvalidDataException("Root and VM native world-entry byte owners disagree.");
        }
    }
}
