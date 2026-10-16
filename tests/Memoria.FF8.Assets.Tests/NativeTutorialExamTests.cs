using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeTutorialExamTests
{
    [Fact]
    public void OriginalWrittenTestsKeepAllThreeHundredQuestionsAndExactBinaryAnswers()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        Assert.Same(catalog, assets.ReadNativeTutorialExamCatalog());
        Assert.Equal(30, catalog.Levels.Length);
        Assert.Equal(300, catalog.Levels.Sum(level => level.Questions.Length));
        Assert.Equal(new byte[] { 0, 1, 0, 0, 0, 1, 1, 0, 1, 1 }, catalog.Levels[0].Questions.Select(question => question.CorrectAnswer));
        Assert.Contains("The Draw command extracts magic from enemies.", catalog.Levels[0].Questions[0].Text);
        Assert.Contains("GF stands for Garden Fighter.", catalog.Levels[0].Questions[1].Text);
        Assert.All(catalog.Levels.SelectMany(level => level.Questions), question =>
        {
            Assert.Equal(new[] { "YES", "NO" }, question.Options);
            Assert.NotEmpty(question.Bytes);
            Assert.DoesNotContain("{Option", question.Text);
        });
    }

    [Fact]
    public void OriginalTestEligibilityAndPerfectScoreOnlyProgressStayInTheReturnedIsolatedCopy()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        byte[] correct = catalog.Levels[0].Questions.Select(question => question.CorrectAnswer).ToArray();
        NativeTutorialExamProgress source = new(0, 7, 8);
        NativeTutorialExamResult success = catalog.Evaluate(source, 1, correct, false);
        Assert.Equal(100, success.Score);
        Assert.True(success.Passed);
        Assert.Equal(2, success.PromptIndex);
        Assert.Equal(new NativeTutorialExamProgress(1, 8, 8), success.Progress);
        Assert.Equal(new NativeTutorialExamProgress(0, 7, 8), source);
        correct[0] ^= 1;
        NativeTutorialExamResult failure = catalog.Evaluate(source, 1, correct, false);
        Assert.Equal(90, failure.Score);
        Assert.Same(source, failure.Progress);
        correct[0] ^= 1;
        NativeTutorialExamProgress maximum = source with { SeeDRank = 31 };
        NativeTutorialExamResult atMaximum = catalog.Evaluate(maximum, 1, correct, false);
        Assert.Equal(3, atMaximum.PromptIndex);
        Assert.Equal(31, atMaximum.Progress.SeeDRank);
        NativeTutorialExamProgress review = new(1, 7, 1);
        Assert.Same(review, catalog.Evaluate(review, 1, correct, true).Progress);
        Assert.Throws<InvalidOperationException>(() => catalog.Evaluate(source, 1, correct, true));
        Assert.Throws<InvalidOperationException>(() => catalog.Evaluate(source, 2, correct, false));
        Assert.Throws<InvalidOperationException>(() => catalog.Evaluate(source with { PassedTests = 8 }, 9, correct, false));
        Assert.Throws<ArgumentException>(() => catalog.Evaluate(source, 1, new byte[9], false));
    }

    [Fact]
    public void InformationUsesOriginalNodeLinksAndConditionalAvailabilityWithoutChangingTheInputSet()
    {
        using GameAssets assets = InstalledAssets();
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        Assert.Equal(425, catalog.Information.Length);
        HashSet<int> unlocked = [];
        NativeTutorialExamProgress progress = new(0, 7, 8);
        NativeTutorialInformationPage root = catalog.InformationPage(0, unlocked, progress);
        Assert.Equal("Select term", root.Node.Title);
        Assert.Contains(root.Links, link => link.TargetId == 238 && link.Label == "Basic Terms");
        Assert.DoesNotContain(root.Links, link => link.TargetId == 424);
        unlocked.Add(127);
        Assert.Contains(catalog.InformationPage(0, unlocked, progress).Links, link => link.TargetId == 424 && link.Label == "Proof of Omega");
        Assert.Equal(new[] { 127 }, unlocked);
        HashSet<int> all = Enumerable.Range(0, 128).ToHashSet();
        foreach (NativeTutorialInformationNode node in catalog.Information)
        {
            NativeTutorialInformationPage page = catalog.InformationPage(node.Id, all, progress);
            Assert.All(page.Links, link => Assert.InRange(link.TargetId, 0, 424));
            Assert.DoesNotContain("{Option", page.Text);
        }
        NativeTutorialInformationNode report = catalog.Information.Single(node => node.Title == "Battle Report");
        var numbers = new Dictionary<int, long> { [192] = 12345, [193] = 71, [194] = 70, [195] = 1 };
        string body = catalog.InformationPage(report.Id, all, progress with { InformationNumbers = numbers }).Text;
        Assert.Contains("12345", body);
        Assert.Contains("70", body);
        Assert.DoesNotContain("{Dialog", body);
    }

    private static GameAssets InstalledAssets()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Installed original tutorial resources are required.");
        return new GameAssets(directory);
    }
}
