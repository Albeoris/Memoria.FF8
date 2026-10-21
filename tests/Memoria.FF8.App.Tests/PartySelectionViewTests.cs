using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class PartySelectionViewTests
{
    private static NativePartyMenuState Menu => new(0, string.Empty, string.Empty, 0, 0, 0, 0, 0, NativePartyMenuPhase.Active, 2, [0, 1, 5], 0xFF, 1, 1, false, NativeFieldMenuKind.PartySelection, 2, 0, null, null, null, [2, 255, 3, 4, 255, 6, 7, 255], 2);

    [Theory]
    [InlineData(640, 448, 1f)]
    [InlineData(960, 672, 1.5f)]
    [InlineData(1920, 1080, 3f)]
    public void HelpCaptionUsesJunctionSizeWithoutCappingRequestedTextScale(int width, int height, float textScale)
    {
        UiView view = PartySelectionView.Build(Menu, new MenuData(), 0, textScale: textScale);
        view.Resize(width, height);
        Canvas canvas = new(width, height);
        view.Draw(canvas, new UiTheme());

        var caption = Assert.Single(canvas.Captions);
        Assert.Equal("HELP", caption.Text);
        float expected = 13 * textScale * Math.Min(width / 960f, height / 672f);
        Assert.True(Math.Abs(caption.Size - expected) < 0.001f);
    }

    [Fact]
    public void NormalSwitchKeepsClickableReservesInTheirOwnFourColumnPane()
    {
        MenuController menus = new();
        menus.Open("switch-party", true);
        UiView view = menus.BuildView();
        view.Resize(960, 672);
        UiButton[] choices = Descendants(view.Root).OfType<UiButton>().ToArray();
        Assert.Equal(menus.Data.Characters.Count, choices.Length);
        MenuCharacter[] reserves = menus.Data.Characters.Where(character => !character.InParty).ToArray();
        UiButton firstReserve = Assert.Single(choices, choice => choice.Id == reserves[0].Id);
        UiButton fifthReserve = Assert.Single(choices, choice => choice.Id == reserves[4].Id);
        UiButton party = Assert.Single(choices, choice => choice.Id == menus.Data.Characters.First(character => character.InParty).Id);
        Assert.True(firstReserve.Bounds.X > party.Bounds.Right);
        Assert.Equal(firstReserve.Bounds.X, fifthReserve.Bounds.X);
        Assert.True(fifthReserve.Bounds.Y > firstReserve.Bounds.Y);
        menus.Navigate(MenuInput.Right);
        Assert.Equal(reserves[0].Id, menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Down);
        Assert.Equal(reserves[4].Id, menus.SelectedEntry?.Id);
        menus.Navigate(MenuInput.Up);
        Assert.Equal(reserves[0].Id, menus.SelectedEntry?.Id);
        firstReserve.Pointer(firstReserve.Bounds.X + 1, firstReserve.Bounds.Y + 1);
        Assert.Equal("switch-party-target", menus.ScreenId);
        Assert.Equal(reserves[0].Id, menus.ExchangeId);
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

    [Fact]
    public void NavigationUsesPhysicalReserveCellsIncludingHoles()
    {
        NativePartyMenuState menu = Menu;
        var inputs = NativePartyMenuProjection.Inputs(menu);
        int firstReserve = PartySelectionView.Navigate(menu, 0, 1, 0);
        Assert.Equal(2, inputs[firstReserve].Value);
        int empty = PartySelectionView.Navigate(menu, firstReserve, 1, 0);
        Assert.Equal(255, inputs[empty].Value);
        int belowEmpty = PartySelectionView.Navigate(menu, empty, 0, 1);
        Assert.Equal(6, inputs[belowEmpty].Value);
        Assert.Equal(empty, PartySelectionView.Navigate(menu, belowEmpty, 0, -1));
        Assert.Equal(0, PartySelectionView.Navigate(menu, firstReserve, -1, 0));
        Assert.Equal(firstReserve, PartySelectionView.Navigate(menu, firstReserve, 0, 0));
    }

    [Fact]
    public void ConfirmRemainsSeparateAndUnavailableWhileExchangingOrIncomplete()
    {
        NativePartyMenuState menu = Menu with { SelectedPartySlot = null };
        var inputs = NativePartyMenuProjection.Inputs(menu);
        int confirm = PartySelectionView.Navigate(menu, 2, 0, 1);
        Assert.Equal(PlayerActionKind.Confirm, inputs[confirm].Kind);
        Assert.Equal(2, PartySelectionView.Navigate(menu, confirm, 0, -1));
        Assert.DoesNotContain(NativePartyMenuProjection.Inputs(Menu), input => input.Kind == PlayerActionKind.Confirm);
        Assert.DoesNotContain(NativePartyMenuProjection.Inputs(menu with { TemporaryCombatParty = [0, 1, 255] }), input => input.Kind == PlayerActionKind.Confirm);
    }

    [Fact]
    public void LockedSourceOffersPartyReorderingWithoutRejectedReserveTargets()
    {
        NativePartyMenuState menu = Menu with { SelectedPartySlot = 0 };
        Assert.All(NativePartyMenuProjection.Inputs(menu), input => Assert.Equal(PlayerActionKind.SelectPartySlot, input.Kind));
        Assert.Equal(1, PartySelectionView.Navigate(menu, 0, 0, 1));
        Assert.Equal(0, PartySelectionView.Navigate(menu, 0, 1, 0));
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(960, 672)]
    [InlineData(1920, 1080)]
    public void PortraitsRetainCellOrderAndBothPointersFitTheViewport(int width, int height)
    {
        NativePartyMenuState menu = Menu;
        string before = JsonSerializer.Serialize(menu);
        MenuData data = new();
        for (int row = 0; row < 8; row++)
        {
            data.Characters.Add(new MenuCharacter { Id = $"character-{row}", NativeId = row, Name = $"Member {row}", Level = 20, CurrentHp = 700, MaximumHp = 1000 });
        }
        int candidate = PartySelectionView.Navigate(menu, 0, 1, 0);
        UiView view = PartySelectionView.Build(menu, data, candidate);
        Canvas canvas = new(width, height);
        view.Resize(width, height);
        view.Draw(canvas, new UiTheme());
        Assert.Equal(2, canvas.Pointers.Count);
        Assert.Equal(new[] { 0, 1, 5, 2, 3, 4, 6, 7 }, canvas.Images.Select(image => image.Source.Id));
        Assert.True(canvas.Images[3].Bounds.X < canvas.Images[4].Bounds.X);
        Assert.True(canvas.Images[6].Bounds.Y > canvas.Images[3].Bounds.Y);
        Assert.DoesNotContain(canvas.Texts, text => text.StartsWith("Slot ", StringComparison.Ordinal) || text.StartsWith("Reserve:", StringComparison.Ordinal));
        Assert.Equal(before, JsonSerializer.Serialize(menu));
    }

    private sealed class Canvas(int width, int height) : IUiCanvas
    {
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];
        public List<UiRect> Pointers { get; } = [];
        public List<string> Texts { get; } = [];
        public List<(string Text, float Size)> Captions { get; } = [];

        public void OutlinedText(string text, float fontSize, UiRect bounds, UiColor color, UiColor outline, float thickness)
        {
            Captions.Add((text, fontSize));
        }

        public void Fill(UiRect bounds, UiColor color)
        {
            Check(bounds);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Check(bounds);
            Texts.Add(text);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
            Check(bounds);
            Images.Add((source, bounds));
        }

        public void MenuPointer(UiRect bounds)
        {
            Check(bounds);
            Pointers.Add(bounds);
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }

        private void Check(UiRect bounds)
        {
            Assert.True(bounds.X >= 0 && bounds.Y >= 0 && bounds.Right <= width + 0.01f && bounds.Bottom <= height + 0.01f);
        }
    }
}
