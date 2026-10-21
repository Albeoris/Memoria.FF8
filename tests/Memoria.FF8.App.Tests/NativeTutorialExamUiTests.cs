using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialExamUiTests
{
    [Fact]
    public void InformationHasOneSelectableDocumentWithEveryRootLinkVisible()
    {
        using GameAssets assets = InstalledAssets();
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => new HashSet<int>(), _ => { });
        menu.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), () => new(0, 7, 8));
        menu.Open("tutorial");
        menu.SelectEntry("tutorial", "category-6");
        menu.Navigate(MenuInput.Confirm);
        UiView view = menu.BuildView();
        NativeTutorialDocumentElement document = Descendants(view.Root).OfType<NativeTutorialDocumentElement>().Single();
        Assert.DoesNotContain(Descendants(view.Root), element => element is UiList);
        Assert.Equal(9, document.Children.Count);
        foreach (UiElement link in document.Children)
        {
            Assert.True(link.Visible);
            Assert.True(link.Bounds.Y >= document.Bounds.Y && link.Bounds.Bottom <= document.Bounds.Bottom);
        }
        BoundsCanvas canvas = new();
        view.Draw(canvas, new UiTheme());
        Assert.Single(canvas.Texts, item => item.Text == "Basic Terms");
        Assert.Single(canvas.Texts, item => item.Text == "Information");
        document.Children[0].Pointer(document.Children[0].Bounds.X + 40, document.Children[0].Bounds.Y + 10);
        Assert.Equal(238, menu.CurrentNativeInformationPage!.Node.Id);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(0, menu.CurrentNativeInformationPage!.Node.Id);
    }

    [Fact]
    public void EveryOriginalInformationDocumentFitsWithLinksOnTheirAuthoredLines()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        foreach (NativeTutorialInformationNode node in catalog.Information)
        {
            NativeTutorialInformationPage page = catalog.InformationPage(node.Id, Enumerable.Range(0, 128).ToHashSet(), new(0, 7, 8));
            NativeTutorialDocumentElement document = new(page.Text, 36, SystemFontCatalog.DefaultFamily);
            foreach (NativeTutorialInformationLink link in page.Links)
            {
                string[] authoredLines = page.Text.Split("{Line}");
                Assert.Contains(link.Label, authoredLines[link.LineIndex]);
                document.AddLink(link.LineIndex, new UiButton());
            }
            document.Arrange(new UiRect(0, 0, 900, 520));
            BoundsCanvas canvas = new();
            document.Draw(canvas, new UiTheme());
            Assert.All(canvas.Texts, item => Assert.True(item.Bounds.X >= 0 && item.Bounds.Right <= 900.01f && item.Bounds.Y >= 0 && item.Bounds.Bottom <= 520.01f, $"Information node {node.Id}: {item.Bounds}"));
            Assert.All(document.Children, link => Assert.True(link.Visible && link.Bounds.Bottom <= 520.01f));
        }
    }

    [Fact]
    public void TutorialDocumentPreservesMultilineTextColorsAndWrapsWithinItsPanel()
    {
        NativeTutorialDocumentElement document = new("First line{Line}{Line}{Color Green}Green text{Color White}{Line}" + new string('W', 180), 25, SystemFontCatalog.DefaultFamily);
        document.Arrange(new UiRect(0, 0, 400, 200));
        BoundsCanvas canvas = new();
        document.Draw(canvas, new UiTheme());
        Assert.Contains(canvas.Texts, item => item.Text == "First line");
        Assert.Contains(canvas.Texts, item => item.Text == "Green text" && item.Color.G > item.Color.R);
        Assert.True(canvas.Texts.Select(item => item.Bounds.Y).Distinct().Count() > 3);
        Assert.All(canvas.Texts, item => Assert.True(item.Bounds.Bottom <= 200.01f));
    }

    [Fact]
    public void InformationDocumentsUseTheCurrentNativeIconContractInsideColoredText()
    {
        NativeTutorialDocumentElement document = new("{Color Green}{Key CharacterAbility} Ability{Color White}{Line}{Key FireElement}Fire{Line}{Key DefaultKey} Boost{Line}{Key DeathStatus} Death {Key 113} Drain", 25, SystemFontCatalog.DefaultFamily);
        document.Arrange(new UiRect(0, 0, 400, 200));
        BoundsCanvas canvas = new();
        document.Draw(canvas, new UiTheme());
        Assert.Equal(new[] { 219, 288, 136, 272, 284 }, canvas.Images.Select(image => image.Id));
        Assert.DoesNotContain(canvas.Texts, item => item.Text.Contains("{Key", StringComparison.Ordinal));
        Assert.Contains(canvas.Texts, item => item.Text == " Ability" && item.Color.G > item.Color.R);
        Assert.Equal("binding:8", DialogueTextFormatter.Format("{Key 35}", buttonBindingHint: buttons => $"binding:{buttons}"));
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

    private sealed class BoundsCanvas : IUiCanvas
    {
        public List<(string Text, UiRect Bounds, UiColor Color)> Texts { get; } = [];
        public List<UiImageSource> Images { get; } = [];
        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            if (text.Length > 0)
            {
                Texts.Add((text, bounds, color));
            }
        }
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment alignment)
        {
            Text(text, fontSize, bounds, color);
        }
        public void Fill(UiRect bounds, UiColor color)
        {
        }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }
        public void MenuPointer(UiRect bounds)
        {
        }
        public void Image(UiImageSource source, UiRect bounds)
        {
            Images.Add(source);
        }
        public void PushClip(UiRect bounds)
        {
        }
        public void PopClip()
        {
        }
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InformationRestoresTheSelectedTermAndListPageAfterCancel(bool secondPage)
    {
        using GameAssets assets = InstalledAssets();
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => new HashSet<int>(), _ => { });
        menu.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), () => new(0, 7, 8));
        menu.Open("tutorial");
        menu.SelectEntry("tutorial", "category-6");
        menu.Navigate(MenuInput.Confirm);
        menu.SelectEntry(menu.ScreenId, "information-0");
        menu.Navigate(MenuInput.Confirm);
        if (secondPage)
        {
            menu.Navigate(MenuInput.PageRight);
        }
        int listNode = menu.CurrentNativeInformationPage!.Node.Id;
        Assert.True(menu.Entries.Count > 1);
        int selected = menu.Entries.Count - 2;
        string entryId = menu.Entries[selected].Id;
        menu.SelectEntry(menu.ScreenId, entryId);
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(listNode, menu.CurrentNativeInformationPage!.Node.Id);
        Assert.Equal(selected, menu.SelectedIndex);
        Assert.Equal(entryId, menu.SelectedEntry!.Id);
        menu.BuildView();
        Assert.Equal(entryId, menu.View!.FocusedElement!.Id);
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(selected, menu.SelectedIndex);
        if (secondPage)
        {
            menu.Navigate(MenuInput.PageLeft);
            Assert.Equal(0, menu.SelectedIndex);
            menu.SelectEntry(menu.ScreenId, "information-2");
            menu.Navigate(MenuInput.PageRight);
            Assert.Equal(listNode, menu.CurrentNativeInformationPage!.Node.Id);
            Assert.Equal(selected, menu.SelectedIndex);
            menu.Navigate(MenuInput.PageLeft);
            Assert.Equal(2, menu.SelectedIndex);
            menu.Navigate(MenuInput.PageRight);
        }
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(0, menu.CurrentNativeInformationPage!.Node.Id);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("tutorial", menu.ScreenId);
        Assert.Equal("category-6", menu.SelectedEntry!.Id);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }
}
