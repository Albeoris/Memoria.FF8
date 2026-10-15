using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeShopHistory? ShopHistory => State.ShopHistory;
    public bool CanStoreShopHistory => true;

    public void InitializeNewGameShopHistory(NativeShopHistory history)
    {
        if (State.ShopHistory is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Native shop history initialization requires the untouched authentic New Game template load.");
        }
        history.Validate();
        State = State with { ShopHistory = history };
    }

    public void SetShopHistory(NativeShopHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        history.Validate();
        if (State.Economy is NativeEconomy economy && economy.InitOutSha256 != history.InitOutSha256)
        {
            throw new InvalidDataException("Native shop history and economy have different New Game provenance.");
        }
        State = State with { ShopHistory = history };
    }

    private static void ValidateShopHistoryCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.ShopHistory?.Validate();
        if (state.ShopHistory is NativeShopHistory history && state.Economy is NativeEconomy economy && history.InitOutSha256 != economy.InitOutSha256)
        {
            throw new InvalidDataException("Saved native shop history and economy have different New Game provenance.");
        }
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (!vm.TryGetProperty("ShopHistory", out JsonElement captured))
        {
            throw new InvalidDataException("Saved VM lacks its native shop history owner; migrate this save explicitly.");
        }
        NativeShopHistory? restored = captured.ValueKind == JsonValueKind.Null ? null : captured.Deserialize<NativeShopHistory>(JsonOptions);
        restored?.Validate();
        if (state.Scene != SceneKind.WorldMap && !JsonElement.DeepEquals(captured, JsonSerializer.SerializeToElement(state.ShopHistory, JsonOptions)))
        {
            throw new InvalidDataException("Root and VM native shop history owners disagree.");
        }
    }
}
