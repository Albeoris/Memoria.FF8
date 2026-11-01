using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionPercentageIconTests
{
    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void PercentageTablesKeepNativeIdentitiesBesideNamesAndNumbers(MenuPresentation presentation)
    {
        UiView view = View(presentation);
        view.Resize(1280, 720);
        UiElement[] elements = Descendants(view.Root).ToArray();
        foreach ((string family, int count, int first) in new[] { ("elemental", 8, 288), ("status", 13, 272) })
        {
            for (int index = 0; index < count; index++)
            {
                UiElement row = Assert.Single(elements, element => element.Id == family + "-readout-" + index);
                UiImage icon = Assert.Single(Descendants(row).OfType<UiImage>());
                Assert.Equal(new UiImageSource(UiImageKind.Icon, first + index), icon.Source);
                Assert.Equal(family + "-icon-" + index, icon.Id);
                Assert.IsType<UiLabel>(row.Children[1]);
                Assert.Single(Descendants(row.Children[0]).OfType<UiLabel>());
            }
        }
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void InstalledPercentageIconsRenderThroughTheNativeArtworkProvider(MenuPresentation presentation)
    {
        string gameDirectory = TestResources.GameDirectory;
        UiView view = View(presentation);
        view.Resize(1280, 720);
        using SKBitmap bitmap = new(1280, 720);
        using BattlePreviewCanvas canvas = new(bitmap, gameDirectory);
        canvas.BeginFrame(TimeSpan.Zero);
        view.Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
        MenuArtworkProvider artwork = new(gameDirectory);
        foreach (UiImage icon in Descendants(view.Root).OfType<UiImage>().Where(image => image.Id?.Contains("-icon-", StringComparison.Ordinal) == true))
        {
            Assert.True(icon.Bounds.Width > 0 && icon.Bounds.Height > 0);
            var source = artwork.Read(icon.Source!.Value);
            Assert.Contains(source.Pixels, pixel => pixel.A > 0);
        }
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            bitmap.SavePng(Path.Combine(directory, $"junction-native-percentage-icons-{presentation}.png"));
        }
    }

    private static UiView View(MenuPresentation presentation)
    {
        string[] elements = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
        string[] statuses = ["Death", "Poison", "Petrify", "Darkness", "Silence", "Berserk", "Zombie", "Sleep", "Slow", "Stop", "Curse", "Confuse", "Drain"];
        MenuCharacter character = new()
        {
            Id = "character-0",
            NativeId = 0,
            Name = "Squall",
            InParty = true,
            JunctionEffects = new(elements.Select(name => new MenuJunctionPercentage(name, 100, 150)).ToArray(), statuses.Select(name => new MenuJunctionPercentage(name, 100, 100)).ToArray())
        };
        MenuData data = new();
        data.Characters.Add(character);
        character.AvailableJunctionSlots.Add("HP");
        data.Magic.Add(new() { Id = "1", CharacterId = character.Id, Name = "Fire", Quantity = 100 });
        data.Magic.Add(new() { Id = "9", CharacterId = character.Id, Name = "Blizzaga", Quantity = 24 });
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(1280, 720);
        menu.SelectEntry("junction-dashboard", "slot-HP");
        menu.Navigate(MenuInput.Confirm);
        menu.BuildView().Focus(Descendants(menu.BuildView().Root).Single(element => element.Id == "candidate-9"));
        return menu.BuildView();
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
