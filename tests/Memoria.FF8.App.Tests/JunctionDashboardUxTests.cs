using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardUxTests
{
    [Theory]
    [InlineData(MenuPresentation.Classic, 1280, 720, 100)]
    [InlineData(MenuPresentation.Vector, 640, 448, 100)]
    [InlineData(MenuPresentation.Vector, 1280, 720, 150)]
    public void ActionsAndPrioritiesUseCompactBackgroundlessColumnRows(MenuPresentation presentation, int width, int height, int textScale)
    {
        MenuData data = Fixture();
        data.Settings["Text scale"] = textScale + "%";
        MenuController menu = Open(data, presentation);
        menu.PrepareViewport(width, height);
        menu.Navigate(MenuInput.Alternate);
        AssertRows(["optimize", "clear-magic", "clear-party-magic"]);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id is "fill-commands" or "pin-stat" or "remove-junctions");
        menu.Navigate(MenuInput.Confirm);
        AssertRows(["priority-Attack", "priority-Magic", "priority-Defense"]);
        menu.Navigate(MenuInput.Cancel);
        Assert.Empty(Descendants(menu.BuildView().Root).OfType<UiColumnPicker>());

        void AssertRows(string[] expected)
        {
            UiView view = menu.BuildView();
            UiColumnPicker columns = Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>());
            Assert.Equal(expected, columns.Children.Select(row => row.Id));
            Assert.All(columns.Children, row => Assert.False(Assert.IsType<UiButton>(row).DrawBackground));
            Assert.All(columns.Children, row => Assert.Single(row.Children.OfType<UiLabel>(), label => label.Text.Length > 0));
            Assert.Equal(26 * textScale / 100f, columns.Children[1].Bounds.Y - columns.Children[0].Bounds.Y, 3);
            Assert.Equal(expected[0], view.FocusedElement?.Id);
            menu.Navigate(MenuInput.Down);
            Assert.Equal(expected[1], menu.BuildView().FocusedElement?.Id);
            menu.Navigate(MenuInput.Up);
            Assert.Equal(expected[0], menu.BuildView().FocusedElement?.Id);
        }
    }

    [Fact]
    public void MagicColumnScrollingKeepsFocusAcrossPreviewRebuildsAndReturnsToTheStart()
    {
        MenuData data = Fixture();
        for (int index = 0; index < 56; index++)
        {
            data.Magic.Add(new MenuMagic { Id = "magic-" + index, CharacterId = "character-0", Name = "Long magic specimen " + index.ToString("D2"), Quantity = 100, Capacity = 4, RemainingUses = 1 });
        }
        MenuController menu = Open(data);
        menu.PrepareViewport(640, 448);
        menu.SelectEntry("junction-dashboard", "slot-HP");
        menu.Navigate(MenuInput.Confirm);
        UiColumnPicker initial = Assert.Single(Descendants(menu.BuildView().Root).OfType<UiColumnPicker>());
        string[] ids = initial.Children.Select(element => element.Id!).ToArray();
        int rows = initial.RowCount;
        for (int index = 0; index < ids.Length; index += rows)
        {
            UiView view = menu.BuildView();
            Assert.Equal(ids[index], view.FocusedElement?.Id);
            UiColumnPicker columns = Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>());
            Assert.All(columns.Children.Where(element => element.Visible), element => Assert.True(element.Bounds.Right <= columns.Bounds.Right));
            menu.Navigate(MenuInput.Right);
        }
        UiColumnPicker last = Assert.Single(Descendants(menu.BuildView().Root).OfType<UiColumnPicker>());
        Assert.True(last.FirstVisibleColumn > 0);
        for (int index = ids.Length / rows; index > 0; index--)
        {
            menu.Navigate(MenuInput.Left);
        }
        Assert.Equal(ids[0], menu.BuildView().FocusedElement?.Id);
        Assert.Equal(0, Assert.Single(Descendants(menu.BuildView().Root).OfType<UiColumnPicker>()).FirstVisibleColumn);
        Assert.Equal("0", data.Characters[0].JunctionIds["HP"]);
        Assert.DoesNotContain(Descendants(menu.BuildView().Root).OfType<UiButton>(), button => button.Id is "candidate-next" or "candidate-previous");
    }

    [Theory]
    [InlineData(false, 1280, 720, 100)]
    [InlineData(true, 1280, 720, 100)]
    [InlineData(false, 640, 448, 100)]
    [InlineData(true, 640, 448, 100)]
    [InlineData(false, 1280, 720, 150)]
    [InlineData(true, 1280, 720, 150)]
    public void AbilityCandidatesUseIconColumnsAndOnlyTheSelectedDescription(bool passive, int width, int height, int textScale)
    {
        MenuData data = Fixture();
        data.Settings["Text scale"] = textScale + "%";
        List<MenuAbilityChoice> choices = passive ? data.Characters[0].AvailableAbilities : data.Characters[0].AvailableCommands;
        choices.Clear();
        for (int index = 1; index <= 36; index++)
        {
            choices.Add(new(index.ToString(), "Ability specimen " + index.ToString("D2"), true)
            {
                Description = "Description of ability " + index,
                Icon = new UiImageSource(UiImageKind.Icon, 7)
            });
        }
        MenuController menu = Open(data);
        menu.PrepareViewport(width, height);
        menu.SelectEntry("junction-dashboard", passive ? "passive-0" : "command-0");
        menu.Navigate(MenuInput.Confirm);
        UiView view = menu.BuildView();
        UiColumnPicker columns = Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>());
        Assert.Equal("junction-ability-columns", columns.Id);
        Assert.Equal(37, columns.Children.Count);
        Assert.True(columns.Children.Count > columns.RowCount);
        Assert.DoesNotContain(Descendants(view.Root).OfType<UiButton>(), button => button.Id is "candidate-next" or "candidate-previous");
        Assert.All(columns.Children.Skip(1), row =>
        {
            Assert.Equal(new UiImageSource(UiImageKind.Icon, 7), Assert.Single(row.Children.OfType<UiImage>()).Source);
            Assert.Equal("", row.Children.OfType<UiLabel>().Last().Text);
            Assert.DoesNotContain(row.Children.OfType<UiLabel>(), label => label.Text.Contains("Description", StringComparison.Ordinal) || label.Text.Contains('◆'));
        });
        menu.SelectEntry("junction-dashboard", "candidate-36");
        view = menu.BuildView();
        Assert.Equal("candidate-36", view.FocusedElement?.Id);
        UiLabel description = Assert.Single(Descendants(view.Root).OfType<UiLabel>(), label => label.Id == "junction-candidate-description");
        Assert.Equal(choices[^1].Description, description.Text);
        Assert.Single(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.StartsWith("Description", StringComparison.Ordinal));
        Assert.Equal("Ability specimen 36", menu.Entries.Single(entry => entry.Id == "candidate-36").Label);
        Assert.True(Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>()).FirstVisibleColumn > 0);
        MenuNavigationState saved = menu.CaptureNavigation();
        menu.Navigate(MenuInput.Cancel);
        menu.RestoreNavigation(saved);
        Assert.Equal("candidate-36", menu.BuildView().FocusedElement?.Id);
        menu.SelectEntry("junction-dashboard", "candidate-0");
        view = menu.BuildView();
        Assert.Equal("Clear this slot", Descendants(view.Root).OfType<UiLabel>().Single(label => label.Id == "junction-candidate-description").Text);
        Assert.Equal(0, Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>()).FirstVisibleColumn);
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void DashboardColumnsFollowCommandsStatsBindingsAndEffects(MenuPresentation presentation)
    {
        MenuController menu = Open(Fixture(), presentation);
        UiElement[] elements = Descendants(menu.BuildView().Root).ToArray();
        float commands = elements.Single(element => element.Id == "command-0").Bounds.X;
        float stats = elements.Single(element => element.Id == "slot-HP").Bounds.X;
        float bindings = elements.Single(element => element.Id == "slot-Elem-Atk").Bounds.X;
        float effects = elements.Single(element => element.Id == "elemental-readout-0").Bounds.X;
        Assert.True(commands < stats && stats < bindings && bindings < effects);
        AssertNoFooterButtons(elements);
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void MovingRightFromHpReachesElementalAttack(MenuPresentation presentation)
    {
        MenuController menu = Open(Fixture(), presentation);
        menu.SelectEntry("junction-dashboard", "slot-HP");
        menu.Navigate(MenuInput.Right);
        Assert.Equal("slot-Elem-Atk", menu.BuildView().FocusedElement?.Id);
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void MovingUpFromTheFirstCommandReachesTheNearestGuardianAndKeepsItsFocus(MenuPresentation presentation)
    {
        MenuData data = Fixture();
        data.GuardianForces.AddRange([
            new() { Id = "gf-0", NativeId = 0, Name = "Quezacotl" },
            new() { Id = "gf-1", NativeId = 1, Name = "Shiva" },
            new() { Id = "gf-2", NativeId = 2, Name = "Ifrit" }
        ]);
        MenuController menu = Open(data, presentation);
        menu.SelectEntry("junction-dashboard", "basic-action");
        menu.Navigate(MenuInput.Up);
        Assert.Equal("gf-portrait-gf-2", menu.BuildView().FocusedElement?.Id);
        menu.Navigate(MenuInput.Left);
        Assert.Equal("gf-portrait-gf-1", menu.BuildView().FocusedElement?.Id);
        menu.Navigate(MenuInput.Left);
        Assert.Equal("gf-portrait-gf-0", menu.BuildView().FocusedElement?.Id);
        menu.Navigate(MenuInput.Right);
        Assert.Equal("gf-portrait-gf-1", menu.BuildView().FocusedElement?.Id);
    }

    [Fact]
    public void LockedSlotsUseGrayVectorLocksAndAvailableEmptyMagicUsesDashes()
    {
        MenuData data = Fixture();
        data.Characters[0].AvailableJunctionSlots.Remove("Mag");
        MenuController menu = Open(data, MenuPresentation.Vector);
        UiElement[] elements = Descendants(menu.BuildView().Root).ToArray();
        UiElement magicLock = elements.Single(element => element.Id == "junction-lock-Mag");
        UiElement passiveLock = elements.Single(element => element.Id == "junction-lock-passive-2");
        Assert.DoesNotContain(elements.OfType<UiLabel>(), label => label.Text == "Locked");
        UiElement hp = elements.Single(element => element.Id == "slot-HP");
        Assert.Contains(Descendants(hp).OfType<UiLabel>(), label => label.Text == "---");
        foreach (UiElement icon in new[] { magicLock, passiveLock })
        {
            LockCanvas canvas = new();
            icon.Draw(canvas, new VectorUiTheme());
            Assert.NotEmpty(canvas.Colors);
            Assert.All(canvas.Colors, color => Assert.Equal(new UiColor(128, 128, 128), color));
            Assert.Empty(canvas.Texts);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PassiveEmptyAttentionRequiresAnUnusedLegalAlternative(bool alternative)
    {
        MenuData data = Fixture();
        if (alternative)
        {
            data.Characters[0].AvailableAbilities.Add(new("39", "HP+20%", true));
        }
        MenuController menu = Open(data);
        UiElement passive = Descendants(menu.BuildView().Root).Single(element => element.Id == "passive-0");
        string text = string.Join(" ", Descendants(passive).OfType<UiLabel>().Select(label => label.Text).Prepend((passive as UiButton)?.Text ?? ""));
        if (alternative)
        {
            Assert.Contains("!", text);
            Assert.Contains("Empty", text);
        }
        else
        {
            Assert.Contains("---", text);
            Assert.DoesNotContain("Empty", text);
        }
    }

    [Fact]
    public void OrdinaryPickerContainsDirectionalFocusAndHasNoFooterActions()
    {
        MenuController menu = Open(Fixture());
        menu.SelectEntry("junction-dashboard", "command-0");
        menu.Navigate(MenuInput.Confirm);
        foreach (MenuInput direction in new[] { MenuInput.Up, MenuInput.Down, MenuInput.Left, MenuInput.Right })
        {
            for (int step = 0; step < 12; step++)
            {
                menu.Navigate(direction);
                UiView view = menu.BuildView();
                Assert.StartsWith("candidate-", view.FocusedElement?.Id);
                AssertNoFooterButtons(Descendants(view.Root));
            }
        }
    }

    [Fact]
    public void HoveringGuardianForcesDoesNotDispatchGameplayActions()
    {
        MenuData data = Fixture();
        data.GuardianForces.Add(new() { Id = "gf-0", NativeId = 0, Name = "Quezacotl" });
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.Open("junction-dashboard");
        menu.PrepareViewport(1280, 720);
        UiView view = menu.BuildView();
        UiButton gf = Assert.IsType<UiButton>(Descendants(view.Root).Single(element => element.Id == "gf-portrait-gf-0"));
        Assert.NotNull(gf.Hover);
        gf.Hover!();
        Assert.Empty(actions);
        Assert.True(view.Focus(gf));
        Assert.Empty(actions);
    }

    [Fact]
    public void GuardianForceFocusDoesNotAddASelectionSquareOrAccentLine()
    {
        MenuData data = Fixture();
        data.GuardianForces.Add(new() { Id = "gf-0", NativeId = 0, Name = "Quezacotl" });
        MenuController menu = Open(data);
        UiView view = menu.BuildView();
        UiButton gf = Assert.IsType<UiButton>(Descendants(view.Root).Single(element => element.Id == "gf-portrait-gf-0"));
        LockCanvas before = new();
        gf.Draw(before, new VectorUiTheme());
        Assert.True(view.Focus(gf));
        LockCanvas focused = new();
        gf.Draw(focused, new VectorUiTheme());
        Assert.Equal(before.Colors, focused.Colors);
    }

    private static void AssertNoFooterButtons(IEnumerable<UiElement> elements)
    {
        string[] forbidden = ["junction-undo", "junction-actions", "junction-back", "junction-search", "apply-junction-preview", "cancel-junction-preview"];
        Assert.DoesNotContain(elements.OfType<UiButton>(), button => forbidden.Contains(button.Id));
    }

    private static MenuController Open(MenuData data, MenuPresentation presentation = MenuPresentation.Vector)
    {
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(1280, 720);
        menu.BuildView();
        return menu;
    }

    private static MenuData Fixture()
    {
        MenuData data = new();
        MenuCharacter character = new() { Id = "character-0", NativeId = 0, Name = "Squall", InParty = true, Level = 10, CurrentHp = 100, MaximumHp = 200, AbilitySlots = 2 };
        character.Commands.AddRange(["Attack", "--", "--", "--"]);
        character.CommandIds.AddRange(["0", "0", "0"]);
        character.AbilityIds.AddRange(["0", "0", "0", "0"]);
        character.AvailableCommands.AddRange([new("20", "Magic", true), new("21", "GF", true), new("22", "Draw", true), new("23", "Item", true)]);
        foreach (string slot in new[] { "HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck", "Elem-Atk", "ST-Atk", "Elem-Def 1", "Elem-Def 2", "Elem-Def 3", "Elem-Def 4", "ST-Def 1", "ST-Def 2", "ST-Def 3", "ST-Def 4" })
        {
            character.AvailableJunctionSlots.Add(slot);
            character.JunctionIds.Add(slot, "0");
        }
        data.Characters.Add(character);
        return data;
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

    private sealed class LockCanvas : IUiCanvas
    {
        public List<UiColor> Colors { get; } = [];
        public List<string> Texts { get; } = [];
        public void Fill(UiRect bounds, UiColor color) => Colors.Add(color);
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness) => Colors.Add(color);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Texts.Add(text);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment) => Texts.Add(text);
        public void Image(UiImageSource source, UiRect bounds)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }
    }
}
