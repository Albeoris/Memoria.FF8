using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PartyCharacterSelectionTests
{
    [Theory]
    [InlineData("junction")]
    [InlineData("magic")]
    [InlineData("status")]
    public void KeyboardReachesReserveCharactersAfterPartyAndWrapsWithoutFillingEmptyPartySlots(string destination)
    {
        MenuData data = ReorderedLiveData();
        MenuController controller = new(() => data);
        controller.Open();
        SelectMainCommand(controller, destination);
        controller.Navigate(MenuInput.Confirm);
        controller.Navigate(MenuInput.Up);
        Assert.Equal("0", controller.BuildView().FocusedElement!.Id);
        Assert.DoesNotContain(Descendants(controller.View!.Root).OfType<UiLabel>(), label => label.Text == "Empty party slot");
        UiPanel emptyPartySlot = Assert.Single(Descendants(controller.View.Root).OfType<UiPanel>(), panel => panel.Id == "party-status-2");
        Assert.DoesNotContain(Descendants(emptyPartySlot).OfType<UiLabel>(), label => label.Text == "Renamed reserve");
        controller.Navigate(MenuInput.Down);
        Assert.Equal("7", controller.BuildView().FocusedElement!.Id);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Down);
        Assert.Equal("0", controller.BuildView().FocusedElement!.Id);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(destination, controller.ScreenId);
        Assert.Equal("0", controller.Character!.Id);
    }

    [Fact]
    public void ReserveOnlyRosterStillAllowsCharacterSelection()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "3", NativeId = 3, Name = "Quistis", InParty = false });
        MenuController controller = new(() => data);
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("3", controller.BuildView().FocusedElement!.Id);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction", controller.ScreenId);
        Assert.Equal("3", controller.Character!.Id);
    }

    [Theory]
    [InlineData("junction", false)]
    [InlineData("magic", false)]
    [InlineData("status", false)]
    [InlineData("junction", true)]
    [InlineData("magic", true)]
    [InlineData("status", true)]
    public void MainCharacterCommandsSelectACharacterUnlessMagicStockIsShared(string destination, bool sharedMagic)
    {
        MenuData data = ReorderedLiveData();
        data.SharedMagicStock = sharedMagic;
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, requests.Add);
        controller.Open();
        SelectMainCommand(controller, destination);
        int commandIndex = controller.SelectedIndex;
        controller.Navigate(MenuInput.Confirm);

        if (destination == "magic" && sharedMagic)
        {
            Assert.Equal("magic", controller.ScreenId);
            Assert.False(controller.IsSelectingCharacter);
            Assert.Equal("7", controller.Character!.Id);
            Assert.Empty(requests);
            controller.BuildView();
            controller.Navigate(MenuInput.Cancel);
            Assert.Equal("main", controller.ScreenId);
            Assert.True(controller.IsOpen);
            Assert.Equal(commandIndex, controller.SelectedIndex);
            return;
        }

        Assert.Equal("main", controller.ScreenId);
        Assert.True(controller.IsSelectingCharacter);
        Assert.Equal(destination, controller.CharacterSelectionDestination);
        Assert.Equal(commandIndex, controller.SelectedIndex);
        Assert.Equal(new[] { "7", "2", "0" }, controller.CharacterChoices.Select(character => character.Id));
        Assert.Equal("Renamed leader", controller.CharacterChoices[0].Name);

        controller.Navigate(MenuInput.Down);
        Assert.Equal(1, controller.CharacterSelectionIndex);
        Assert.Equal("7", controller.Character!.Id);
        controller.Navigate(MenuInput.Confirm);

        Assert.Equal(destination, controller.ScreenId);
        Assert.False(controller.IsSelectingCharacter);
        Assert.Equal("2", controller.Character!.Id);
        Assert.Equal("Renamed second", controller.Character.Name);
        Assert.Empty(requests);
        controller.Back();
        Assert.Equal("main", controller.ScreenId);
        Assert.Equal(commandIndex, controller.SelectedIndex);
    }

    [Fact]
    public void CancelRestoresMainCommandWithoutChangingCharacterOrWritingLiveState()
    {
        MenuData data = ReorderedLiveData();
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, requests.Add);
        controller.Open();
        controller.Execute(new MenuAction("select-character", "2"));
        SelectMainCommand(controller, "junction");
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(1, controller.CharacterSelectionIndex);
        controller.Navigate(MenuInput.Up);
        Assert.Equal(0, controller.CharacterSelectionIndex);
        controller.Navigate(MenuInput.Cancel);

        Assert.True(controller.IsOpen);
        Assert.False(controller.IsSelectingCharacter);
        Assert.Equal("main", controller.ScreenId);
        Assert.Equal("junction", controller.SelectedEntry!.Id);
        Assert.Equal("2", controller.Character!.Id);
        Assert.Equal("commands", controller.BuildView().FocusedElement!.Id);
        Assert.Empty(requests);
        controller.Navigate(MenuInput.Cancel);
        Assert.False(controller.IsOpen);
    }

    [Fact]
    public void ChosenNativeIdRemainsTheJunctionOwnerAcrossProviderReordering()
    {
        MenuData data = ReorderedLiveData();
        data.SupportedActions.UnionWith(["junction-gf", "junction-command", "junction-magic", "junction-ability"]);
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, action =>
        {
            requests.Add(action);
            data.Characters.Reverse();
        });
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        controller.ChooseCharacter("2");
        Assert.Equal("junction", controller.ScreenId);
        controller.Execute(new MenuAction("junction-gf", "0"));
        controller.Execute(new MenuAction("select-command-slot", "3"));
        controller.Execute(new MenuAction("junction-command", "4"));
        controller.Execute(new MenuAction("select-slot", "Mag"));
        controller.Execute(new MenuAction("junction-magic", "1"));
        controller.Execute(new MenuAction("select-ability-slot", "1"));
        controller.Execute(new MenuAction("junction-ability", "20"));

        Assert.Equal(4, requests.Count);
        Assert.All(requests, action => Assert.Equal("2", action.TargetId));
        Assert.Equal("2", requests[1].Value);
        Assert.Equal("Mag", requests[2].Value);
        Assert.Equal("1", requests[3].Value);
        Assert.Equal("2", controller.Character!.Id);
    }

    [Fact]
    public void PartyPaneFocusTracksTheCharacterCursorAndConfirmUsesTheFocusedPane()
    {
        MenuData data = ReorderedLiveData();
        MenuController controller = new(() => data);
        controller.Open();
        controller.BuildView();
        controller.Navigate(MenuInput.Confirm);
        UiView first = controller.BuildView();
        Assert.Equal("7", first.FocusedElement!.Id);
        Assert.False(Descendants(first.Root).OfType<UiList>().Single(list => list.Id == "commands").Enabled);
        controller.Navigate(MenuInput.Down);
        UiView second = controller.BuildView();
        Assert.Equal("2", second.FocusedElement!.Id);
        UiButton leader = Descendants(second.Root).OfType<UiButton>().Single(button => button.Id == "7");
        Assert.True(second.Focus(leader));
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction", controller.ScreenId);
        Assert.Equal("7", controller.Character!.Id);
    }

    [Theory]
    [InlineData(640, 448, "0")]
    [InlineData(960, 672, "2")]
    [InlineData(1280, 720, "2")]
    [InlineData(1280, 896, "0")]
    [InlineData(1920, 1080, "0")]
    public void ClickingACharacterPaneSelectsThatExactCharacter(int width, int height, string characterId)
    {
        MenuData data = ReorderedLiveData();
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, requests.Add);
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        UiView view = controller.BuildView();
        view.Resize(width, height);
        UiButton choice = Descendants(view.Root).OfType<UiButton>().Single(button => button.Id == characterId);
        float scale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
        float offsetX = (width - view.DesignWidth * scale) / 2;
        float offsetY = (height - view.DesignHeight * scale) / 2;
        view.Pointer(offsetX + (choice.Bounds.X + choice.Bounds.Width / 2) * scale, offsetY + (choice.Bounds.Y + choice.Bounds.Height / 2) * scale, true);
        Assert.Equal("junction", controller.ScreenId);
        Assert.Equal(characterId, controller.Character!.Id);
        Assert.Empty(requests);
    }

    [Fact]
    public void PickerRejectsEmptyAndStaleChoicesAndOpenResetsTheMode()
    {
        MenuData data = ReorderedLiveData();
        MenuController controller = new(() => data);
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        controller.ChooseCharacter("");
        controller.ChooseCharacter("255");
        Assert.True(controller.IsSelectingCharacter);
        data.Characters.RemoveAll(character => character.Id == "2");
        controller.ChooseCharacter("2");
        Assert.True(controller.IsSelectingCharacter);
        Assert.Equal("main", controller.ScreenId);
        controller.Open("status");
        Assert.False(controller.IsSelectingCharacter);
        controller.ChooseCharacter("7");
        Assert.Equal("status", controller.ScreenId);
    }

    [Fact]
    public void MissingCharactersDoNotOpenACharacterScreen()
    {
        MenuData data = new();
        MenuController controller = new(() => data);
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("main", controller.ScreenId);
        Assert.False(controller.IsSelectingCharacter);
        Assert.Equal("No characters are available.", controller.Message);
    }

    [Fact]
    public void ExplicitCharacterIdentityDoesNotFallBackToAnotherCharacterAfterRefresh()
    {
        MenuData data = ReorderedLiveData();
        data.SupportedActions.Add("junction-gf");
        List<MenuAction> requests = [];
        MenuController controller = new(() => data, action =>
        {
            requests.Add(action);
            data.Characters.RemoveAll(character => character.Id == "2");
        });
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        controller.ChooseCharacter("2");
        controller.Execute(new MenuAction("junction-gf", "0"));
        Assert.Null(controller.Character);
        controller.Execute(new MenuAction("junction-gf", "0"));
        Assert.Single(requests);
        Assert.Equal("Selected character data is unavailable.", controller.Message);
    }

    private static void SelectMainCommand(MenuController controller, string id)
    {
        while (controller.SelectedEntry?.Id != id)
        {
            controller.Navigate(MenuInput.Down);
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

    private static MenuData ReorderedLiveData()
    {
        MenuData data = new();
        data.Characters.AddRange([
            new MenuCharacter { Id = "0", NativeId = 0, Name = "Renamed reserve", InParty = false },
            new MenuCharacter { Id = "7", NativeId = 7, Name = "Renamed leader", InParty = true },
            new MenuCharacter { Id = "", Name = "Empty party slot", InParty = true },
            new MenuCharacter { Id = "2", NativeId = 2, Name = "Renamed second", InParty = true }
        ]);
        return data;
    }
}
