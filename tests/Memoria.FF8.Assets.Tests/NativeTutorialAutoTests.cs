using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTutorialAutoTests
{
    [Fact]
    public void NativeAutoUsesStockOrderForTiesAndSignedStockCounts()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeCharacterVitals party = Fixture(catalog, 2, new NativeMagicStock(1, 10), new NativeMagicStock(4, 10));
        string original = JsonSerializer.Serialize(party);
        NativeCharacterVitals first = catalog.AutoJunction(party, 0, 0);
        Assert.Equal((byte)1, first.Records[0].Junction.MagicJunctions[1]);
        Assert.Equal(original, JsonSerializer.Serialize(party));
        Assert.Equal(party.Records[0].MagicInventory, first.Records[0].MagicInventory);
        NativeCharacterVitals reversed = Fixture(catalog, 2, new NativeMagicStock(4, 10), new NativeMagicStock(1, 10));
        Assert.Equal((byte)4, catalog.AutoJunction(reversed, 0, 0).Records[0].Junction.MagicJunctions[1]);
        NativeCharacterVitals quantity = Fixture(catalog, 2, new NativeMagicStock(1, 10), new NativeMagicStock(4, 11));
        Assert.Equal((byte)4, catalog.AutoJunction(quantity, 0, 0).Records[0].Junction.MagicJunctions[1]);
        NativeCharacterVitals signed = Fixture(catalog, 2, new NativeMagicStock(1, 255), new NativeMagicStock(4, 0));
        Assert.All(catalog.AutoJunction(signed, 0, 0).Records[0].Junction.MagicJunctions.Take(19), spell => Assert.Equal((byte)0, spell));
    }

    [Fact]
    public void NativeAutoRespectsSlotMasksDefenseCapacityAndUniqueStocks()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeMagicStock[] stocks = [new(1, 10), new(2, 20), new(3, 30), new(4, 40)];
        foreach ((int ability, int count) in new[] { (12, 1), (14, 2), (15, 4) })
        {
            NativeCharacterVitals result = catalog.AutoJunction(Fixture(catalog, ability, stocks), 0, 2);
            byte[] junctions = result.Records[0].Junction.MagicJunctions;
            Assert.All(junctions.Take(11), spell => Assert.Equal((byte)0, spell));
            Assert.Equal(count, junctions.Skip(11).Take(4).Count(spell => spell != 0));
            Assert.All(junctions.Skip(15).Take(4), spell => Assert.Equal((byte)0, spell));
            Assert.Equal(count, junctions.Where(spell => spell != 0).Distinct().Count());
        }
        NativeCharacterVitals unavailable = Fixture(catalog, 0, stocks);
        Assert.All(catalog.AutoJunction(unavailable, 0, 0).Records[0].Junction.MagicJunctions.Take(19), spell => Assert.Equal((byte)0, spell));
    }

    [Fact]
    public void OriginalMagicLessonAutoRetainsItsOriginalPartyAndRejectsInvalidOperands()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeCharacterVitals party = catalog.CreateIsolatedParty(1);
        string original = JsonSerializer.Serialize(party);
        NativeCharacterVitals result = catalog.AutoJunction(party, 0, 0);
        Assert.Equal((byte)7, result.Records[0].Junction.MagicJunctions[1]);
        Assert.Equal(party.CombatParty, result.CombatParty);
        Assert.Equal(party.Records[0].Junction.GuardianForces, result.Records[0].Junction.GuardianForces);
        Assert.Equal(party.Records[0].Junction.Commands, result.Records[0].Junction.Commands);
        Assert.Equal(party.Records[0].MagicInventory, result.Records[0].MagicInventory);
        Assert.Equal(original, JsonSerializer.Serialize(party));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.AutoJunction(party, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.AutoJunction(party, 8, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.AutoJunction(party, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.AutoJunction(party, 0, 3));
        NativeCharacterVitals malformed = Fixture(catalog, 2, new NativeMagicStock(57, 1));
        Assert.Throws<InvalidDataException>(() => catalog.AutoJunction(malformed, 0, 0));
    }

    private static NativeCharacterVitals Fixture(NativeTutorialCatalog catalog, int learnedAbility, params NativeMagicStock[] stocks)
    {
        NativeCharacterVitals party = catalog.CreateIsolatedParty(1);
        NativeCharacterVital[] records = party.Records.ToArray();
        NativeMagicStock[] inventory = Enumerable.Range(0, 32).Select(index => index < stocks.Length ? stocks[index] : new NativeMagicStock(0, 0)).ToArray();
        records[0] = records[0] with { MagicInventory = inventory, Junction = records[0].Junction with { GuardianForces = 1, MagicJunctions = new byte[20] } };
        NativeGuardianForceVital[] guardianForces = party.GuardianForces.ToArray();
        guardianForces[0] = guardianForces[0] with { Flags = 1, LearnedBank = new NativeGuardianForceLearnedBank(1UL << learnedAbility, 0) };
        return party with { Records = records, GuardianForces = guardianForces };
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }
}
