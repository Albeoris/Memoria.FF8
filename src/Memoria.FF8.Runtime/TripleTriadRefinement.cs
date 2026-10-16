namespace Memoria.FF8.Runtime;

public sealed record TripleTriadRefinement(int CardId, int RequiredCards, int ItemId, int ItemCount);
public sealed record TripleTriadRefinementResult(NativeCardInventory Cards, NativeEconomy Economy, int CardsConsumed, int ItemsCreated);

/// <summary>Publishes both inventory candidates together only when the complete conversion fits.</summary>
public static class TripleTriadRefinementService
{
    public static bool TryApply(NativeCardInventory cards, NativeEconomy economy, TripleTriadRefinement recipe, out TripleTriadRefinementResult result, int batches = 1)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(recipe);
        cards.Validate();
        economy.Validate();
        if ((uint)recipe.CardId >= NativeCardInventory.CardCount || recipe.RequiredCards is < 1 or > 100 || recipe.ItemId is < 1 or > 198 || recipe.ItemCount is < 1 or > 100 || batches is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(recipe), "Card Mod requires a valid catalog recipe and one to one hundred batches.");
        }
        int required = checked(recipe.RequiredCards * batches);
        int produced = checked(recipe.ItemCount * batches);
        result = new TripleTriadRefinementResult(cards.DeepCopy(), economy.DeepCopy(), 0, 0);
        if (cards.AvailableCount(recipe.CardId) < required)
        {
            return false;
        }
        int slot = Array.FindIndex(economy.Items, item => item.ItemId == recipe.ItemId);
        int currentCount = slot < 0 ? 0 : economy.Items[slot].Count;
        if (currentCount + produced > 100 || slot < 0 && !economy.Items.Any(item => item.ItemId == 0))
        {
            return false;
        }
        NativeEconomy nextEconomy = economy.AddItem(recipe.ItemId, produced, out _);
        NativeCardInventory nextCards = cards.DeepCopy();
        for (int index = 0; index < required; index++)
        {
            nextCards = nextCards.Transfer(recipe.CardId, 0, out int transferResult);
            if (transferResult != 0)
            {
                throw new InvalidDataException("Card Mod input changed after quantity validation.");
            }
        }
        result = new TripleTriadRefinementResult(nextCards, nextEconomy, required, produced);
        return true;
    }
}
