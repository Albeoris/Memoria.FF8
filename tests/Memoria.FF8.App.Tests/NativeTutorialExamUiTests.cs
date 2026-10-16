using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialExamUiTests
{
    [Fact]
    public void OriginalWrittenTestAndReviewNavigateTheirActualQuestionsWithoutChangingTheLiveProvider()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialExamCatalog exams = assets.ReadNativeTutorialExamCatalog();
        NativeTutorialExamProgress source = new(0, 7, 8, assets.ReadNativeTutorialCatalog().CreateIsolatedParty(1));
        string original = JsonSerializer.Serialize(source);
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => new HashSet<int>(), _ => throw new InvalidOperationException("A written test must not start a Junction demo."));
        menu.ConnectNativeTutorialExam(exams, () => source);
        menu.Open("tutorial");
        Assert.False(menu.Entries.Single(entry => entry.Id == "category-4").Enabled);
        menu.SelectEntry("tutorial", "category-3");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("tutorial-native-test", menu.ScreenId);
        menu.SelectEntry(menu.ScreenId, "yes");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("tutorial-native-exam-question", menu.ScreenId);
        Assert.Contains("The Draw command extracts magic from enemies.", menu.CurrentScreen.Details(menu)[0]);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("tutorial-native-exam-question", menu.ScreenId);
        foreach (NativeTutorialExamQuestion question in exams.Levels[0].Questions)
        {
            menu.SelectEntry(menu.ScreenId, question.CorrectAnswer.ToString());
            menu.Navigate(MenuInput.Confirm);
        }
        Assert.Equal("tutorial-native-exam-result", menu.ScreenId);
        Assert.Equal(100, menu.NativeTutorialExamResult!.Score);
        Assert.Equal(1, menu.NativeTutorialExamProgress!.PassedTests);
        Assert.Equal(8, menu.NativeTutorialExamProgress.SeeDRank);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("tutorial-native-test", menu.ScreenId);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("tutorial", menu.ScreenId);
        Assert.True(menu.Entries.Single(entry => entry.Id == "category-4").Enabled);
        menu.SelectEntry("tutorial", "category-4");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("tutorial-native-review", menu.ScreenId);
        Assert.Single(menu.Entries);
        menu.Navigate(MenuInput.Confirm);
        foreach (NativeTutorialExamQuestion question in exams.Levels[0].Questions)
        {
            menu.SelectEntry(menu.ScreenId, question.CorrectAnswer.ToString());
            menu.Navigate(MenuInput.Confirm);
        }
        Assert.Equal(8, menu.NativeTutorialExamProgress.SeeDRank);
        Assert.Equal(1, menu.NativeTutorialExamProgress.PassedTests);
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void InformationFollowsAnOriginalLinkAndReturnsToItsOriginalParent()
    {
        using GameAssets assets = InstalledAssets();
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => new HashSet<int>(), _ => { });
        menu.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), () => new(0, 7, 8));
        menu.Open("tutorial");
        menu.SelectEntry("tutorial", "category-6");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("tutorial-native-information", menu.ScreenId);
        Assert.Equal(0, menu.CurrentNativeInformationPage!.Node.Id);
        menu.SelectEntry(menu.ScreenId, "information-0");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(238, menu.CurrentNativeInformationPage!.Node.Id);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(0, menu.CurrentNativeInformationPage!.Node.Id);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("tutorial", menu.ScreenId);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }
}
