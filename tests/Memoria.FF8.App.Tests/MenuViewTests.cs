using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class MenuViewTests
{
    [Theory]
    [InlineData(0u, "00:00")]
    [InlineData(59u, "00:00")]
    [InlineData(60u, "00:01")]
    [InlineData(3599u, "00:59")]
    [InlineData(3600u, "01:00")]
    [InlineData(45296u, "12:34")]
    [InlineData(90000u, "25:00")]
    public void MainMenuDisplaysSavedNativePlaytime(uint seconds, string expected)
    {
        GameState state = new() { Playtime = new NativePlaytimeState(seconds, 1234, 0) };
        MenuController menus = new(() => LiveMenuData.Build(state)) { Presentation = MenuPresentation.Classic };
        menus.Open();

        Assert.Equal(TimeSpan.FromSeconds(seconds), menus.Data.PlayTime);
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text.Split('\n').Contains($"PLAY  {expected}"));
        Assert.Equal(new NativePlaytimeState(seconds, 1234, 0), state.Playtime);
    }

    [Fact]
    public void MainMenuRefreshesPlaytimeFromAdvancedNativeClock()
    {
        GameState state = new() { Playtime = new NativePlaytimeState(3599, 0, 0) };
        MenuController menus = new(() => LiveMenuData.Build(state)) { Presentation = MenuPresentation.Classic };
        menus.Open();
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text.Split('\n').Contains("PLAY  00:59"));

        state = state with { Playtime = state.Playtime.AdvanceTo(60) };
        menus.InvalidateLiveData();

        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text.Split('\n').Contains("PLAY  01:00"));
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void JunctionCategoryLabelsFitTheirHeaderCellsAtEveryTextScale(int width, int height)
    {
        foreach (string textScale in new[] { "100%", "125%", "150%" })
        {
            MenuController menus = new() { Presentation = MenuPresentation.Classic };
            menus.Open("junction", true);
            menus.Data.Settings["Text scale"] = textScale;
            menus.PrepareViewport(width, height);
            UiView view = menus.BuildView();
            UiLabel[] headings = Descendants(view.Root).OfType<UiLabel>().Where(label => label.Text is "Stat" or "Value").ToArray();
            Assert.Equal(2, headings.Length);
            Assert.DoesNotContain(Descendants(view.Root).OfType<UiLabel>(), label => label.Text == "Stats");
            foreach (UiLabel label in headings)
            {
                Assert.True(SystemTextLayout.MeasureAdvance(label.Text, label.FontSize) <= label.Bounds.Width);
                Assert.True(label.FontSize <= label.Bounds.Height);
            }
        }
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void JunctionAliasesKeepAllDashboardGroupsAvailable(int width, int height)
    {
        foreach (string screen in new[] { "junction-options", "junction-off", "junction-auto", "junction-gf" })
        {
            MenuController menus = new() { Presentation = MenuPresentation.Classic };
            menus.Open(screen, true);
            menus.PrepareViewport(width, height);
            UiView view = menus.BuildView();
            UiElement[] elements = Descendants(view.Root).ToArray();
            Assert.Equal("junction-dashboard", menus.ScreenId);
            Assert.Contains(elements, element => element.Id == "junction-character-portrait");
            foreach (string slot in new[] { "HP", "Str", "Luck", "Elem-Atk", "ST-Atk" })
            {
                Assert.Contains(elements, element => element.Id == "slot-" + slot);
            }
            Assert.Contains(elements, element => element.Id == "command-0");
            Assert.Contains(elements, element => element.Id == "passive-0");
            Assert.Contains(elements.OfType<UiLabel>(), label => label.Text.EndsWith("Actions", StringComparison.Ordinal));
            Assert.DoesNotContain(elements.OfType<UiLabel>(), label => label.Text.EndsWith("Assign", StringComparison.Ordinal) || label.Text.EndsWith("Close", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    public void ItemPartyNamesOccupySeparateRowsBesideInventory(int width, int height)
    {
        MenuController menus = new();
        menus.Open("item-use", true);
        UiView view = menus.BuildView();
        view.Resize(width, height);
        UiElement[] elements = Descendants(view.Root).ToArray();
        Assert.DoesNotContain(elements.OfType<UiImage>(), image => image.Source?.Kind == UiImageKind.CharacterPortrait);
        UiList inventory = Assert.Single(elements.OfType<UiList>(), list => list.Id == "commands");
        Assert.Equal(menus.Data.Items.Count, inventory.Items.Count);
        MenuCharacter[] party = menus.Data.Characters.Where(character => character.InParty).ToArray();
        UiLabel[] names = party.Select(character => Assert.Single(elements.OfType<UiLabel>(), label => label.Text == character.Name)).ToArray();
        Assert.Equal(party.Length, names.Length);
        Assert.All(names, name => Assert.True(name.Bounds.X > inventory.Bounds.Right));
        for (int index = 1; index < names.Length; index++)
        {
            Assert.True(names[index].Bounds.Y >= names[index - 1].Bounds.Bottom);
        }
    }

    [Theory]
    [InlineData("main", UiImageKind.CharacterPortrait)]
    [InlineData("junction", UiImageKind.CharacterPortrait)]
    [InlineData("status", UiImageKind.CharacterPortrait)]
    [InlineData("magic", UiImageKind.CharacterPortrait)]
    [InlineData("gf", UiImageKind.GuardianForcePortrait)]
    [InlineData("gf-learn", UiImageKind.GuardianForcePortrait)]
    [InlineData("card", UiImageKind.Card)]
    [InlineData("card-detail", UiImageKind.Card)]
    public void Menu_views_draw_native_foreground_artwork(string screen, UiImageKind kind)
    {
        MenuController menus = new();
        menus.Open(screen, true);
        UiView view = menus.BuildView();
        view.Resize(1280, 720);
        ValidationCanvas canvas = new();
        view.Draw(canvas, new UiTheme());

        Assert.Contains(canvas.Images, source => source.Kind == kind);
        Assert.DoesNotContain(Descendants(view.Root), element => element is UiSigil);
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Every_registered_view_draws_with_balanced_clipping_at_small_and_large_viewports()
    {
        foreach (string screen in new MenuController().ScreenIds)
        {
            var menus = new MenuController();
            menus.Open(screen, true);
            if (screen == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(menus);
            }
            foreach (var (width, height) in new[] { (640, 448), (1920, 1080) })
            {
                UiView view = menus.BuildView();
                view.Resize(width, height);
                var canvas = new ValidationCanvas();
                view.Draw(canvas, new UiTheme());
                Assert.Equal(0, canvas.ClipDepth);
                Assert.True(canvas.TextCount > 0, screen);
            }
        }
    }

    [Fact]
    public void Focused_portrait_tiles_remain_clickable_and_confirm_uses_the_selected_tile()
    {
        var menus = new MenuController();
        menus.Open("gf", true);
        UiView first = menus.BuildView();
        first.Resize(960, 672);
        var tile = first.Root.Children.SelectMany(Descendants).OfType<UiButton>().First();
        first.Pointer(tile.Bounds.X + tile.Bounds.Width / 2, tile.Bounds.Y + tile.Bounds.Height / 2, true);
        Assert.Equal("gf-learn", menus.ScreenId);
        menus.Open("gf", true);
        menus.Navigate(MenuInput.Right);
        UiView second = menus.BuildView();
        second.Resize(960, 672);
        Assert.Equal(menus.Entries[1].Id, second.FocusedElement?.Id);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("Shiva", menus.GuardianForce?.Name);
    }

    [Fact]
    public void MainShowsNativePortraitsForActiveAndReserveCharacters()
    {
        MenuController menus = new();
        menus.Open("main", true);
        UiImage[] portraits = Descendants(menus.BuildView().Root).OfType<UiImage>().Where(image => image.Source?.Kind == UiImageKind.CharacterPortrait).ToArray();
        Assert.Equal(menus.Data.Characters.Count, portraits.Length);
        foreach (MenuCharacter character in menus.Data.Characters)
        {
            UiImage portrait = Assert.Single(portraits, image => image.Id == "portrait-" + character.Id);
            Assert.Equal(character.NativeId, portrait.Source?.Id);
            Assert.True(portrait.Bounds.Width > 0 && portrait.Bounds.Height > 0);
        }
        Assert.Contains(menus.Data.Characters, character => character.InParty);
        Assert.Contains(menus.Data.Characters, character => !character.InParty);
    }

    [Fact]
    public void CardDownSelectsNextTextRowAndOpensThatCard()
    {
        MenuController menus = new();
        menus.Open("card", true);
        string expected = menus.Entries[1].Id;
        menus.BuildView();
        menus.Navigate(MenuInput.Down);
        Assert.Equal(1, menus.SelectedIndex);
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("card-detail", menus.ScreenId);
        Assert.Equal(expected, menus.Card?.Id);
    }

    [Fact]
    public void JunctionDashboardExposesAllDestinationsAndCardActions()
    {
        MenuController menus = new();
        menus.Open("junction", true);
        Assert.Equal("junction-dashboard", menus.ScreenId);
        foreach (string control in new[] { "slot-HP", "slot-Elem-Atk", "slot-ST-Atk", "command-0", "passive-0" })
        {
            menus.SelectEntry("junction-dashboard", control);
            Assert.Equal(control, menus.BuildView().FocusedElement?.Id);
        }
        menus.Navigate(MenuInput.Alternate);
        foreach (string action in new[] { "optimize", "clear-magic", "remove-junctions", "clear-party-magic" })
        {
            Assert.Contains(menus.Entries, entry => entry.Id == action);
        }
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void JunctionAliasesKeepCommandsAbovePassivesAndOpenTheirPicker(int width, int height)
    {
        foreach (string screen in new[] { "junction-command", "junction-ability", "junction-command-choice", "junction-ability-choice" })
        {
            MenuController menus = new();
            menus.Open(screen, true);
            menus.PrepareViewport(width, height);
            UiView view = menus.BuildView();
            UiElement[] elements = Descendants(view.Root).ToArray();
            UiElement command = Assert.Single(elements, element => element.Id == "command-0");
            UiElement passive = Assert.Single(elements, element => element.Id == "passive-0");
            Assert.True(command.Bounds.Bottom <= passive.Bounds.Y);
            string target = screen.Contains("command", StringComparison.Ordinal) ? "command-0" : "passive-0";
            menus.SelectEntry("junction-dashboard", target);
            menus.Navigate(MenuInput.Confirm);
            Assert.StartsWith("candidate-", menus.BuildView().FocusedElement?.Id);
            Assert.Equal("junction-dashboard", menus.ScreenId);
            Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiButton>(), button => button.Id?.StartsWith("candidate-", StringComparison.Ordinal) == true);
        }
    }

    [Fact]
    public void JunctionAbilitySlotNavigationCrossesGroupsAndCancelReturnsToJunction()
    {
        MenuController menus = new();
        menus.Open("junction", true);
        menus.SelectEntry("junction-dashboard", "command-2");
        menus.Navigate(MenuInput.Down);
        Assert.Equal("passive-0", menus.BuildView().FocusedElement?.Id);
        menus.Navigate(MenuInput.Up);
        Assert.Equal("command-2", menus.BuildView().FocusedElement?.Id);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.StartsWith("candidate-", menus.BuildView().FocusedElement?.Id);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-dashboard", menus.ScreenId);
        Assert.Equal("passive-0", menus.BuildView().FocusedElement?.Id);
    }

    [Theory]
    [InlineData("junction-command")]
    [InlineData("junction-ability")]
    public void JunctionCandidatePreviewDoesNotEquipBeforeSelectingASlot(string screen)
    {
        MenuController menus = new();
        menus.Open(screen, true);
        string[] commands = menus.Character!.Commands.ToArray();
        string[] abilities = menus.Character.Abilities.ToArray();
        Assert.DoesNotContain(menus.Entries, entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal));
        menus.SelectEntry("junction-dashboard", screen == "junction-command" ? "command-0" : "passive-0");
        menus.Navigate(MenuInput.Confirm);
        UiButton candidate = Descendants(menus.BuildView().Root).OfType<UiButton>().First(button => button.Id?.StartsWith("candidate-", StringComparison.Ordinal) == true);
        candidate.Hover?.Invoke();
        Assert.Equal("junction-dashboard", menus.ScreenId);
        Assert.Equal(commands, menus.Character.Commands);
        Assert.Equal(abilities, menus.Character.Abilities);
    }

    [Theory]
    [InlineData("junction-command", "junction-ability-choice", 0)]
    [InlineData("junction-ability", "junction-command-choice", 2)]
    public void JunctionInactiveSlotClickSelectsItsGroupAndOpensCandidates(string screen, string destination, int row)
    {
        MenuController menus = new();
        menus.Open(screen, true);
        menus.PrepareViewport(1280, 720);
        UiView view = menus.BuildView();
        string target = destination == "junction-command-choice" ? "command-" + row : "passive-" + row;
        UiElement button = Assert.Single(Descendants(view.Root), element => element.Id == target);
        view.Pointer(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2, true);
        Assert.Equal("junction-dashboard", menus.ScreenId);
        Assert.Equal(target, menus.CaptureJunctionDashboard().Origin);
        Assert.StartsWith("candidate-", menus.BuildView().FocusedElement?.Id);
    }

    [Theory]
    [InlineData("item-use", "item-target")]
    [InlineData("magic-use", "magic-target")]
    public void InventoryQuantityClickSelectsTheMatchingStock(string screen, string destination)
    {
        MenuController menus = new();
        menus.Open(screen, true);
        string expected = menus.Entries[1].Id;
        UiView view = menus.BuildView();
        UiList quantities = Assert.Single(Descendants(view.Root).OfType<UiList>(), list => list.Id == "commands");
        view.Pointer(quantities.Bounds.Right - 30, quantities.Bounds.Y + quantities.RowHeight * 1.5f, true);
        Assert.Equal(destination, menus.ScreenId);
        Assert.Equal(expected, screen == "item-use" ? menus.Item?.Id : menus.Magic?.Id);
    }

    [Fact]
    public void DemoMagicDiscardsCharacterSelectionFromDifferentLiveRoster()
    {
        MenuData live = new();
        live.Characters.Add(new MenuCharacter { Id = "character-0", NativeId = 0, Name = "Squall", InParty = true });
        MenuController menus = new(() => live);
        menus.Open("main");
        menus.Execute(new MenuAction("select-character", "character-0"));
        Assert.Equal("character-0", menus.Character?.Id);

        menus.Open("magic-use", true);
        MenuCharacter selected = Assert.IsType<MenuCharacter>(menus.Character);
        Assert.Contains(menus.Data.Characters, character => character.Id == selected.Id);
        Assert.NotEmpty(menus.Entries);
        UiView view = menus.BuildView();
        ValidationCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.True(canvas.TextCount > 0);
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void EveryRegisteredLiveScreenBuildsAndDrawsWithoutProjectedData()
    {
        foreach (string screen in new MenuController().ScreenIds)
        {
            MenuController menus = new(() => new MenuData());
            menus.Open(screen);
            if (screen == "magazine")
            {
                MagazineMenuFixture.AddOwnedPage(menus);
            }
            UiView view = menus.BuildView();
            ValidationCanvas canvas = new();
            view.Draw(canvas, new UiTheme());
            Assert.Equal(0, canvas.ClipDepth);
            Assert.True(canvas.TextCount > 0, screen);
        }
    }

    [Fact]
    public void MainAccommodatesEightReservePortraits()
    {
        MenuData data = new();
        for (int index = 0; index < 11; index++)
        {
            data.Characters.Add(new MenuCharacter { Id = "character-" + index, NativeId = index, Name = "Character " + index, InParty = index < 3 });
        }
        MenuController menus = new(() => data);
        menus.Open("main");
        UiView view = menus.BuildView();
        UiImage[] portraits = Descendants(view.Root).OfType<UiImage>().Where(image => image.Source?.Kind == UiImageKind.CharacterPortrait).ToArray();
        Assert.Equal(11, portraits.Length);
        Assert.All(portraits, portrait => Assert.True(portrait.Bounds.Width > 0 && portrait.Bounds.Height > 0));
        foreach (MenuCharacter character in data.Characters)
        {
            Assert.Single(portraits, portrait => portrait.Id == "portrait-" + character.Id);
        }
        ValidationCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Equal(11, canvas.Images.Count(source => source.Kind == UiImageKind.CharacterPortrait));
        Assert.Equal(0, canvas.ClipDepth);
    }

    [Fact]
    public void Mods_can_supply_controls_and_are_discovered_in_the_gallery()
    {
        var menus = new MenuController();
        bool invoked = false;
        menus.Register(new MenuScreenDefinition
        {
            Id = "mod-settings",
            GalleryEligible = true,
            Title = "Mod settings",
            Help = "Custom controls",
            MainCommand = true,
            BuildContent = (_, root) => root.Children.Add(new UiButton { Id = "mod-action", Width = 300, Height = 60, HorizontalAlignment = UiAlignment.Center, VerticalAlignment = UiAlignment.Center, Text = "Apply", Click = () => invoked = true })
        });
        menus.Open("debug-gallery", true);
        Assert.Contains(menus.Entries, entry => entry.Destination == "mod-settings");
        menus.Open("main", true);
        Assert.Contains(menus.Entries, entry => entry.Destination == "mod-settings");
        menus.Open("mod-settings", true);
        menus.BuildView().Resize(960, 672);
        menus.Navigate(MenuInput.Confirm);
        Assert.True(invoked);
    }

    private static void ActivateEntry(MenuController menus, int index)
    {
        for (int attempts = 0; menus.SelectedIndex != index && attempts < menus.Entries.Count; attempts++)
        {
            menus.Navigate(menus.BuildView().FocusedElement is UiList { Orientation: UiOrientation.Horizontal } ? MenuInput.Right : MenuInput.Down);
        }
        Assert.Equal(index, menus.SelectedIndex);
        menus.BuildView();
        menus.Navigate(MenuInput.Confirm);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class ValidationCanvas : IUiCanvas
    {
        public int ClipDepth { get; private set; }
        public int TextCount { get; private set; }
        public List<UiImageSource> Images { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            var measured = SystemTextLayout.MeasureWrapped(text, fontSize, maximumWidth);
            return new UiSize(measured.X, measured.Y);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Validate(bounds);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Validate(bounds);
            Assert.True(float.IsFinite(fontSize) && fontSize > 0);
            TextCount++;
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Validate(bounds);
            Images.Add(source);
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Assert.True(float.IsFinite(x1) && float.IsFinite(y1) && float.IsFinite(x2) && float.IsFinite(y2));
            Assert.True(float.IsFinite(thickness) && thickness > 0);
        }

        public void PushClip(UiRect bounds)
        {
            Validate(bounds);
            ClipDepth++;
        }

        public void PopClip()
        {
            ClipDepth--;
            Assert.True(ClipDepth >= 0);
        }

        private static void Validate(UiRect bounds)
        {
            Assert.True(float.IsFinite(bounds.X) && float.IsFinite(bounds.Y) && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height));
            Assert.True(bounds.Width >= 0 && bounds.Height >= 0);
        }
    }
}
