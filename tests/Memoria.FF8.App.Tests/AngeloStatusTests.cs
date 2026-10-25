using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class AngeloStatusTests
{
    private static NativeLimitProgress Progress() => new(new string('A', 64), 0, 0, 1, 1, [0, 180, 80, 200, 255, 255, 255, 255]) { PetPalsFlags = 0x16 };

    [Theory]
    [InlineData(100, 100, false, 0)]
    [InlineData(100, 50, false, 32)]
    [InlineData(100, 0, false, 64)]
    [InlineData(100, 255, false, 0)]
    [InlineData(0, 255, true, 64)]
    public void TrainingBarUsesNativeRemainingCounter(int total, int remaining, bool learned, int expected)
    {
        Assert.Equal(expected, LiveMenuCatalog.AngeloTrainingProgress((byte)total, (byte)remaining, learned));
    }

    [Fact]
    public void ChoosingTrainingPreservesAllProgressAndSurvivesJsonRestore()
    {
        GameSession session = new(new GameState { LimitProgress = Progress() });
        session.SelectAngeloLearning(2);
        Assert.Equal(JsonSerializer.Serialize(Progress() with { TrainingSelection = 2 }), JsonSerializer.Serialize(session.LimitProgress));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        Assert.Equal((byte)2, restored.LimitProgress!.TrainingSelection);
        restored.SelectAngeloLearning(1);
        Assert.Equal(Progress().TrainingCounters.ToArray(), restored.LimitProgress.TrainingCounters.ToArray());
        Assert.Equal(Progress().AngeloFlags, restored.LimitProgress.AngeloFlags);
        restored.SelectAngeloLearning(0);
        Assert.Equal((byte)0, restored.LimitProgress.TrainingSelection);
        Assert.Throws<InvalidOperationException>(() => restored.SelectAngeloLearning(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => restored.SelectAngeloLearning(8));
        GameSession unknown = new(new GameState { LimitProgress = Progress() with { PetPalsFlags = null } });
        Assert.Throws<NotSupportedException>(() => unknown.SelectAngeloLearning(1));
    }

    [Fact]
    public void LiveStatusListsKnownMovesAndChangesTheSavedTrainingTarget()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native menu resources are required.");
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        GameSession session = new(new GameState { CharacterVitals = assets.ReadNewGameCharacterVitals(), LimitProgress = assets.ReadNewGameLimitProgress() with { AngeloFlags = 1, PetPalsFlags = 0x16, TrainingSelection = 1 } });
        MenuController menus = new(() => LiveMenuData.Build(session.State, new Dictionary<int, string> { [2] = "Comet" }, catalog, visibleCharacterRows: new HashSet<int> { 4 }), action => session.SelectAngeloLearning(int.Parse(action.SubjectId.AsSpan(6))));
        menus.Open("status-angelo");
        menus.Execute(new MenuAction("select-character", "character-4"));
        Assert.Equal(new[] { "limit-0", "limit-1", "limit-2", "limit-4" }, menus.Entries.Select(entry => entry.Id));
        Assert.True(menus.Entries[0].Enabled);
        Assert.All(menus.Entries, entry => Assert.DoesNotContain("{Char", entry.Label));
        UiList List() => Descendants(menus.BuildView().Root).OfType<UiList>().Single(list => list.Id == "angelo-abilities");
        Assert.Equal(UiOrientation.Vertical, List().Orientation);
        Assert.NotNull(List().LeadingImages[1]);
        menus.SelectEntry("status-angelo", "limit-2");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal((byte)2, session.LimitProgress!.TrainingSelection);
        Assert.Null(List().LeadingImages[1]);
        Assert.NotNull(List().LeadingImages[2]);
        menus.SelectEntry("status-angelo", "limit-0");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("status-angelo-warning", menus.ScreenId);
        Assert.Equal((byte)2, session.LimitProgress.TrainingSelection);
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal((byte)2, session.LimitProgress.TrainingSelection);
        menus.SelectEntry("status-angelo", "limit-0");
        menus.Navigate(MenuInput.Confirm);
        menus.SelectEntry("status-angelo-warning", "yes");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal((byte)0, session.LimitProgress.TrainingSelection);
        Assert.All(List().LeadingImages, image => Assert.Null(image));
        menus.SelectEntry("status-angelo", "limit-2");
        menus.Navigate(MenuInput.Confirm);
        session.SetLimitProgress(session.LimitProgress! with { PetPalsFlags = 255 });
        menus.InvalidateLiveData();
        Assert.Equal(8, menus.Entries.Count);
        Assert.Equal("Comet", menus.Character!.LimitSettings["Angelo name"]);
        Assert.All(menus.Entries, entry => Assert.NotEmpty(entry.Help));
        foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
        {
            menus.Presentation = presentation;
            UiView view = menus.BuildView();
            view.Resize(1280, 896);
            using System.Drawing.Bitmap bitmap = new(1280, 896);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            view.Draw(canvas, menus.CreateTheme());
            if (Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_LIMIT_OUTPUT") is string output)
            {
                Directory.CreateDirectory(output);
                bitmap.Save(Path.Combine(output, $"{presentation.ToString().ToLowerInvariant()}-angelo.png"));
            }
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
}
