using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMagicRefinementMenuTests
{
    [Fact]
    public void InstalledRecipeFlowsThroughLiveMenuAndRefreshesBothOwners()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original refinement resources are required.");
        using GameAssets assets = new(directory);
        NativeRefinementDefinitions recipes = assets.ReadNativeRefinementDefinitions();
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeGuardianForceVital gf = vitals.GuardianForces[0];
        vitals.GuardianForces[0] = gf with { Flags = (byte)(gf.Flags | 1), LearnedBank = gf.LearnedBank with { High = gf.LearnedBank.High | (1UL << (97 - 64)) } };
        vitals.Records[0].MagicInventory[0] = new(8, 90);
        NativeEconomy economy = assets.ReadNewGameEconomy();
        economy.Items[0] = new(128, 2);
        NativeCharacterVitals hidden = vitals.WithMemberFlags(7, false);
        Assert.Throws<InvalidOperationException>(() => NativeMagicMenuInput.Apply(hidden, new PlayerAction(PlayerActionKind.TransferMagic, 1, "0:8:7")));
        NativeCharacterVital[] guestRecords = hidden.Records.ToArray();
        guestRecords[7] = guestRecords[7] with { CharacterId = 9 };
        NativeCharacterVitals guest = hidden with { Records = guestRecords };
        Assert.False(guest.IsMenuCharacterAvailable(7, NativeWorldControlState.CreateNewGame()));
        Assert.True(guest.IsMenuCharacterAvailable(7, NativeWorldControlState.CreateNewGame().WithGuestMember(9, true)));
        List<PlayerAction> inputs = [];
        MenuController controller = new(() =>
        {
            GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals, Economy = economy };
            MenuData data = LiveMenuData.Build(state, catalog: names);
            NativeRefinementMenuProjection.Augment(data, state, recipes, names);
            return data;
        }, action =>
        {
            PlayerAction input = NativeRefinementMenuProjection.PlayerAction(action);
            inputs.Add(input);
            (int recipe, int row) = NativeRefinementInput.Validate(input);
            NativeRefinementResult next = recipes.Refine(vitals, economy, null, input.Value, recipe, row, input.Quantity!.Value);
            vitals = next.Vitals;
            economy = next.Economy;
        });
        controller.Open("ability-refine");
        controller.Execute(new MenuAction("select-recipe", "native-refine-97-0"));
        controller.Navigate(MenuInput.Confirm);
        PlayerAction request = Assert.Single(inputs);
        Assert.Equal("0:0", request.Text);
        Assert.Equal(97, request.Value);
        Assert.Equal(1, request.Quantity);
        Assert.Equal(100, vitals.Records[0].MagicInventory.Single(stock => stock.SpellId == 8).Count);
        Assert.Equal(new NativeItemStock(128, 1), economy.Items[0]);
        Assert.All(controller.Entries, entry => Assert.False(entry.Enabled));
        controller.Navigate(MenuInput.Confirm);
        Assert.Single(inputs);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
        restored.Validate();
        Assert.Equal(vitals.Records[0], restored.Records[0]);
    }

    [Theory]
    [InlineData("97:0", "8", "1")]
    [InlineData("96:0", "0", "1")]
    [InlineData("97:143", "0", "1")]
    [InlineData("97:0", "0", "0")]
    [InlineData("97:0", "0", "101")]
    public void InvalidRefinementInputCannotReachAnOwner(string recipe, string character, string batches)
    {
        Assert.Throws<ArgumentException>(() => NativeRefinementInput.Validate(NativeRefinementMenuProjection.PlayerAction(new MenuAction("native-refine", recipe, character, batches))));
    }
}
