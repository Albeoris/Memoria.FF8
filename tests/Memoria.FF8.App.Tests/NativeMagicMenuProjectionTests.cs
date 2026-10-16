using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMagicMenuProjectionTests
{
    [Theory]
    [InlineData("magic-use", "", PlayerActionKind.UseFieldMagic, 1)]
    [InlineData("magic-transfer", "10", PlayerActionKind.TransferMagic, 10)]
    [InlineData("magic-transfer", "All", PlayerActionKind.TransferMagic, 100)]
    public void ProjectionPreservesPhysicalStockOwnerAndRecipient(string kind, string quantity, PlayerActionKind expected, int count)
    {
        PlayerAction action = NativeMagicMenuProjection.PlayerAction(new MenuAction(kind, "magic-6-21", "character-2", quantity));
        Assert.Equal(expected, action.Kind);
        Assert.Equal(count, action.Value);
        Assert.Equal("6:21:2", action.Text);
        Assert.Equal((6, 21, 2), NativeMagicMenuInput.Validate(action));
    }

    [Theory]
    [InlineData("magic-8-21", "character-2", "1")]
    [InlineData("magic-0-0", "character-2", "1")]
    [InlineData("magic-0-57", "character-2", "1")]
    [InlineData("magic-0-21", "character-8", "1")]
    [InlineData("magic-0-21", "character-2", "0")]
    [InlineData("magic-0-21", "character-2", "101")]
    public void ProjectionRejectsInvalidIdentitiesAndQuantities(string stock, string target, string quantity)
    {
        Assert.Throws<ArgumentException>(() => NativeMagicMenuProjection.PlayerAction(new MenuAction("magic-transfer", stock, target, quantity)));
    }

    [Fact]
    public void LiveControllerBindsSelectedSpellAndExplicitExchangeTarget()
    {
        MenuData data = Data();
        data.SupportedActions.Add("magic-transfer");
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("magic-exchange");
        controller.Execute(new MenuAction("select-magic", "magic-0-2"));
        controller.Execute(new MenuAction("select-exchange", "character-1"));
        controller.Execute(new MenuAction("magic-transfer", Value: "All"));
        Assert.Equal(new MenuAction("magic-transfer", "magic-0-2", "character-1", "All"), Assert.Single(actions));
    }

    [Fact]
    public void DepletedSelectedStockNeverFallsBackToAnotherSpell()
    {
        MenuData data = Data();
        data.SupportedActions.Add("magic-use");
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, action =>
        {
            actions.Add(action);
            data.Magic.RemoveAll(magic => magic.Id == action.SubjectId);
        });
        controller.Open("magic-use");
        controller.Execute(new MenuAction("select-magic", "magic-0-1"));
        controller.Execute(new MenuAction("magic-use", TargetId: "character-1"));
        controller.Execute(new MenuAction("magic-use", TargetId: "character-1"));
        Assert.Single(actions);
        Assert.Null(controller.Magic);
        Assert.Contains("Select a spell again", controller.Message);
        controller.Execute(new MenuAction("select-character", "character-1"));
        Assert.Equal("magic-1-1", controller.Magic?.Id);
    }

    [Fact]
    public void InstalledFieldMenuCastsAndTransfersCanonicalStocks()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native magic and character resources are required.");
        using GameAssets assets = new(directory);
        using BattleAssets battle = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int cure = Enumerable.Range(1, 56).Single(id => catalog.MagicName(id) == "Cure");
        int fire = Enumerable.Range(1, 56).Single(id => catalog.MagicName(id) == "Fire");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] stocks = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray();
        stocks[0] = new((byte)fire, 2);
        stocks[1] = new((byte)cure, 2);
        records[0] = records[0] with { MagicInventory = stocks };
        records[1] = records[1] with { HitPoints = new(1, 1), Status = 0, MagicInventory = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray() };
        vitals = vitals with { Records = records };
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: catalog), action =>
        {
            PlayerAction input = NativeMagicMenuProjection.PlayerAction(action);
            int row = NativeMagicMenuInput.Validate(input).Source;
            vitals = NativeMagicMenuInput.Apply(vitals, input, battle.ReadPlayerNativeStatRules(vitals, row));
        });
        controller.Open("magic-use");
        Assert.False(controller.Entries.Single(entry => entry.Id == $"magic-0-{fire}").Enabled);
        Assert.True(controller.Entries.Single(entry => entry.Id == $"magic-0-{cure}").Enabled);
        controller.Execute(new MenuAction("select-magic", $"magic-0-{cure}"));
        controller.Execute(new MenuAction("magic-use", TargetId: "character-1"));
        Assert.True(vitals.Records[1].HitPoints.Minimum > 1);
        Assert.Equal(1, vitals.Records[0].MagicInventory[1].Count);
        controller.Execute(new MenuAction("select-exchange", "character-1"));
        controller.Execute(new MenuAction("magic-transfer", Value: "All"));
        Assert.Equal(new NativeMagicStock(0, 0), vitals.Records[0].MagicInventory[1]);
        Assert.Equal(new NativeMagicStock((byte)cure, 1), vitals.Records[1].MagicInventory[0]);
        Assert.Null(controller.Magic);
    }

    private static MenuData Data()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "character-0", InParty = true, CurrentHp = 10 });
        data.Characters.Add(new MenuCharacter { Id = "character-1", InParty = true, CurrentHp = 10 });
        data.Magic.Add(new MenuMagic { Id = "magic-0-1", CharacterId = "character-0", Quantity = 1 });
        data.Magic.Add(new MenuMagic { Id = "magic-0-2", CharacterId = "character-0", Quantity = 10 });
        data.Magic.Add(new MenuMagic { Id = "magic-1-1", CharacterId = "character-1", Quantity = 10 });
        return data;
    }
}
