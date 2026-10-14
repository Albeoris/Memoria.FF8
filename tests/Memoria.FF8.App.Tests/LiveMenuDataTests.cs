using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LiveMenuDataTests
{
    [Fact]
    public void MissingNativeOwnersStayUnavailableWithoutDemoFallback()
    {
        MenuData data = LiveMenuData.Build(new GameState());
        Assert.Empty(data.Characters);
        Assert.Empty(data.GuardianForces);
        Assert.Empty(data.Items);
        Assert.Empty(data.Magic);
        Assert.Empty(data.Cards);
        Assert.Empty(data.SupportedActions);
        Assert.Null(data.Currency);
        Assert.Null(data.PlayTime);
        Assert.Null(data.SeeDRank);
        Assert.False(data.SaveAvailable);
    }

    [Fact]
    public void CardProjectionDistinguishesCurrentOwnershipFromHistoricalAcquisition()
    {
        NativeCardInventory cards = NativeCardInventory.CreateNewGame().Acquire(0, out _).Acquire(77, out _).Acquire(78, out _);
        cards.Cards[78] = 0;
        string original = JsonSerializer.Serialize(cards);
        MenuData data = LiveMenuData.Build(new GameState { CardInventory = cards });
        Assert.Equal(1, data.Cards.Single(card => card.Id == "card-0").Quantity);
        Assert.Equal(1, data.Cards.Single(card => card.Id == "card-77").Quantity);
        Assert.Equal(0, data.Cards.Single(card => card.Id == "card-78").Quantity);
        Assert.Equal(original, JsonSerializer.Serialize(cards));
    }

    [Fact]
    public void LiveProjectionPreservesPhysicalPartyOrderIntervalsAndAuthenticText()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native initialization resources are required.");
        using GameAssets assets = new(directory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true).AcquireGuardianForce(0);
        NativeCharacterVital[] rows = initial.Records.ToArray();
        rows[0] = rows[0] with { HitPoints = new NativeHpWord(1, 10) };
        NativeCharacterVitals vitals = initial with { Records = rows, CombatParty = [1, 0, 255] };
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(1, 1, out _).AddItem(33, 1, out _);
        GameState state = new() { CharacterVitals = vitals, Economy = economy, EventState = JsonSerializer.SerializeToElement(new { Scene = new { Names = new Dictionary<int, string> { [0] = "Saved research name" } } }) };
        string original = JsonSerializer.Serialize(state);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        MenuData data = LiveMenuData.Build(state, new Dictionary<int, string> { [0] = "Research name" }, catalog);
        Assert.Equal(new[] { "character-1", "character-0" }, data.Characters.Where(character => character.InParty).Select(character => character.Id));
        MenuCharacter squall = data.Characters.Single(character => character.Id == "character-0");
        Assert.Equal("Research name", squall.Name);
        Assert.Null(squall.CurrentHp);
        Assert.Equal("1..10", squall.HpDisplay);
        Assert.Equal(vitals.Definitions.CharacterMaximum(rows[0]), squall.MaximumHp);
        Assert.Empty(squall.Stats);
        Assert.Equal("Zell", data.Characters.Single(character => character.Id == "character-1").Name);
        Assert.Equal("Quezacotl", data.GuardianForces.Single().Name);
        Assert.Equal("Potion", data.Items.Single(item => item.Id == "item-1").Name);
        Assert.Equal("Tent", data.Items.Single(item => item.Id == "item-33").Name);
        Assert.Equal("Geezard", catalog.CardName(0));
        Assert.Equal("Squall", catalog.CardName(109));
        Assert.Empty(data.SupportedActions);
        Assert.Equal(original, JsonSerializer.Serialize(state));
        Assert.Equal("Saved research name", LiveMenuData.Build(state, catalog: catalog).Characters.Single(character => character.Id == "character-0").Name);
        rows[1] = rows[1] with { CharacterId = 9 };
        NativeCharacterVitals guestVitals = vitals with { Records = rows, CombatParty = [0, 255, 255], DreamEnabled = true };
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame();
        GameState guestState = state with { CharacterVitals = guestVitals, WorldControls = controls };
        Assert.DoesNotContain(LiveMenuData.Build(guestState, catalog: catalog).Characters, character => character.Id == "character-1");
        Assert.Equal("Kiros", LiveMenuData.Build(guestState with { WorldControls = controls.WithGuestMember(9, true) }, catalog: catalog).Characters.Single(character => character.Id == "character-1").Name);
        byte[] junctionMagic = Enumerable.Repeat((byte)1, 20).ToArray();
        rows[0] = rows[0] with { Junction = rows[0].Junction with { MagicJunctions = junctionMagic } };
        MenuCharacter junctioned = LiveMenuData.Build(state with { CharacterVitals = vitals with { Records = rows } }, catalog: catalog).Characters.Single(character => character.Id == "character-0");
        Assert.Equal("Fire", junctioned.Junctions["Eva"]);
        Assert.Equal("Fire", junctioned.Junctions["Hit"]);
        Assert.Equal("Fire", junctioned.Junctions["Luck"]);
        Assert.Equal("Fire", junctioned.Junctions["Elem-Def 4"]);
        Assert.Equal("Fire", junctioned.Junctions["ST-Def 4"]);
    }
}
