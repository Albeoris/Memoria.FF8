using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionStatLockTests
{
    [Theory]
    [InlineData(MenuPresentation.Classic, 100)]
    [InlineData(MenuPresentation.Vector, 100)]
    [InlineData(MenuPresentation.Classic, 150)]
    [InlineData(MenuPresentation.Vector, 150)]
    public void StatHeadingAlignsWithNamesAndNamesMatchCommandFont(MenuPresentation presentation, int textScale)
    {
        MenuData data = new();
        data.Settings["Text scale"] = textScale + "%";
        MenuCharacter character = new() { Id = "character-0", NativeId = 0, Name = "Squall", InParty = true, CurrentHp = 100, MaximumHp = 200 };
        character.Commands.Add("Attack");
        data.Characters.Add(character);
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(1280, 720);
        UiView view = menu.BuildView();
        view.Resize(1280, 720);
        UiElement[] elements = Descendants(view.Root).ToArray();
        UiLabel heading = Assert.Single(elements.OfType<UiLabel>(), label => label.Text == "Stat");
        UiButton command = Assert.IsType<UiButton>(elements.Single(element => element.Id == "basic-action"));
        foreach (string slot in new[] { "HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck" })
        {
            UiElement row = elements.Single(element => element.Id == "slot-" + slot);
            UiLabel name = Descendants(row).OfType<UiLabel>().First();
            Assert.Equal(command.FontSize, name.FontSize);
            Assert.Equal(heading.Bounds.X, name.Bounds.X, 2);
        }
        Assert.DoesNotContain(elements.OfType<UiLabel>(), label => label.Text.Contains('◆'));
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            using SKBitmap bitmap = new(1280, 720);
            using BattlePreviewCanvas canvas = new(bitmap, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
            canvas.BeginFrame(TimeSpan.Zero);
            view.Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
            bitmap.SavePng(Path.Combine(directory, $"junction-stat-lock-{presentation}-{textScale}.png"));
        }
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
}
