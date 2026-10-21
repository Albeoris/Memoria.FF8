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
        MenuController menus = new(() => LiveMenuData.Build(state));
        menus.Open();

        Assert.Equal(TimeSpan.FromSeconds(seconds), menus.Data.PlayTime);
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text == $"PLAY  {expected}");
        Assert.Equal(new NativePlaytimeState(seconds, 1234, 0), state.Playtime);
    }

    [Fact]
    public void MainMenuRefreshesPlaytimeFromAdvancedNativeClock()
    {
        GameState state = new() { Playtime = new NativePlaytimeState(3599, 0, 0) };
        MenuController menus = new(() => LiveMenuData.Build(state));
        menus.Open();
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text == "PLAY  00:59");

        state = state with { Playtime = state.Playtime.AdvanceTo(60) };
        menus.InvalidateLiveData();

        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiLabel>(), label => label.Text == "PLAY  01:00");
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void JunctionCategoryLabelsFitTheirHeaderCellsAtEveryTextScale(int width, int height)
    {
        foreach (string textScale in new[] { "100%", "125%", "150%" })
        {
            foreach (string screen in new[] { "junction-options", "junction-off", "junction-auto" })
            {
                MenuController menus = new();
                menus.Open(screen, true);
                menus.Data.Settings["Text scale"] = textScale;
                UiView view = menus.BuildView();
                view.Resize(width, height);
                UiList commands = Assert.Single(Descendants(view.Root).OfType<UiList>(), list => list.Id == "commands");
                float textWidth = commands.Bounds.Width / commands.Items.Count - commands.TextInset - 4;
                foreach (string label in commands.Items)
                {
                    float advance = SystemTextLayout.MeasureAdvance(label, commands.FontSize);
                    var measured = SystemTextLayout.MeasureWrapped(label, commands.FontSize, textWidth);
                    Assert.True(advance <= textWidth, $"{screen} at {textScale}: '{label}' exceeds its header cell width ({advance} > {textWidth}).");
                    Assert.True(measured.Y <= commands.Bounds.Height, $"{screen} at {textScale}: '{label}' exceeds its header cell height ({measured.Y} > {commands.Bounds.Height}).");
                }
            }
        }
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void JunctionSubmenusKeepStatsVisibleWithCategoriesAboveAndEntitiesOnRight(int width, int height)
    {
        string[] categories = ["junction-options", "junction-off", "junction-auto"];
        string[] entities = ["junction-gf"];
        foreach (string screen in categories.Concat(entities))
        {
            MenuController menus = new();
            menus.Open(screen, true);
            UiView view = menus.BuildView();
            view.Resize(width, height);
            UiElement[] elements = Descendants(view.Root).ToArray();
            UiElement stats = Assert.Single(elements, element => element.Id == "junction-stats");
            UiList commands = Assert.Single(elements.OfType<UiList>(), list => list.Id == "commands");
            UiImage portrait = Assert.Single(elements.OfType<UiImage>(), image => image.Source?.Kind == UiImageKind.CharacterPortrait);
            Assert.True(commands.Bounds.Bottom <= stats.Bounds.Y);
            Assert.True(stats.Bounds.Bottom <= view.DesignHeight);
            Assert.Contains(Descendants(stats).OfType<UiLabel>(), label => label.Text == "Str");
            Assert.Contains(Descendants(stats).OfType<UiLabel>(), label => label.Text == "Luck");
            if (categories.Contains(screen))
            {
                Assert.Equal(UiOrientation.Horizontal, commands.Orientation);
                Assert.True(commands.Bounds.Bottom <= portrait.Bounds.Y);
            }
            else
            {
                Assert.Equal(UiOrientation.Vertical, commands.Orientation);
                Assert.True(commands.Bounds.X >= portrait.Bounds.Right);
                Assert.True(commands.Bounds.Y >= portrait.Bounds.Y);
            }
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
    [InlineData("gf-detail", UiImageKind.GuardianForcePortrait)]
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
        Assert.Equal("gf-detail", menus.ScreenId);
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
    public void JunctionRootExposesFourCommandsAndReachesAllJunctionCategories()
    {
        MenuController menus = new();
        menus.Open("junction", true);
        UiList commands = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiList>(), list => list.Id == "commands");
        Assert.Equal(new[] { "Junction", "Off", "Auto", "Ability" }, commands.Items);
        string[] categories = ["junction-gf", "junction-magic", "junction-element", "junction-status"];
        for (int index = 0; index < categories.Length; index++)
        {
            menus.Open("junction", true);
            ActivateEntry(menus, 0);
            Assert.Equal("junction-options", menus.ScreenId);
            ActivateEntry(menus, index);
            Assert.Equal(categories[index], menus.ScreenId);
            Assert.NotNull(menus.BuildView().Root);
        }
        string[] roots = ["junction-off", "junction-auto", "junction-command"];
        for (int index = 0; index < roots.Length; index++)
        {
            menus.Open("junction", true);
            ActivateEntry(menus, index + 1);
            Assert.Equal(roots[index], menus.ScreenId);
        }
        menus.Open("junction", true);
        ActivateEntry(menus, 3);
        Assert.Equal("junction-command", menus.ScreenId);
        Assert.Equal("1", menus.SelectedEntry?.Id);
    }

    [Theory]
    [InlineData(640, 448)]
    [InlineData(1280, 896)]
    [InlineData(1920, 1080)]
    public void JunctionAbilitiesShowCommandSlotsAbovePassiveSlotsAndCandidatesOnRight(int width, int height)
    {
        string[] screens = ["junction-command", "junction-ability", "junction-command-choice", "junction-ability-choice"];
        foreach (string screen in screens)
        {
            MenuController menus = new();
            menus.Open(screen, true);
            UiView view = menus.BuildView();
            view.Resize(width, height);
            UiElement[] elements = Descendants(view.Root).ToArray();
            UiElement commandSlots = Assert.Single(elements, element => element.Id == "junction-command-slots");
            UiElement passiveSlots = Assert.Single(elements, element => element.Id == "junction-passive-slots");
            UiElement candidates = Assert.Single(elements, element => element.Id == "junction-ability-choices");
            Assert.True(commandSlots.Bounds.Bottom <= passiveSlots.Bounds.Y);
            Assert.True(commandSlots.Bounds.Right <= candidates.Bounds.X);
            Assert.True(passiveSlots.Bounds.Right <= candidates.Bounds.X);
            Assert.True(passiveSlots.Bounds.Bottom <= view.DesignHeight);
            Assert.True(candidates.Bounds.Bottom <= view.DesignHeight);
            UiList active = Assert.Single(elements.OfType<UiList>(), list => list.Id == "commands");
            UiElement activePanel = screen.EndsWith("-choice", StringComparison.Ordinal) ? candidates : screen == "junction-command" ? commandSlots : passiveSlots;
            Assert.Contains(active, Descendants(activePanel));
            Assert.Same(active, view.FocusedElement);
            Assert.NotEmpty(Descendants(candidates).OfType<UiList>().SelectMany(list => list.Items));
        }
    }

    [Fact]
    public void JunctionAbilitySlotNavigationCrossesGroupsAndCancelReturnsToJunction()
    {
        MenuController menus = new();
        menus.Open("junction", true);
        ActivateEntry(menus, 3);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Down);
        Assert.Equal("3", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Down);
        Assert.Equal("junction-ability", menus.ScreenId);
        Assert.Equal("0", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Up);
        Assert.Equal("junction-command", menus.ScreenId);
        Assert.Equal("3", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Down);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-ability-choice", menus.ScreenId);
        Assert.Equal(0, menus.AbilitySlot);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-ability", menus.ScreenId);
        Assert.Equal("0", menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("junction", menus.ScreenId);
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
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiElement panel = Assert.Single(Descendants(view.Root), element => element.Id == "junction-ability-choices");
        UiList candidates = Assert.Single(Descendants(panel).OfType<UiList>());
        view.Pointer(candidates.Bounds.X + candidates.Bounds.Width / 2, candidates.Bounds.Y + candidates.RowHeight / 2, true);
        Assert.Equal(screen, menus.ScreenId);
        Assert.Equal(commands, menus.Character.Commands);
        Assert.Equal(abilities, menus.Character.Abilities);
    }

    [Theory]
    [InlineData("junction-command", "junction-passive-slots", "junction-ability-choice", 0)]
    [InlineData("junction-ability", "junction-command-slots", "junction-command-choice", 2)]
    public void JunctionInactiveSlotClickSelectsItsGroupAndOpensCandidates(string screen, string panelId, string destination, int row)
    {
        MenuController menus = new();
        menus.Open(screen, true);
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiElement panel = Assert.Single(Descendants(view.Root), element => element.Id == panelId);
        UiList slots = Assert.Single(Descendants(panel).OfType<UiList>());
        view.Pointer(slots.Bounds.X + slots.Bounds.Width / 2, slots.Bounds.Y + slots.RowHeight * (row + 0.5f), true);
        Assert.Equal(destination, menus.ScreenId);
        if (destination == "junction-command-choice")
        {
            Assert.Equal(row + 1, menus.CommandSlot);
        }
        else
        {
            Assert.Equal(row, menus.AbilitySlot);
        }
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
