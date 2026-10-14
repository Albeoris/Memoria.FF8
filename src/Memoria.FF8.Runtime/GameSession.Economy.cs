using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeEconomy? Economy => State.Economy;
    public bool CanStoreEconomy => true;

    public void InitializeNewGameEconomy(NativeEconomy economy)
    {
        if (State.Economy is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.ReplayJournal is not { EntryCount: 0, IsComplete: true } || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Economy initialization requires the untouched authentic New Game before its first engine transaction.");
        }
        SetEconomy(economy);
    }

    public void SetEconomy(NativeEconomy economy)
    {
        ArgumentNullException.ThrowIfNull(economy);
        NativeEconomy next = economy.DeepCopy();
        ValidateEconomyAliases(State.Variables, State.NativeVariableValues);
        if (State.CharacterVitals is not null && next.InitOutSha256 != State.CharacterVitals.InitOutSha256)
        {
            throw new InvalidDataException("Economy and character records have different executed New Game templates.");
        }
        State = State with { Economy = next };
    }

    private static void ValidateEconomyAliases(IReadOnlyDictionary<int, int> variables, IReadOnlyDictionary<int, NativeMemoryValue>? nativeVariables)
    {
        if (variables.Keys.Any(address => address < 80 && (long)address + 4 > 72) || nativeVariables?.Any(pair => pair.Key < 80 && (long)pair.Key + pair.Value.Width > 72) == true)
        {
            throw new InvalidDataException("Canonical native gil bank words overlap a separately stored VM memory alias.");
        }
    }

    private static void ValidateEconomyCheckpoint(GameState state, JsonElement? currentVm)
    {
        if (state.Economy is not null)
        {
            state.Economy.Validate();
            ValidateEconomyAliases(state.Variables, state.NativeVariableValues);
        }
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("Economy", out JsonElement json))
        {
            throw new InvalidDataException("Saved VM lacks its current native economy shape; recreate the authentic journal.");
        }
        NativeEconomy? saved = json.ValueKind == JsonValueKind.Null ? null : json.Deserialize<NativeEconomy>(JsonOptions) ?? throw new InvalidDataException("Empty event economy.");
        saved?.Validate();
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.Economy), JsonSerializer.SerializeToElement(saved)))
        {
            throw new InvalidDataException("Root and event economy checkpoints disagree.");
        }
    }
}
