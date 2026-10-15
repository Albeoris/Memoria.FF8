using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeDiscProcessState? DiscProcess => State.DiscProcess;
    public bool CanStoreDiscProcess => true;

    public void SetDiscProcess(NativeDiscProcessState disc)
    {
        disc.Validate();
        ValidateDiscAliases(State.Variables, State.NativeVariableValues);
        State = State with { DiscProcess = disc };
    }

    private static void ValidateDiscAliases(IReadOnlyDictionary<int, int> variables, IReadOnlyDictionary<int, NativeMemoryValue>? memory)
    {
        if (variables.Keys.Any(address => address <= 204 && (long)address + 4 > 204) || memory?.Any(pair => pair.Key <= 204 && (long)pair.Key + pair.Value.Width > 204) == true)
        {
            throw new InvalidDataException("Native disc BYTE204 overlaps a second memory owner.");
        }
    }

    private static void ValidateDiscCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.DiscProcess?.Validate();
        if (state.DiscProcess is not null)
        {
            ValidateDiscAliases(state.Variables, state.NativeVariableValues);
        }
        if (currentVm is JsonElement vm && (!vm.TryGetProperty("DiscProcess", out JsonElement saved) || !JsonElement.DeepEquals(saved, JsonSerializer.SerializeToElement(state.DiscProcess, JsonOptions))))
        {
            throw new InvalidDataException("Root and VM native disc process owners disagree; recreate authentic history.");
        }
    }
}
