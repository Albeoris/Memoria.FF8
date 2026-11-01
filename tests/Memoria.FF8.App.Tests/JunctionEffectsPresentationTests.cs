using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionEffectsPresentationTests
{
    [Theory]
    [InlineData(MenuPresentation.Vector, true)]
    [InlineData(MenuPresentation.Vector, false)]
    [InlineData(MenuPresentation.Classic, true)]
    [InlineData(MenuPresentation.Classic, false)]
    public void CompleteTablesKeepAuthoritativeMovePreviewAndRestoreCurrentEffectsOnCancel(MenuPresentation presentation, bool elemental)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Original effect rules must be configured.");
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        foreach (int ability in Enumerable.Range(1, 19))
        {
            source = source.TeachGuardianForceAbility(0, ability);
        }
        source = source.ToggleGuardianForceJunction(0, 0);
        int defenseSlot = elemental ? 11 : 15;
        int selectedSpell = 0;
        for (int spell = 1; spell <= 56; spell++)
        {
            source.Records[0] = source.Records[0] with { MagicInventory = Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), Junction = source.Records[0].Junction with { MagicJunctions = new byte[20] } };
            source.Records[0].MagicInventory[0] = new NativeMagicStock((byte)spell, 100);
            source = source.SetJunctionMagic(0, defenseSlot, spell);
            MenuJunctionEffects effects = LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = source }, catalog: catalog, includeJunctionPlanner: false).Characters.First(actor => actor.Id == "character-0").JunctionEffects!;
            if ((elemental ? effects.Elements : effects.Statuses).Any(value => value.Defense > 0))
            {
                selectedSpell = spell;
                break;
            }
        }
        Assert.NotEqual(0, selectedSpell);
        string original = JsonSerializer.Serialize(source);
        MenuController menu = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = source }, catalog: catalog)) { Presentation = presentation };
        menu.Open(elemental ? "junction-element" : "junction-status");
        string prefix = elemental ? "elemental" : "status";
        string other = elemental ? "status" : "elemental";
        menu.Execute(new("junction-ui", "slot-" + (elemental ? "Elem-Atk" : "ST-Atk")));
        menu.SelectEntry("junction-dashboard", $"candidate-magic-0-{selectedSpell}");
        AssertTables();
        Assert.Throws<InvalidOperationException>(() => menu.Execute(new("junction-ui", prefix + "-defense")));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-dashboard", menu.ScreenId);
        menu.Execute(new("junction-ui", prefix + "-defense"));
        Assert.Contains(Descendants(menu.BuildView().Root).OfType<UiButton>(), button => button.Id == prefix + "-defense" && button.Text.StartsWith("●", StringComparison.Ordinal));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.DoesNotContain(Descendants(menu.BuildView().Root).Where(element => element.Id?.Contains("-readout-", StringComparison.Ordinal) == true).SelectMany(Descendants).OfType<UiLabel>(), label => label.Text.Contains('→'));

        void AssertTables()
        {
            foreach ((int width, int height) in new[] { (1280, 720), (1920, 1080), (2560, 1080) })
            {
                UiView view = menu.BuildView();
                view.Resize(width, height);
                UiElement[] all = Descendants(view.Root).ToArray();
                Assert.Equal(8, all.Count(element => element.Id?.StartsWith("elemental-readout-", StringComparison.Ordinal) == true));
                Assert.Equal(13, all.Count(element => element.Id?.StartsWith("status-readout-", StringComparison.Ordinal) == true));
                Assert.Equal(19, all.Count(element => element.Id?.StartsWith("slot-", StringComparison.Ordinal) == true));
                Assert.Contains(all, element => element.Id == $"candidate-magic-0-{selectedSpell}");
                Assert.All(all.Where(element => element.Id?.Contains("-readout-", StringComparison.Ordinal) == true), element => Assert.True(element.Bounds.Width > 0 && element.Bounds.Height > 0));
            }
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
