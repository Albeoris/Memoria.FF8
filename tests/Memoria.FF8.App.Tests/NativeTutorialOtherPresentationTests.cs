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
    public void SwitchLessonUsesRegularActionsAndRestoresThePendingExchange(MenuPresentation presentation)
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 8);
        MenuController menu = player.Menu;
        menu.Presentation = presentation;

        PlayThrough(player, 53);
        Assert.Equal("switch-party-target", menu.ScreenId);
        Assert.Equal("character-1", menu.BuildView().FocusedElement?.Id);
        Assert.Equal("party-switch", menu.SelectedEntry?.Action?.Kind);
        PlayThrough(player, 55);
        Assert.Equal("character-0", menu.BuildView().FocusedElement?.Id);
        PlayThrough(player, 63);
        Assert.Equal(new byte[] { 3, 0, 5 }, player.Party.CombatParty);

        PlayThrough(player, 166);
        Assert.Equal("switch-junction-target", menu.ScreenId);
        Assert.Equal("character-3", menu.BuildView().FocusedElement?.Id);
        Assert.Equal("junction-exchange", menu.SelectedEntry?.Action?.Kind);
        PlayThrough(player, 180);
        UiView view = menu.BuildView();
        Assert.Equal("switch-junction-confirm", menu.ScreenId);
        UiList commands = Assert.IsType<UiList>(view.FocusedElement);
        Assert.Equal(new[] { "No", "Yes" }, commands.Items);
        Assert.Equal("No", commands.Items[commands.SelectedIndex]);
        string prompt = menu.SystemText("junction.exchange.hp-warning", "Current HP will be reduced to the new maximum. Exchange junctions?");
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == prompt && !label.Bounds.IsEmpty);
        PlayThrough(player, 182);
        commands = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.Equal("Yes", commands.Items[commands.SelectedIndex]);
        Assert.Equal("switch-junction-confirm", menu.SelectedEntry?.Action?.Kind);

        JsonElement checkpoint = player.Capture();
        NativeTutorialPlayer restored = new(catalog, menus, 8, checkpoint);
        Assert.True(JsonElement.DeepEquals(checkpoint, restored.Capture()));

        NativeTutorialSnapshot saved = checkpoint.Deserialize<NativeTutorialSnapshot>()!;
        var originalRoster = catalog.CreateIsolatedParty(8);
        var oldParty = saved.Party with { Records = saved.Party.Records.Select((record, row) => record with { MemberFlags = originalRoster.Records[row].MemberFlags }).ToArray() };
        saved.Navigation.Selections["switch-junction-confirm"] = 0;
        saved.Settings.Add("Exchange source", "character-1");
        saved.Settings.Add("Exchange target", "character-3");
        NativeTutorialSnapshot legacy = saved with { Party = oldParty, Navigation = saved.Navigation with { History = saved.Navigation.History[1..] } };
        NativeTutorialPlayer restoredLegacy = new(catalog, menus, 8, JsonSerializer.SerializeToElement(legacy));
        Assert.True(JsonElement.DeepEquals(checkpoint, restoredLegacy.Capture()));

        JsonElement expectedParty = JsonSerializer.SerializeToElement(player.Party.ExchangeJunctions(1, 3));
        foreach (NativeTutorialPlayer continuation in new[] { player, restored, restoredLegacy })
        {
            PlayThrough(continuation, 184);
            Assert.Equal("switch-junction", continuation.Menu.ScreenId);
            Assert.True(JsonElement.DeepEquals(expectedParty, JsonSerializer.SerializeToElement(continuation.Party)));
        }
        JsonElement afterExchange = player.Capture();
        Assert.True(JsonElement.DeepEquals(afterExchange, new NativeTutorialPlayer(catalog, menus, 8, afterExchange).Capture()));
    }

    [Fact]
    public void StatusLessonOpensRegularStatsAndRenzokukenMoves()
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

        Assert.Equal(new[] { "status-stats", "status-info", "status-limit" }, player.Menu.Entries.Select(entry => entry.Id));
        PlayThrough(player, 19);
        Assert.Equal("status-stats", player.Menu.ScreenId);
        PlayThrough(player, 28);
        Assert.Equal("status", player.Menu.ScreenId);
        PlayThrough(player, 39);
        Assert.Equal("status-limit", player.Menu.ScreenId);
        PlayThrough(player, 75);
        Assert.Equal("status-renzokuken", player.Menu.ScreenId);
        Assert.All(player.Menu.Entries, entry => Assert.Null(entry.Action));
        PlayThrough(player, 91);
        Assert.Equal("finisher-1", player.Menu.SelectedEntry?.Id);
        MenuController regular = new(() => player.Menu.Data);
        regular.Open("status-renzokuken");
        regular.SelectEntry("status-renzokuken", "finisher-1");
        Assert.Equal(regular.Entries, player.Menu.Entries);
        Assert.Equal(regular.CurrentScreen.Details(regular), player.Menu.CurrentScreen.Details(player.Menu));
    }

    [Fact]
    public void DuelLessonShowsLearnedMovesInTheRegularStatusMenu()
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), 6);
        PlayThrough(player, 32);
        Assert.Equal("status-duel", player.Menu.ScreenId);
        Assert.Equal(System.Numerics.BitOperations.PopCount(catalog.CreateIsolatedLimitProgress(6).DuelFlags), player.Menu.Character!.LimitMoves.Count(move => move.Learned == true));
        Assert.All(player.Menu.Entries, entry => Assert.Null(entry.Action));
    }

    [Fact]
    public void AngeloLessonUsesRegularTrainingAndRestoresItsPendingConfirmation()
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 7);
        PlayThrough(player, 46);
        Assert.Equal("status-angelo", player.Menu.ScreenId);
        Assert.Equal(new[] { "limit-0", "limit-1", "limit-4" }, player.Menu.Entries.Select(entry => entry.Id));
        UiList commands = Assert.IsType<UiList>(player.Menu.BuildView().FocusedElement);
        Assert.Equal("angelo-abilities", commands.Id);
        Assert.Equal("0%", commands.SecondaryItems[1]);
        PlayThrough(player, 69);
        commands = Assert.IsType<UiList>(player.Menu.BuildView().FocusedElement);
        Assert.Equal("Angelo Recover", commands.Items[commands.SelectedIndex]);
        Assert.NotNull(commands.LeadingImages[1]);

        PlayThrough(player, 87);
        Assert.Equal("status-angelo-warning", player.Menu.ScreenId);
        Assert.Equal("no", player.Menu.SelectedEntry?.Id);
        JsonElement saved = player.Capture();
        NativeTutorialPlayer restored = new(catalog, menus, 7, saved);
        Assert.True(JsonElement.DeepEquals(saved, restored.Capture()));
        PlayThrough(restored, 105);
        Assert.Equal("status-angelo", restored.Menu.ScreenId);
        Assert.DoesNotContain(restored.Menu.Character!.LimitMoves, move => move.IsLearning);
        PlayThrough(restored, 109);
        Assert.Equal("limit-1", Assert.Single(restored.Menu.Character!.LimitMoves, move => move.IsLearning).Id);
    }

    [Fact]
    public void AngeloLessonRestoresAnEarlierSnapshotAtTheSameScriptPosition()
    {
        using GameAssets assets = ReadInstalledAssets();
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 7);
        PlayThrough(player, 87);
        NativeTutorialSnapshot current = player.Capture().Deserialize<NativeTutorialSnapshot>()!;
        NativeTutorialSnapshot earlier = current with
        {
            StatusProgress = null,
            Navigation = current.Navigation with { ScreenId = "status-angelo", History = ["status", "status-elements", "status-gf"], PendingAngeloMove = null },
            Settings = new(StringComparer.Ordinal) { ["Auto"] = "Off", ["Indicator"] = "On", ["Duel-Auto"] = "Off", ["Angelo learning"] = "1" }
        };

        NativeTutorialPlayer restored = new(catalog, menus, 7, JsonSerializer.SerializeToElement(earlier));
        Assert.Equal(current.ProgramCounter, restored.Capture().Deserialize<NativeTutorialSnapshot>()!.ProgramCounter);
        Assert.Equal("status-angelo-warning", restored.Menu.ScreenId);
        Assert.Equal("limit-1", Assert.Single(restored.Menu.Character!.LimitMoves, move => move.IsLearning).Id);
        PlayThrough(restored, 105);
        Assert.Equal("status-angelo", restored.Menu.ScreenId);
        Assert.DoesNotContain(restored.Menu.Character!.LimitMoves, move => move.IsLearning);
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
