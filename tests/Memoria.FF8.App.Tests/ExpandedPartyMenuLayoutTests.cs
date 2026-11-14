using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class ExpandedPartyMenuLayoutTests
{
    private static readonly byte[] PartyOrder = [3, 0, 2, 7, 1, 9, 4, 8, 5, 10, 6];

    public static IEnumerable<object[]> LayoutCases()
    {
        foreach (int count in new[] { 3, 4, 5, 11 })
        {
            foreach (MenuPresentation presentation in Enum.GetValues<MenuPresentation>())
            {
                yield return [count, presentation, 640, 448];
                yield return [count, presentation, 1280, 720];
            }
        }
    }

    [Theory]
    [MemberData(nameof(LayoutCases))]
    public void MainPartyPanesKeepOrderedIdentityAcrossNavigationAndPointer(int count, MenuPresentation presentation, int width, int height)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = CreateParty(assets, count);
        MenuController menu = new(() => BuildData(vitals, count)) { Presentation = presentation };
        OpenCharacterPicker(menu);
        BattleUiTests.TestCanvas canvas = new();
        UiView view = Draw(menu, width, height, canvas);
        UiButton[] party = PartyButtons(view, count);
        AssertPartyBounds(party, view, count, width, height);
        if (count == 4 && presentation == MenuPresentation.Vector)
        {
            float scale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
            for (int index = 0; index < count; index++)
            {
                MenuCharacter member = menu.CharacterChoices[index];
                UiRect bounds = ToPixels(view, party[index].Bounds, width, height);
                float nameHeight = Assert.Single(canvas.TextDraws, draw => draw.Text == member.Name && bounds.Contains(draw.Bounds.X + 1, draw.Bounds.Y + 1)).Bounds.Height;
                float hpHeight = Assert.Single(canvas.TextDraws, draw => draw.Text == "HP   " + MenuController.Hp(member) && bounds.Contains(draw.Bounds.X + 1, draw.Bounds.Y + 1)).Bounds.Height;
                Assert.True(nameHeight >= 16 * scale, $"{member.Name} has only {nameHeight / scale:F1} design pixels for the name.");
                Assert.True(hpHeight >= 18 * scale, $"{member.Name} has only {hpHeight / scale:F1} design pixels for HP.");
            }
        }
        if (count == 3)
        {
            Assert.Equal(3, Descendants(view.Root).OfType<UiButton>().Count(button => button.Id is "character-3" or "character-0" or "character-2"));
            Assert.Equal(party[0].Bounds.Height, party[2].Bounds.Height, 2);
            Assert.True(party[2].Bounds.Bottom <= CharacterButton(view, 1).Bounds.Y);
        }
        Assert.Equal(PartyOrder[..count].Select(Id), menu.CharacterChoices.Take(count).Select(character => character.Id));

        for (int index = 0; index < count; index++)
        {
            Assert.Equal(Id(PartyOrder[index]), Draw(menu, width, height).FocusedElement?.Id);
            menu.Navigate(MenuInput.Down);
        }
        if (count < PartyOrder.Length)
        {
            string reserve = menu.CharacterChoices[count].Id;
            Assert.Equal(reserve, Draw(menu, width, height).FocusedElement?.Id);
            for (int index = count; index < PartyOrder.Length; index++)
            {
                menu.Navigate(MenuInput.Down);
            }
        }
        Assert.Equal(Id(PartyOrder[0]), Draw(menu, width, height).FocusedElement?.Id);

        foreach (byte row in PartyOrder[Math.Min(count - 1, 3)..count])
        {
            view = Draw(menu, width, height);
            Click(view, CharacterButton(view, row), width, height);
            Assert.Equal("status", menu.ScreenId);
            Assert.Equal(Id(row), menu.Character?.Id);
            if (row != PartyOrder[count - 1])
            {
                OpenCharacterPicker(menu);
            }
        }
    }

    [Theory]
    [MemberData(nameof(LayoutCases))]
    public void SwitchPanesReachBothColumnsAndPublishClickedMemberIdentity(int count, MenuPresentation presentation, int width, int height)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals vitals = CreateParty(assets, count);
        List<PlayerAction> input = [];
        MenuController menu = new(() => BuildData(vitals, count), action =>
        {
            PlayerAction request = NativePartySwitchMenuProjection.PlayerAction(action);
            input.Add(request);
            vitals = NativePartySwitchInput.Apply(vitals, request, 2, count);
        }) { Presentation = presentation };
        menu.Open("switch-party");
        UiView view = Draw(menu, width, height);
        UiButton[] party = PartyButtons(view, count);
        AssertPartyBounds(party, view, count, width, height);

        menu.SelectEntry("switch-party", Id(PartyOrder[0]));
        menu.Navigate(MenuInput.Right);
        if (count > 4)
        {
            Assert.Equal(Id(PartyOrder[1]), menu.SelectedEntry?.Id);
            if (count < PartyOrder.Length)
            {
                menu.Navigate(MenuInput.Right);
                Assert.Contains(menu.Data.Characters, character => !character.InParty && character.Id == menu.SelectedEntry?.Id);
                menu.Navigate(MenuInput.Left);
                Assert.Equal(Id(PartyOrder[1]), menu.SelectedEntry?.Id);
            }
            menu.Navigate(MenuInput.Left);
            Assert.Equal(Id(PartyOrder[0]), menu.SelectedEntry?.Id);
        }
        else
        {
            Assert.Contains(menu.Data.Characters, character => !character.InParty && character.Id == menu.SelectedEntry?.Id);
        }

        foreach (byte row in PartyOrder[..count])
        {
            view = Draw(menu, width, height);
            Click(view, CharacterButton(view, row), width, height);
            Assert.Equal("switch-party-target", menu.ScreenId);
            Assert.Equal(Id(row), menu.ExchangeId);
            menu.Navigate(MenuInput.Cancel);
            Assert.Equal("switch-party", menu.ScreenId);
        }

        if (count > 3)
        {
            byte last = PartyOrder[count - 1];
            view = Draw(menu, width, height);
            Click(view, CharacterButton(view, last), width, height);
            view = Draw(menu, width, height);
            Click(view, CharacterButton(view, NativePartySwitchInput.EmptySlot), width, height);
            Assert.Equal(new PlayerAction(PlayerActionKind.SwitchPartyMembers, last, Quantity: NativePartySwitchInput.EmptySlot), Assert.Single(input));
            Assert.Equal(PartyOrder[..(count - 1)], vitals.GetBattlePartyRows(count));

            view = Draw(menu, width, height);
            Click(view, CharacterButton(view, NativePartySwitchInput.EmptySlot), width, height);
            view = Draw(menu, width, height);
            Click(view, CharacterButton(view, last), width, height);
            Assert.Equal(new PlayerAction(PlayerActionKind.SwitchPartyMembers, NativePartySwitchInput.EmptySlot, Quantity: last), input[^1]);
            Assert.Equal(PartyOrder[..count], vitals.GetBattlePartyRows(count));
        }
        Assert.Equal(PartyOrder[..3], vitals.CombatParty);
    }

    [Fact]
    public void RenderExpandedPartyMenusWhenOutputIsRequested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_PARTY_MENU_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_EXPANDED_PARTY_MENU_OUTPUT to a scratch directory for menu previews.");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        Directory.CreateDirectory(output!);
        foreach (int count in new[] { 4, 5, 11 })
        {
            NativeCharacterVitals vitals = CreateParty(assets, count);
            foreach (MenuPresentation presentation in Enum.GetValues<MenuPresentation>())
            {
                MenuController menu = new(() => BuildData(vitals, count, catalog)) { Presentation = presentation };
                OpenCharacterPicker(menu);
                RenderPreview(menu, directory, output!, count, "main");
                menu.Open("switch-party");
                RenderPreview(menu, directory, output!, count, "switch");
            }
        }
    }

    [Fact]
    public void RenderFourMemberVectorMainWhenOutputIsRequested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_EXPANDED_PARTY_MENU_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_EXPANDED_PARTY_MENU_OUTPUT to a scratch directory for menu previews.");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        NativeCharacterVitals vitals = CreateParty(assets, 4);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        MenuController menu = new(() => BuildData(vitals, 4, catalog)) { Presentation = MenuPresentation.Vector };
        OpenCharacterPicker(menu);
        Directory.CreateDirectory(output!);
        RenderPreview(menu, directory, output!, 4, "main");
    }

    private static void RenderPreview(MenuController menu, string directory, string output, int count, string screen)
    {
        using SkiaSharp.SKBitmap bitmap = new(1280, 720);
        using BattlePreviewCanvas canvas = new(bitmap, directory);
        UiView view = menu.BuildView();
        view.Resize(bitmap.Width, bitmap.Height);
        view.Draw(canvas, menu.CreateTheme());
        bitmap.SavePng(Path.Combine(output, $"party-{count}-{menu.Presentation.ToString().ToLowerInvariant()}-{screen}.png"));
    }

    private static NativeCharacterVitals CreateParty(GameAssets assets, int count)
    {
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().WithExtendedRoster(true);
        NativeCharacterVital[] records = source.Records.Select((record, row) => record with { CharacterId = (byte)row, MemberFlags = 1 }).ToArray();
        return (source with { Records = records, CombatParty = PartyOrder[..3] }).WithBattleParty(PartyOrder[..count], count);
    }

    private static MenuData BuildData(NativeCharacterVitals vitals, int capacity, LiveMenuCatalog? catalog = null)
    {
        GameState state = new() { Scene = SceneKind.Field, CharacterVitals = vitals, GameplayPreferences = new() { BattleAllyCount = capacity }, WorldControls = NativeWorldControlState.CreateNewGame() with { Menu209 = 2 } };
        return LiveMenuData.Build(state, catalog: catalog, includeJunctionPlanner: false);
    }

    private static void OpenCharacterPicker(MenuController menu)
    {
        menu.Open();
        menu.SelectEntry("main", "status");
        menu.Navigate(MenuInput.Confirm);
        Assert.True(menu.IsSelectingCharacter);
    }

    private static UiView Draw(MenuController menu, int width, int height, BattleUiTests.TestCanvas? canvas = null)
    {
        UiView view = menu.BuildView();
        view.Resize(width, height);
        view.Draw(canvas ?? new BattleUiTests.TestCanvas(), menu.CreateTheme());
        return view;
    }

    private static void AssertPartyBounds(UiButton[] party, UiView view, int count, int width, int height)
    {
        Assert.Equal(count <= 4 ? 1 : 2, party.Select(button => button.Bounds.X).Distinct().Count());
        Assert.Equal(count <= 4 ? count : (count + 1) / 2, party.Select(button => button.Bounds.Y).Distinct().Count());
        UiRect viewport = new(0, 0, width, height);
        for (int index = 0; index < party.Length; index++)
        {
            UiRect bounds = ToPixels(view, party[index].Bounds, width, height);
            Assert.True(bounds.Height >= 20 && bounds.Width >= 30, $"{party[index].Id} at {bounds}");
            Assert.True(bounds.X >= 0 && bounds.Y >= 0 && bounds.Right <= viewport.Right + 0.01f && bounds.Bottom <= viewport.Bottom + 0.01f, $"{party[index].Id} at {bounds} outside {viewport}");
            foreach (UiButton previous in party.Take(index))
            {
                UiRect previousBounds = ToPixels(view, previous.Bounds, width, height);
                UiRect overlap = bounds.Intersect(previousBounds);
                Assert.True(overlap.Width < 0.01f || overlap.Height < 0.01f, $"{party[index].Id} at {bounds} and {previous.Id} at {previousBounds} overlap at {overlap}");
            }
        }
    }

    private static UiButton[] PartyButtons(UiView view, int count) => PartyOrder[..count].Select(row => CharacterButton(view, row)).ToArray();

    private static UiButton CharacterButton(UiView view, int row) => Assert.Single(Descendants(view.Root).OfType<UiButton>(), button => button.Id == Id(row));

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children.SelectMany(Descendants))
        {
            yield return child;
        }
    }

    private static void Click(UiView view, UiElement element, int width, int height)
    {
        UiRect bounds = ToPixels(view, element.Bounds, width, height);
        view.Pointer(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, false);
        view.Pointer(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2, true);
    }

    private static UiRect ToPixels(UiView view, UiRect bounds, int width, int height)
    {
        float scale = Math.Min(width / view.DesignWidth, height / view.DesignHeight);
        float offsetX = view.ViewportMode == UiViewportMode.Fit ? (width - view.DesignWidth * scale) / 2 : 0;
        float offsetY = view.ViewportMode == UiViewportMode.Fit ? (height - view.DesignHeight * scale) / 2 : 0;
        return new(offsetX + bounds.X * scale, offsetY + bounds.Y * scale, bounds.Width * scale, bounds.Height * scale);
    }

    private static string Id(byte row) => Id((int)row);
    private static string Id(int row) => $"character-{row}";
}
