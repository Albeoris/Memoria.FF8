using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class MenuControllerTests
{
    [Fact]
    public void EveryRegisteredDemoScreenBuildsAndEveryDestinationExists()
    {
        var ids = new MenuController().ScreenIds;
        Assert.True(ids.Count >= 70);
        foreach (var id in ids)
        {
            var controller = new MenuController();
            controller.Open(id, demo: true);
            if (id == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(controller);
            }
            Assert.NotNull(controller.BuildView());
            foreach (var entry in controller.Entries)
            {
                if (entry.Destination != null)
                {
                    Assert.Contains(entry.Destination, ids);
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MagazineViewerRejectsMissingOriginalPageMetadata(bool demo)
    {
        MenuController controller = new(() => new MenuData());
        controller.Open("magazine", demo);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => controller.BuildView());

        Assert.Equal("Magazine viewing requires original page layout metadata.", exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MagazineViewerRejectsOwnedPageWithoutOriginalLayout(bool demo)
    {
        MenuController controller = new(() => new MenuData());
        controller.Open("magazine", demo);
        MenuItem item = MagazineMenuFixture.AddOwnedPage(controller);
        item.NativeMagazinePages[0] = item.NativeMagazinePages[0] with { Layout = null };

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => controller.BuildView());

        Assert.Equal("Magazine page lacks its original layout and artwork descriptors.", exception.Message);
    }

    [Fact]
    public void LiveProviderIsCachedAndNoMissingNativeDataUsesFixtureValues()
    {
        var reads = 0;
        var live = new MenuData { Location = "Authentic field" };
        var controller = new MenuController(() =>
        {
            reads++;
            return live;
        });
        controller.Open("main");
        Assert.Same(controller.BuildView(), controller.BuildView());
        Assert.Equal(1, reads);
        Assert.Empty(controller.Data.Characters);
        Assert.Empty(controller.Data.Items);
        Assert.Null(controller.Data.Currency);
        controller.Open("item", demo: true);
        Assert.NotEmpty(controller.Data.Items);
        Assert.Empty(live.Items);
        Assert.Equal("Authentic field", live.Location);
    }

    [Fact]
    public void UnsupportedLiveActionCannotReachMutationOwner()
    {
        var requests = new List<MenuAction>();
        var live = new MenuData();
        var controller = new MenuController(() => live, requests.Add);
        controller.Open("config");
        controller.Execute(new MenuAction("junction-auto", Value: "Attack"));
        Assert.Empty(requests);
        Assert.Contains("Unsupported native contract", controller.Message);
        live.SupportedActions.Add("config:Music volume");
        controller.Execute(new MenuAction("config", "Music volume", Value: "75%"));
        Assert.Single(requests);
        controller.Execute(new MenuAction("config", "ATB", Value: "Wait"));
        Assert.Single(requests);
    }

    [Fact]
    public void ItemUseConsumesOnlyFixtureStockAndUpdatesSelectedTarget()
    {
        var controller = new MenuController();
        controller.Open("item-use", demo: true);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("item-target", controller.ScreenId);
        var item = controller.Item!;
        var target = controller.Data.Characters[0];
        var oldQuantity = item.Quantity;
        var oldHp = target.CurrentHp!.Value;
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(oldQuantity - 1, item.Quantity);
        Assert.Equal(Math.Min(target.MaximumHp!.Value, oldHp + item.DemoHealing), target.CurrentHp);
        controller.Navigate(MenuInput.Cancel);
        Assert.Equal("item-use", controller.ScreenId);
    }

    [Fact]
    public void MagicTransferConservesStockAndRespectsCapacity()
    {
        var controller = new MenuController();
        controller.Open("magic-exchange", demo: true);
        controller.Navigate(MenuInput.Confirm);
        var source = controller.Magic!;
        var recipient = controller.Data.Characters[1];
        var destination = controller.Data.Magic.Single(m => m.Id == source.Id && m.CharacterId == recipient.Id);
        destination.Quantity = 98;
        var total = source.Quantity + destination.Quantity;
        controller.Execute(new MenuAction("magic-transfer", TargetId: recipient.Id));
        Assert.Equal(100, destination.Quantity);
        Assert.Equal(total, source.Quantity + destination.Quantity);
    }

    [Fact]
    public void JunctionCommandsTargetSelectedSlotAndExchangePreservesMagicOwnership()
    {
        var controller = new MenuController();
        controller.Open("junction-command", demo: true);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(2, controller.CommandSlot);
        controller.Execute(new MenuAction("junction-command", Value: "Item"));
        Assert.Equal("Item", controller.Character!.Commands[2]);
        var source = controller.Data.Characters[0];
        var target = controller.Data.Characters[1];
        var sourceMagic = controller.Data.Magic.First(m => m.CharacterId == source.Id);
        var targetMagic = controller.Data.Magic.First(m => m.CharacterId == target.Id);
        controller.Execute(new MenuAction("select-exchange", source.Id));
        controller.Execute(new MenuAction("junction-exchange", TargetId: target.Id));
        Assert.Equal(target.Id, sourceMagic.CharacterId);
        Assert.Equal(source.Id, targetMagic.CharacterId);
        Assert.Equal("Item", target.Commands[2]);
    }

    [Fact]
    public void FixtureRefinementSaveAndResetAreIsolated()
    {
        var controller = new MenuController();
        controller.Open("ability-refine", demo: true);
        var recipe = controller.Recipe!;
        var ingredient = controller.Data.Items.Single(i => i.Id == recipe.IngredientId);
        var result = controller.Data.Magic.Single(m => m.Id == recipe.ResultId && m.CharacterId == controller.Character!.Id);
        var oldStock = result.Quantity;
        var oldIngredients = ingredient.Quantity;
        controller.Execute(new MenuAction("refine"));
        Assert.Equal(oldStock + recipe.ResultQuantity, result.Quantity);
        Assert.Equal(oldIngredients - recipe.IngredientQuantity, ingredient.Quantity);
        controller.Execute(new MenuAction("save", "1"));
        Assert.StartsWith("DEMO", controller.Data.SaveSlots[1].Summary);
        controller.Execute(new MenuAction("demo-reset"));
        Assert.Equal("Empty demo slot", controller.Data.SaveSlots[1].Summary);
    }

    [Fact]
    public void NewScreensAppearInMainAndGalleryWithoutControllerChanges()
    {
        var controller = new MenuController();
        controller.Register(new MenuScreenDefinition { Id = "extension", Title = "Extension", Help = "Test extension", MainCommand = true, Entries = _ => [] });
        controller.Open("main", demo: true);
        Assert.Contains(controller.Entries, e => e.Id == "extension");
        controller.Open("debug-gallery", demo: true);
        Assert.Contains(controller.Entries, e => e.Id == "extension");
        controller.Open("extension", demo: true);
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Cancel);
        Assert.False(controller.IsOpen);
    }

    [Fact]
    public void LimitFixturePagesHavePopulatedListsAndMutableSetup()
    {
        var controller = new MenuController();
        foreach (var (screen, family) in new[] { ("status-renzokuken", "Renzokuken"), ("status-angelo", "Combine"), ("status-angel-wing", "Combine"), ("status-duel", "Duel"), ("status-blue-magic", "Blue Magic"), ("status-shot", "Shot"), ("status-slot", "Slot") })
        {
            controller.Open(screen, demo: true);
            Assert.NotEmpty(controller.Entries);
            Assert.Equal(family, controller.Character!.LimitBreak);
        }
        controller.Open("status-renzokuken", demo: true);
        var squall = controller.Data.Characters[0];
        Assert.Equal("Off", squall.LimitSettings["Auto"]);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("On", squall.LimitSettings["Auto"]);
        controller.Open("status-slot", demo: true);
        var selphie = controller.Data.Characters[3];
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("1", selphie.LimitSettings["Preview index"]);
        controller.Open("status-angelo", demo: true);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("Angelo Cannon", controller.Data.Characters[1].LimitSettings["Learning"]);
    }

    [Fact]
    public void ExplicitTransferAndRefineQuantitiesClampWithoutCreatingStock()
    {
        var controller = new MenuController();
        controller.Open("magic-exchange", demo: true);
        controller.Navigate(MenuInput.Confirm);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("magic-exchange-quantity", controller.ScreenId);
        var source = controller.Magic!;
        var target = controller.Data.Magic.Single(m => m.Id == source.Id && m.CharacterId == controller.Data.Characters[1].Id);
        var oldSource = source.Quantity;
        var oldTarget = target.Quantity;
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(oldSource - 1, source.Quantity);
        Assert.Equal(oldTarget + 1, target.Quantity);
        controller.Open("ability-refine", demo: true);
        var recipe = controller.Recipe!;
        var ingredient = controller.Data.Items.Single(i => i.Id == recipe.IngredientId);
        var result = controller.Data.Magic.Single(m => m.Id == recipe.ResultId && m.CharacterId == controller.Character!.Id);
        result.Quantity = 99;
        var oldIngredients = ingredient.Quantity;
        controller.Execute(new MenuAction("refine", Value: "All"));
        Assert.Equal(99, result.Quantity);
        Assert.Equal(oldIngredients, ingredient.Quantity);
    }

    [Fact]
    public void BattleDrawNavigationStocksOnlyFixtureMagicAndUsesBattleLayout()
    {
        var controller = new MenuController();
        foreach (var id in controller.ScreenIds.Where(id => id.StartsWith("battle-", StringComparison.Ordinal)))
        {
            controller.Open(id, demo: true);
            Assert.Equal(MenuLayout.BattleHud, controller.CurrentScreen.Layout);
        }
        controller.Open("battle-command", demo: true);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("battle-target", controller.ScreenId);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("battle-draw", controller.ScreenId);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("battle-draw-action", controller.ScreenId);
        var oldStock = controller.Magic!.Quantity;
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(oldStock + 3, controller.Magic.Quantity);
        Assert.NotNull(controller.Data.BattleRewards);
        var live = new MenuCharacter();
        Assert.Null(live.AtbProgress);
    }

    [Fact]
    public void SaveCancellationReturnsAndCardModConsumesExplicitFixtureRecipe()
    {
        var controller = new MenuController();
        controller.Open("save", demo: true);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("save-confirm", controller.ScreenId);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("save", controller.ScreenId);
        Assert.Equal("Empty demo slot", controller.Data.SaveSlots[1].Summary);
        controller.Open("ability-card-mod", demo: true);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("ability-card-result", controller.ScreenId);
        var card = controller.Card!;
        var result = controller.Data.Items.Single(i => i.Id == card.RefinementItemId);
        var oldCards = card.Quantity;
        var oldItems = result.Quantity;
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(oldCards - 1, card.Quantity);
        Assert.Equal(oldItems + card.RefinementQuantity, result.Quantity);
    }
}
