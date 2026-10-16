using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeCardInventoryMigrationTests
{
    [Fact]
    public void ExplicitMigrationRepairsOnlyNamedRareIdsAndPreservesSourceAndCurrentCardHistory()
    {
        NativeCardInventory original = Legacy();
        original.Cards[0] = 0x82;
        original.Cards[78] = 0xF0;
        original.ObtainedRareCards[0] = 2;
        original.Cards[109] = 42;
        string before = JsonSerializer.Serialize(original);
        TripleTriadStatistics counters = new(7, 8, 9);
        NativeCardInventory migrated = NativeCardInventoryMigration.MigrateLegacyInitialization(original, [77], 1234, counters, 3);
        Assert.Equal(NativeCardInventory.InitializationRule, migrated.Rule);
        Assert.Equal(200, migrated.Location(77));
        Assert.Equal(0, migrated.Location(79));
        Assert.Equal(42, migrated.Location(109));
        Assert.Equal(2, migrated.AvailableCount(0));
        Assert.Equal(1, migrated.AvailableCount(78));
        Assert.Equal(1234u, migrated.TripleTriadRandomState);
        Assert.Equal(counters, migrated.TripleTriadStatistics);
        Assert.Equal((byte)3, migrated.CollectionFlags);
        Assert.Equal(before, JsonSerializer.Serialize(original));
        migrated.Cards[0] = 0;
        migrated.ObtainedRareCards[0] = 0;
        Assert.Equal(0x82, original.Cards[0]);
        Assert.Equal(2, original.ObtainedRareCards[0]);
    }

    [Fact]
    public void ExplicitDiagnosticMigrationCanPreserveAllRareBytesWithoutGuessingAnOngoingState()
    {
        NativeCardInventory original = Legacy();
        NativeCardInventory migrated = NativeCardInventoryMigration.MigrateLegacyInitialization(original, [], 0, new(0, 0, 0), 0);
        Assert.Equal(original.Cards, migrated.Cards);
        Assert.Equal(original.ObtainedRareCards, migrated.ObtainedRareCards);
        Assert.Equal(0u, migrated.TripleTriadRandomState);
        Assert.Equal(new TripleTriadStatistics(0, 0, 0), migrated.TripleTriadStatistics);
        Assert.Equal((byte)0, migrated.CollectionFlags);
    }

    [Fact]
    public void MigrationRejectsUnknownContractsInvalidIdsAndChangesToAcquiredOrLocatedRareCards()
    {
        NativeCardInventory original = Legacy();
        Assert.Throws<InvalidDataException>(() => original.Validate());
        Assert.Throws<InvalidDataException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(original with { Rule = "unknown" }, [], 1, new(0, 0, 0), 0));
        Assert.Throws<InvalidDataException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(NativeCardInventory.CreateNewGame(), [], 1, new(0, 0, 0), 0));
        Assert.Throws<ArgumentException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(original, [76], 1, new(0, 0, 0), 0));
        Assert.Throws<ArgumentException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(original, [110], 1, new(0, 0, 0), 0));
        Assert.Throws<ArgumentException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(original, [77, 77], 1, new(0, 0, 0), 0));
        original.Cards[77] = 33;
        Assert.Throws<InvalidDataException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(original, [77], 1, new(0, 0, 0), 0));
        original.Cards[77] = 0;
        original.ObtainedRareCards[0] = 1;
        Assert.Throws<InvalidDataException>(() => NativeCardInventoryMigration.MigrateLegacyInitialization(original, [77], 1, new(0, 0, 0), 0));
    }

    private static NativeCardInventory Legacy() => new()
    {
        Version = 1,
        Rule = NativeCardInventoryMigration.LegacyInitializationRule,
        Cards = new byte[110],
        ObtainedRareCards = new byte[5]
    };
}

