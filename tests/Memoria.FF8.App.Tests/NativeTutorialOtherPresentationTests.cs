using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialOtherPresentationTests
{
    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void SwitchLessonFocusesActualTargetsAndShowsTheExchangePrompt(MenuPresentation presentation)
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialPlayer player = new(assets.ReadNativeTutorialCatalog(), LiveMenuCatalog.ReadInstalled(assets), 8);
        MenuController menu = player.Menu;
        menu.Presentation = presentation;

        PlayThrough(player, 53);
        Assert.Equal("switch-party-target", menu.ScreenId);
        Assert.Equal("character-1", menu.BuildView().FocusedElement?.Id);
        PlayThrough(player, 55);
        Assert.Equal("character-0", menu.BuildView().FocusedElement?.Id);
        PlayThrough(player, 63);
        Assert.Equal(new byte[] { 3, 0, 5 }, player.Party.CombatParty);

        PlayThrough(player, 166);
        Assert.Equal("switch-junction-target", menu.ScreenId);
        Assert.Equal("character-3", menu.BuildView().FocusedElement?.Id);
        PlayThrough(player, 180);
        UiView view = menu.BuildView();
        Assert.Equal("switch-junction-confirm", menu.ScreenId);
        UiList commands = Assert.IsType<UiList>(view.FocusedElement);
        Assert.Equal(new[] { "Yes", "No" }, commands.Items);
        Assert.Equal("No", commands.Items[commands.SelectedIndex]);
        string prompt = Assert.Single(menu.CurrentScreen.Details(menu));
        Assert.NotEmpty(prompt);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == prompt && !label.Bounds.IsEmpty);
        PlayThrough(player, 182);
        commands = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.Equal("Yes", commands.Items[commands.SelectedIndex]);
    }

    [Fact]
    public void StatusLessonShowsTheEffectiveIndicatorWhileKeepingItsManualSetting()
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 5);
        Assert.Equal(new[] { "21", "22", "23" }, player.Menu.Character!.CommandIds);
        Assert.Equal(new[] { "Attack", "GF", "Draw", "Item" }, player.Menu.Character.Commands);

        NativeTutorialSnapshot earlier = player.Capture().Deserialize<NativeTutorialSnapshot>()! with { Party = catalog.CreateIsolatedParty(5) };
        NativeTutorialPlayer restored = new(catalog, menus, 5, JsonSerializer.SerializeToElement(earlier));
        Assert.Equal(player.Menu.Character.Commands, restored.Menu.Character!.Commands);
        Assert.True(JsonElement.DeepEquals(player.Capture(), restored.Capture()));

        PlayThrough(player, 75);
        AssertIndicator(player.Menu, "Off", enabled: false);
        PlayThrough(player, 91);
        AssertIndicator(player.Menu, "On", enabled: true);
        PlayThrough(player, 116);
        AssertIndicator(player.Menu, "Off", enabled: true);
        PlayThrough(player, 132);
        AssertIndicator(player.Menu, "Off", enabled: false);
    }

    [Fact]
    public void AngeloLessonShowsFocusedTrickAndNativeTrainingGauges()
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialPlayer player = new(assets.ReadNativeTutorialCatalog(), LiveMenuCatalog.ReadInstalled(assets), 7);
        PlayThrough(player, 46);
        UiView view = player.Menu.BuildView();
        UiList commands = Assert.IsType<UiList>(view.FocusedElement);
        Assert.Equal("Angelo Recover", commands.Items[commands.SelectedIndex]);
        Assert.True(commands.VisibleRows >= commands.Items.Count);
        UiProgressBar[] gauges = Descendants(view.Root).OfType<UiProgressBar>().ToArray();
        Assert.Equal(new[] { 64f, 0f, 64f }, gauges.Select(gauge => gauge.Value));
        Assert.All(gauges, gauge => Assert.False(gauge.Bounds.IsEmpty));

        PlayThrough(player, 69);
        commands = Assert.IsType<UiList>(player.Menu.BuildView().FocusedElement);
        Assert.NotNull(commands.LeadingImages[1]);
    }

    [Fact]
    public void GuardianForceLessonLocalizesInstructionsForTheSharedLearnScreen()
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialPlayer player = new(assets.ReadNativeTutorialCatalog(), LiveMenuCatalog.ReadInstalled(assets), 4);
        const string translated = "Translated current GF instructions";
        player.Menu.ConnectLocalizationText((key, fallback) => key == SystemTextCatalog.Key("tutorial.gf.learning-screen") ? translated : fallback);

        PlayThrough(player, 94);
        UiView view = player.Menu.BuildView();
        Assert.Equal("gf-learn", player.Menu.ScreenId);
        Assert.Equal("gf-1", player.Menu.GuardianForce?.Id);
        Assert.Equal(translated, Assert.Single(Descendants(view.Root).OfType<UiTextPopup>()).Text);
    }

    private static GameAssets ReadInstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }

    private static void AssertIndicator(MenuController menu, string value, bool enabled)
    {
        MenuEntry indicator = Assert.Single(menu.Entries, entry => entry.Id == "Indicator");
        Assert.EndsWith(value, indicator.Label);
        Assert.Equal(enabled, indicator.Enabled);
    }

    private static void PlayThrough(NativeTutorialPlayer player, int position)
    {
        bool reached = false;
        void OnOperation(int pc, ushort word)
        {
            reached |= pc == position;
        }
        player.OperationExecuted += OnOperation;
        try
        {
            for (int tick = 0; tick < 10000 && !reached && !player.IsComplete; tick++)
            {
                player.Apply(new PlayerAction(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait));
            }
            Assert.True(reached, $"The lesson did not execute instruction {position}.");
        }
        finally
        {
            player.OperationExecuted -= OnOperation;
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
