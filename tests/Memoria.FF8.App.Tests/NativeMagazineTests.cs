using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMagazineTests
{
    [Fact]
    public void OriginalMagazineRangesAndEnglishPageContentAreRetained()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        Assert.Equal(Enumerable.Range(0, 43), Enumerable.Range(177, 22).SelectMany(item => catalog.MagazinePages(item)).Select(page => (int)page.PageId));
        Assert.Equal(4, catalog.MagazinePages(177).Length);
        NativeMagazinePage first = catalog.MagazinePage(177, 0);
        Assert.Equal(6, first.WeaponRecipe);
        Assert.Contains(first.Text, text => text.Contains("Weapons Monthly 1st Issue", StringComparison.Ordinal));
        Assert.Contains(first.Text, text => text.Contains("Lionheart", StringComparison.Ordinal));
        Assert.Contains(first.Text, text => text.Contains("Adamantine", StringComparison.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => catalog.MagazinePage(177, 4));
        Assert.Single(catalog.MagazinePages(198));
    }

    [Fact]
    public void ViewingAppliesOnlyOriginalPageLearningAndNeverConsumesTheMagazine()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(177, 1, out _).AddItem(184, 1, out _).AddItem(189, 1, out _);
        NativeWeaponRecipes recipes = assets.ReadNewGameWeaponRecipes();
        NativeLimitProgress limits = assets.ReadNewGameLimitProgress();
        string inventory = JsonSerializer.Serialize(economy);
        var weapon = catalog.MagazinePage(177, 0).View(economy, recipes, limits);
        Assert.Equal(recipes.Bits | 1u << 6, weapon.Recipes!.Bits);
        Assert.Equal(recipes.Bits | 1u << 6, catalog.MagazinePage(177, 0).View(economy, weapon.Recipes, limits).Recipes!.Bits);
        Assert.Equal(recipes.Bits, assets.ReadNewGameWeaponRecipes().Bits);
        Assert.Same(limits, weapon.Limits);
        var duel = catalog.MagazinePage(184, 28).View(economy, recipes, limits);
        Assert.Equal((ushort)(limits.DuelFlags | 1 << 4), duel.Limits!.DuelFlags);
        var pet = catalog.MagazinePage(189, 33).View(economy, recipes, limits);
        Assert.Equal((byte)(limits.PetPalsFlags!.Value | 1 << 5), pet.Limits!.PetPalsFlags);
        Assert.Equal(inventory, JsonSerializer.Serialize(economy));
        Assert.Throws<NotSupportedException>(() => catalog.MagazinePage(189, 33).View(economy, recipes, limits with { PetPalsFlags = null }));
        Assert.Throws<InvalidOperationException>(() => catalog.MagazinePage(178, 4).View(economy, recipes, limits));
    }

    [Fact]
    public void ViewerDispatchesFirstPageWrapsTurnsAndClosesWithoutTargetSelection()
    {
        using GameAssets assets = Assets();
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        MenuData data = new();
        data.Items.Add(new MenuItem { Id = "item-177", Name = catalog.ItemName(177)!, Quantity = 1, NativeMagazinePages = catalog.MagazinePages(177) });
        List<MenuAction> actions = [];
        MenuController controller = new(() => data, actions.Add);
        controller.Open("item-use");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("magazine", controller.ScreenId);
        Assert.Equal(new MenuAction("magazine-read", "item-177", Value: "0"), Assert.Single(actions));
        Assert.NotNull(controller.BuildView());
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("3", actions.Last().Value);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("0", actions.Last().Value);
        controller.Navigate(MenuInput.Cancel);
        Assert.Equal("item-use", controller.ScreenId);
        Assert.Equal(1, data.Items[0].Quantity);
        data.Items[0].NativeMagazinePageUnavailableReasons[3] = "Required saved page owner is unavailable.";
        controller.Navigate(MenuInput.Confirm);
        Assert.False(controller.Entries.Single(entry => entry.Id == "previous").Enabled);
        int dispatched = actions.Count;
        controller.Navigate(MenuInput.Up);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(dispatched, actions.Count);
    }

    [Fact]
    public void TaskSaveRefusesUnknownPetPalsOwnerAndDeclaredComponentContinuationRestoresLearning()
    {
        using GameAssets assets = Assets();
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "Task standalone checkpoint is required for the declared continuation component.");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, assets.GameDirectory);
        world.EnsureField();
        Assert.Null(session.State.LimitProgress);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        MenuData projected = LiveMenuData.Build(session.State, catalog: catalog);
        List<MenuAction> actions = [];
        MenuController controller = new(() => projected, actions.Add);
        controller.Open("item-use");
        int petIndex = controller.Entries.ToList().FindIndex(entry => entry.Id == "item-189");
        Assert.True(petIndex >= 0);
        Assert.False(controller.Entries[petIndex].Enabled);
        Assert.Contains("Limit progression", controller.Entries[petIndex].Help);
        for (int index = 0; index < petIndex; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        Assert.Equal("item-189", controller.SelectedEntry!.Id);
        controller.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
        Assert.Equal("item-use", controller.ScreenId);
        NativeEconomy weaponsOwned = session.State.Economy!.AddItem(177, 1, out _);
        MenuData weapons = LiveMenuData.Build(session.State with { Economy = weaponsOwned }, catalog: catalog);
        MenuController valid = new(() => weapons, actions.Add);
        valid.Open("item-use");
        Assert.True(valid.Entries.Single(entry => entry.Id == "item-177").Enabled);
        JsonElement before = world.Events.Capture();
        Assert.Throws<NotSupportedException>(() => world.Apply(new PlayerAction(PlayerActionKind.ReadMagazinePage, 189, Quantity: 33)));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        //Declared component fixture only: this is not a migrated gameplay save or an inferred historical byte.
        session.SetLimitProgress(assets.ReadNewGameLimitProgress() with { PetPalsFlags = 0 });
        NativeEconomy economy = session.State.Economy!;
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.ReadMagazinePage, 189, Quantity: 33)).Accepted);
        Assert.Equal((byte?)32, session.State.LimitProgress!.PetPalsFlags);
        Assert.Equal(economy.Items, session.State.Economy!.Items);
        JsonElement snapshot = world.Events.Capture();
        GameSession restoredSession = new(session.State with { EventState = snapshot });
        using GameWorld restored = new(restoredSession, assets.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(snapshot, restored.Events.Capture()));
        Assert.Equal(session.State.LimitProgress, restoredSession.State.LimitProgress);
        Assert.Equal(session.State.WeaponRecipes, restoredSession.State.WeaponRecipes);
        JsonElement stable = restored.Events.Capture();
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.Apply(new PlayerAction(PlayerActionKind.ReadMagazinePage, 189, Quantity: 34)));
        Assert.True(JsonElement.DeepEquals(stable, restored.Events.Capture()));
    }

    private static GameAssets Assets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original magazine and initialization resources are required.");
        return new GameAssets(directory);
    }
}
