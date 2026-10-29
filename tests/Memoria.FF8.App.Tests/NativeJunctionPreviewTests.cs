using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeJunctionPreviewTests
{
    [Fact]
    public void JunctionSlotPresentationRefreshesWhenGuardiansAreAttachedAndRemoved()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).AcquireGuardianForce(1);
        int junctionAbilityCount = assets.ReadNativeTutorialCatalog().ReadStatusKernel().JunctionAbilities.Count;
        Assert.True(junctionAbilityCount > 3);
        for (int id = 0; id < 2; id++)
        {
            int[] retained = vitals.GuardianForceAbilities(id).Where(entry => entry.Learned && entry.Ability >= junctionAbilityCount).Select(entry => entry.Ability).ToArray();
            int[] junctions = vitals.GuardianForceAbilities(id).Where(entry => entry.Learned && entry.Ability > 0 && entry.Ability < junctionAbilityCount).Select(entry => entry.Ability).ToArray();
            foreach (int ability in junctions)
            {
                vitals = vitals.ForgetGuardianForceAbility(id, ability);
            }
            Assert.Equal(retained, vitals.GuardianForceAbilities(id).Where(entry => entry.Learned && entry.Ability >= junctionAbilityCount).Select(entry => entry.Ability));
        }
        vitals = vitals.TeachGuardianForceAbility(0, 1).TeachGuardianForceAbility(0, 2).TeachGuardianForceAbility(1, 3);
        Assert.Equal(new[] { 1, 2 }, vitals.GuardianForceAbilities(0).Where(entry => entry.Learned && entry.Ability < junctionAbilityCount).Select(entry => entry.Ability));
        Assert.Equal(new[] { 3 }, vitals.GuardianForceAbilities(1).Where(entry => entry.Learned && entry.Ability < junctionAbilityCount).Select(entry => entry.Ability));
        MenuController menu = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: catalog), action =>
        {
            vitals = NativeJunctionMenuInput.Apply(vitals, NativeJunctionMenuProjection.PlayerAction(action));
        });
        menu.Open("junction-gf");
        AssertSlotPresentation(menu);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-gf", menu.ScreenId);
        AssertSlotPresentation(menu, "HP", "Str");
        menu.SelectEntry("junction-gf", "gf-1");
        menu.Navigate(MenuInput.Confirm);
        AssertSlotPresentation(menu, "HP", "Str", "Vit");
        menu.SelectEntry("junction-gf", "gf-0");
        menu.Navigate(MenuInput.Confirm);
        AssertSlotPresentation(menu, "Vit");
        menu.SelectEntry("junction-gf", "gf-1");
        menu.Navigate(MenuInput.Confirm);
        AssertSlotPresentation(menu);
        menu.Open("junction-magic");
        AssertSlotPresentation(menu);
        menu.Open("junction-gf");
        menu.SelectEntry("junction-gf", "gf-0");
        menu.Navigate(MenuInput.Confirm);
        menu.Open("junction-magic");
        AssertSlotPresentation(menu, "HP", "Str");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-stock", menu.ScreenId);
        AssertSlotPresentation(menu, "HP", "Str");
    }

    private static void AssertSlotPresentation(MenuController menu, params string[] available)
    {
        UiView view = menu.BuildView();
        view.Resize(1280, 896);
        UiElement[] elements = Descendants(view.Root).ToArray();
        foreach (string stat in new[] { "HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck" })
        {
            UiLabel name = Assert.Single(elements.OfType<UiLabel>(), label => label.Text == stat);
            UiElement row = Assert.Single(elements, element => element.Children.Contains(name));
            UiLabel[] labels = row.Children.OfType<UiLabel>().ToArray();
            Assert.Equal(3, labels.Length);
            Assert.Equal(!available.Contains(stat), name.Muted);
            Assert.Equal(name.Muted, labels[1].Muted);
            Assert.False(labels[2].Muted);
            Assert.Null(labels[2].Color);
        }
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void MovingStockPreviewsBothStatsWithoutMutationAndConfirmReturnsToStats(int width, int height)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).TeachGuardianForceAbility(0, 1).TeachGuardianForceAbility(0, 2);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] stocks = records[0].MagicInventory.ToArray();
        stocks[0] = new NativeMagicStock(1, 100);
        records[0] = records[0] with { MagicInventory = stocks };
        vitals = (vitals with { Records = records }).SetJunctionMagic(0, 0, 1);
        string before = JsonSerializer.Serialize(vitals);
        int applied = 0;
        MenuController menu = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }, catalog: catalog), action =>
        {
            vitals = NativeJunctionMenuInput.Apply(vitals, NativeJunctionMenuProjection.PlayerAction(action));
            applied++;
        });
        menu.Open("junction-magic");
        Assert.Equal(9, menu.Character!.Stats.Count);
        Assert.NotNull(menu.Character.NextLevelExperience);
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Down);
        Assert.Equal("magic-0-1", menu.SelectedEntry!.Id);
        IReadOnlyDictionary<string, int> projected = menu.Character.JunctionPreview!("Str", "magic-0-1");
        Assert.True(projected["HP"] < menu.Character.MaximumHp);
        Assert.True(projected["Str"] > menu.Character.Stats["Str"]);
        UiView view = menu.BuildView();
        view.Resize(width, height);
        UiLabel hp = Assert.Single(Descendants(view.Root).OfType<UiLabel>(), label => label.Id == "junction-value-HP");
        UiLabel strength = Assert.Single(Descendants(view.Root).OfType<UiLabel>(), label => label.Id == "junction-value-Str");
        Assert.Contains("→", hp.Text);
        Assert.Contains("→", strength.Text);
        Assert.Equal(new UiColor(255, 120, 120), hp.Color);
        Assert.Equal(new UiColor(120, 190, 255), strength.Color);
        Assert.True(hp.Bounds.Bottom <= strength.Bounds.Y);
        Assert.True(hp.Bounds.Right <= width);
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-magic", menu.ScreenId);
        Assert.Equal(before, JsonSerializer.Serialize(vitals));
        Assert.Equal(0, applied);
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(1, applied);
        Assert.Equal("junction-magic", menu.ScreenId);
        Assert.Equal(projected["HP"], menu.Character!.MaximumHp);
        Assert.Equal(projected["Str"], menu.Character.Stats["Str"]);
        Assert.Equal(0, vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(1, vitals.Records[0].Junction.MagicJunctions[1]);
        Assert.Equal(100, vitals.Records[0].MagicInventory[0].Count);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(999, 1)]
    [InlineData(1000, 1000)]
    [InlineData(98999, 1)]
    [InlineData(99000, 0)]
    public void NextLevelExperienceUsesTheNativeThresholdAndLevelCap(uint experience, int remaining)
    {
        Assert.Equal(remaining, LiveMenuData.ExperienceToNextLevel(experience, new NativeStatCurve(100, 0, 1, 1, 1)));
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
