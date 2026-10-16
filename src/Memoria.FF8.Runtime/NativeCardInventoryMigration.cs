namespace Memoria.FF8.Runtime;

/// <summary>Explicit, pure migration from the superseded pre-initializer card-save contract. Never invoked by loading a save.</summary>
public static class NativeCardInventoryMigration
{
    public const string LegacyInitializationRule = "ff8-en-newgame-56d9f0-card-zero-v1";

    /// <summary>Repairs only named unacquired zero-location rare IDs; all other saved card bytes and acquisition bits are preserved.</summary>
    public static NativeCardInventory MigrateLegacyInitialization(NativeCardInventory original, IReadOnlyList<int> rareIdsToInitialize, uint randomState, TripleTriadStatistics statistics, byte collectionFlags)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(rareIdsToInitialize);
        ArgumentNullException.ThrowIfNull(statistics);
        if (original.Version != 1 || original.Rule != LegacyInitializationRule || original.Cards is not { Length: NativeCardInventory.CardCount } || original.ObtainedRareCards is not { Length: 5 })
        {
            throw new InvalidDataException("This explicit migration requires the complete superseded card inventory contract.");
        }
        if (rareIdsToInitialize.Distinct().Count() != rareIdsToInitialize.Count || rareIdsToInitialize.Any(id => id is < NativeCardInventory.CommonCardCount or >= NativeCardInventory.CardCount))
        {
            throw new ArgumentException("Name each intentionally repaired rare card ID77..109 exactly once.", nameof(rareIdsToInitialize));
        }
        byte[] cards = original.Cards.ToArray();
        foreach (int id in rareIdsToInitialize)
        {
            int rare = id - NativeCardInventory.CommonCardCount;
            if (cards[id] != 0 || (original.ObtainedRareCards[rare / 8] & 1 << (rare & 7)) != 0)
            {
                throw new InvalidDataException("A migration must not overwrite an acquired rare card or an existing nonzero location.");
            }
            cards[id] = (byte)(id + 123);
        }
        NativeCardInventory migrated = original with
        {
            Rule = NativeCardInventory.InitializationRule,
            Cards = cards,
            ObtainedRareCards = original.ObtainedRareCards.ToArray(),
            TripleTriadRandomState = randomState,
            TripleTriadStatistics = statistics,
            CollectionFlags = collectionFlags
        };
        migrated.Validate();
        return migrated;
    }
}
