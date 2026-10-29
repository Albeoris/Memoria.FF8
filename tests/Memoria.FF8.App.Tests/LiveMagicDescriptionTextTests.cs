using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class LiveMagicDescriptionTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectedDescriptionPreservesNativeTextAndUsesOneCleanAccessSeparator(bool shared)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native menu resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        int cure = Enumerable.Range(1, 56).Single(id => catalog.MagicName(id) == "Cure");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().ChangeMagicStock(0, cure, 10, MagicOperationReason.ScriptReward, out _);
        vitals = MagicModeCoordinator.Preview(vitals, new GameplayPreferences { SharedMagicStock = shared }).Vitals;
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals };

        MenuMagic magic = LiveMenuData.Build(state, catalog: catalog).Magic.Single(magic => magic.Id == $"magic-0-{cure}");

        Assert.Equal("Restore HP", catalog.MagicDescription(cure));
        Assert.Equal(shared ? "Unbound · Caster: Squall" : "", magic.AccessPresentation);
        Assert.Equal(shared ? "Restore HP · Unbound · Caster: Squall" : "Restore HP", magic.Description);
    }
}
