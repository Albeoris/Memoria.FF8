using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardUxTests
{
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
