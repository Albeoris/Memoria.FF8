using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class FieldMenuStatusTests
{
    [Theory]
    [InlineData("magic-target", 100, MenuPresentation.Vector)]
    [InlineData("magic-target", 150, MenuPresentation.Vector)]
    [InlineData("item-target", 100, MenuPresentation.Vector)]
    [InlineData("item-target", 150, MenuPresentation.Vector)]
    [InlineData("magic-target", 100, MenuPresentation.Classic)]
    [InlineData("magic-target", 150, MenuPresentation.Classic)]
    [InlineData("item-target", 100, MenuPresentation.Classic)]
    [InlineData("item-target", 150, MenuPresentation.Classic)]
    public void TargetsRenderWithConditionsAtNormalAndEnlargedText(string screen, int textScale, MenuPresentation presentation)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native artwork is required.");
        MenuData data = Targets();
        data.Characters[0].NativeId = 0;
        data.Characters[1].NativeId = 4;
        data.Settings["Text scale"] = textScale + "%";
        MenuController menus = new(() => data) { Presentation = presentation };
        menus.Open(screen);
        using SkiaSharp.SKBitmap bitmap = new(1280, 720);
        using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
        UiView view = menus.BuildView();
        view.Resize(bitmap.Width, bitmap.Height);
        for (int frame = 0; frame < 3; frame++)
        {
            canvas.BeginFrame(TimeSpan.Zero);
            canvas.Fill(new UiRect(0, 0, bitmap.Width, bitmap.Height), new UiColor(0, 0, 0));
            view.Draw(canvas, menus.CreateTheme());
        }
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MENU_UI_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            bitmap.SavePng(Path.Combine(output, $"conditions-{screen}-{textScale}-{presentation}.png"));
        }
        UiList list = Commands(menus);
        Assert.True(list.Bounds.Width > 0);
        Assert.Contains("Recipient\nPoison / Blind", list.Items);
    }

    [Theory]
    [InlineData(1, "KO")]
    [InlineData(2, "Poison")]
    [InlineData(4, "Petrify")]
    [InlineData(8, "Blind")]
    [InlineData(16, "Silence")]
    [InlineData(32, "Berserk")]
    [InlineData(64, "Zombie")]
    [InlineData(0x300, "")]
    [InlineData(0x312, "Poison / Silence")]
    public void SavedConditionsProjectNamesWithoutHpFlagsOrMutatingState(int mask, string expected)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native initialization resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals initial = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = initial.Records.ToArray();
        records[0] = records[0] with { Status = (ushort)mask };
        GameState state = new() { CharacterVitals = initial with { Records = records } };
        string before = JsonSerializer.Serialize(state);

        MenuCharacter projected = LiveMenuData.Build(state).Characters.Single(character => character.Id == "character-0");

        Assert.Equal(expected, string.Join(" / ", projected.Conditions));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData("magic-target")]
    [InlineData("item-target")]
    public void TargetRowsShowEveryCharactersOwnConditionsAndRefreshAfterCure(string screen)
    {
        MenuData data = Targets();
        MenuController menus = new(() => data);
        menus.Open(screen);
        UiList list = Commands(menus);
        Assert.Equal(new[] { "Caster\nSilence", "Recipient\nPoison / Blind" }, list.Items);
        Assert.Equal(new[] { "HP 20 / 100", "HP 30 / 200" }, list.SecondaryItems);
        Assert.Equal(UiTextFlow.Wrap, list.TextFlow);

        data.Characters[1].Conditions.Clear();
        menus.InvalidateLiveData();

        Assert.Equal(new[] { "Caster\nSilence", "Recipient" }, Commands(menus).Items);
    }

    [Fact]
    public void ItemStatusPanelFollowsTargetCursorRatherThanCasterAndClearsAfterCure()
    {
        MenuData data = Targets();
        MenuController menus = new(() => data);
        menus.Open("item-target");
        Assert.Equal("Silence", StatusText(menus));

        menus.Navigate(MenuInput.Down);

        Assert.Equal("Caster", menus.Character!.Name);
        Assert.Equal("Poison\nBlind", StatusText(menus));
        data.Characters[1].Conditions.Clear();
        menus.InvalidateLiveData();
        Assert.Equal("", StatusText(menus));
    }

    private static MenuData Targets()
    {
        MenuData data = new();
        MenuCharacter caster = new() { Id = "character-0", Name = "Caster", InParty = true, NativeFieldItemTargetAllowed = true, CurrentHp = 20, MaximumHp = 100 };
        caster.Conditions.Add("Silence");
        MenuCharacter recipient = new() { Id = "character-1", Name = "Recipient", NativeFieldItemTargetAllowed = true, CurrentHp = 30, MaximumHp = 200 };
        recipient.Conditions.AddRange(["Poison", "Blind"]);
        data.Characters.AddRange([caster, recipient]);
        data.Items.Add(new() { Id = "item-1", Name = "Remedy", Quantity = 2, NativeFieldUseSupported = true });
        data.SupportedActions.Add("item-use");
        data.SupportedActions.Add("magic-use");
        return data;
    }

    private static UiList Commands(MenuController menus) => Assert.Single(Descendants(menus.BuildView().Root).OfType<UiList>(), list => list.Id == "commands");

    private static string StatusText(MenuController menus)
    {
        UiPanel panel = Assert.Single(Descendants(menus.BuildView().Root).OfType<UiPanel>(), panel => panel.Caption == "STATUS");
        return Assert.Single(Descendants(panel).OfType<UiLabel>()).Text;
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement element in Descendants(child))
            {
                yield return element;
            }
        }
    }
}
