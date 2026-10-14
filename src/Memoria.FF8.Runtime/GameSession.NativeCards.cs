using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeCardInventory? CardInventory => State.CardInventory;
    public bool CanStoreCardInventory => true;

    public void SetCardInventory(NativeCardInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        State = State with { CardInventory = inventory.DeepCopy() };
    }

    private static void ValidateCardInventoryCheckpoint(GameState state, JsonElement? currentVm)
    {
        if (currentVm is JsonElement vm && vm.TryGetProperty("CardInventory", out JsonElement cardsJson) && cardsJson.ValueKind != JsonValueKind.Null)
        {
            NativeCardInventory cards = cardsJson.Deserialize<NativeCardInventory>(JsonOptions) ?? throw new InvalidDataException("Empty event card inventory.");
            cards.Validate();
            if (state.CardInventory is null || !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(state.CardInventory), JsonSerializer.SerializeToElement(cards)))
            {
                throw new InvalidDataException("Root and event card inventory checkpoints disagree.");
            }
        }
        else if (currentVm is not null && state.CardInventory is not null)
        {
            throw new InvalidDataException("Root card inventory requires matching history in the active event checkpoint; recreate the authentic journal.");
        }
    }
}
