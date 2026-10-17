using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

// This is a component fixture using supplied party records, not a migrated save or story continuation.
public sealed class ArchivedBattleSnapshotTests
{
    [Fact]
    public void Encounter29AcceptsSuppliedPartyRecordsWithInstalledComponentOwners()
    {
        string archivePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_REGRESSION_ARCHIVE") ?? string.Empty;
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(archivePath) && Directory.Exists(game), "The preserved user archive and installed original resources are required.");
        using (FileStream archiveBytes = File.OpenRead(archivePath))
        {
            Assert.SkipUnless(Convert.ToHexString(SHA256.HashData(archiveBytes)).Equals("b56da343981b3fc9ec40768126e7fb1fdbf4cd9d54c229bbbf4ca1b4c74c1584", StringComparison.OrdinalIgnoreCase), "The preserved archive with the documented supplied-party fixture fingerprint is required.");
        }
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        ZipArchiveEntry entry = archive.GetEntry("player-saves/quick.json") ?? throw new InvalidDataException("The archive lacks the supplied standalone quick snapshot.");
        using Stream source = entry.Open();
        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement snapshot = document.RootElement;
        Assert.Equal(314, snapshot.GetProperty("FieldId").GetInt32());
        Assert.Equal(262, snapshot.GetProperty("EventState").GetProperty("Frame").GetInt64());
        JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
        // The archive predates the current GF progress/menu owners; only its complete character records are inputs here.
        JsonElement suppliedVitals = snapshot.GetProperty("CharacterVitals");
        NativeCharacterVital[] records = suppliedVitals.GetProperty("Records").Deserialize<NativeCharacterVital[]>(options)!;
        byte[] combatParty = suppliedVitals.GetProperty("CombatParty").GetBytesFromBase64();
        using GameAssets assets = new(game);
        NativeCharacterVitals componentVitals = assets.ReadNewGameCharacterVitals() with { Records = records, CombatParty = combatParty };
        GameSession component = new(new GameState
        {
            CharacterVitals = componentVitals,
            CharacterAppearance = assets.ReadNewGameCharacterAppearance(),
            Economy = assets.ReadNewGameEconomy(),
            GlobalRandom = assets.ReadNewGameGlobalRandom()
        });
        using GameWorld world = new(component, game);
        // The supplied-party statistics fixture does not claim raw graphical entry or music initialization.
        BattleDefinition definition = world.CreateEncounterBattle(29, nativeSeed: 7) with { NativeGraphics = null, NativeClockRules = null };
        Assert.Equal(29, definition.EncounterId);
        BattleCombatantDefinition[] party = definition.Combatants.Where(actor => actor.IsPlayer).ToArray();
        Assert.Equal(combatParty.Select(row => (int)records[row].CharacterId), party.Select(actor => actor.ModelId));
        Assert.Equal(combatParty.Select(row => (int)records[row].HitPoints.Minimum), party.Select(actor => actor.CurrentHp));
        Assert.Equal(BattleOutcome.Active, new BattleSession(definition, BattleAtbMode.TurnBased).Outcome);
        Assert.Null(component.State.EventState);
    }
}
