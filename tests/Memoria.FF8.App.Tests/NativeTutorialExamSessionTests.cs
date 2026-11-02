using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialExamSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FivePassedTestsSurviveReopeningANewMenuAndSavingTheSession(bool compact)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        GameSession session = CreateSession(assets, 599);
        NativeSeeDState before = session.SeeD!;
        string party = System.Text.Json.JsonSerializer.Serialize(session.CharacterVitals);
        MenuController menu = CreateMenu(assets, session);
        for (int level = 1; level <= 5; level++)
        {
            Answer(menu, catalog, level);
            Assert.Equal((byte)level, session.SeeD!.TestLevel);
            Assert.Equal((ushort)((5 + level) * 100), session.SeeD.RankPoints);
            Assert.Equal(before with { TestLevel = (byte)level, RankPoints = (ushort)((5 + level) * 100) }, session.SeeD);
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal((byte)level, session.SeeD.TestLevel);
            menu.Navigate(MenuInput.Cancel);
            menu.SelectEntry("tutorial", "category-3");
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal((level + 1).ToString(), menu.Entries.Single(entry => entry.Id == "yes").Action!.SubjectId);
            menu = CreateMenu(assets, session);
            Assert.Equal((level + 1).ToString(), menu.Entries.Single(entry => entry.Id == "yes").Action!.SubjectId);
        }
        Assert.Equal(party, System.Text.Json.JsonSerializer.Serialize(session.CharacterVitals));
        string path = TestResources.ArtifactFile(compact ? "exam-compact.json" : "exam.json");
        session.Save(path, compact);
        GameSession restored = new(GameSession.Load(path));
        Assert.Equal(session.SeeD, restored.SeeD);
        MenuController reopened = CreateMenu(assets, restored);
        Assert.Equal("6", reopened.Entries.Single(entry => entry.Id == "yes").Action!.SubjectId);
        Answer(reopened, catalog, 6);
        Assert.Equal((byte)6, restored.SeeD!.TestLevel);
        Assert.Equal((ushort)1100, restored.SeeD.RankPoints);
    }

    [Fact]
    public void LiveFieldSnapshotKeepsRootAndEventSeeDOwnersInAgreement()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        // Fresh diagnostic field allocation; no existing gameplay checkpoint is advanced.
        session.StartNewGame();
        session.SetSeeD(session.SeeD! with { SalaryUiEnabled = true, RankPoints = 799 });
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        MenuController menu = CreateMenu(assets, session);
        Answer(menu, assets.ReadNativeTutorialExamCatalog(), 1);
        string path = TestResources.ArtifactFile("exam-field.json");
        session.Save(path, compact: true);
        GameSession restored = new(GameSession.Load(path));
        Assert.Equal(session.SeeD, restored.SeeD);
        using GameWorld restoredWorld = new(restored, TestResources.GameDirectory);
        restoredWorld.EnsureField();
        Assert.Equal(session.SeeD, restored.SeeD);
        Assert.Equal((byte)1, restored.SeeD!.TestLevel);
        Assert.Equal((ushort)800, restored.SeeD.RankPoints);
        Assert.Equal("2", CreateMenu(assets, restored).Entries.Single(entry => entry.Id == "yes").Action!.SubjectId);
    }

    [Fact]
    public void FailedTestsAndReviewNeverChangePersistentOwners()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialExamCatalog catalog = assets.ReadNativeTutorialExamCatalog();
        GameSession session = CreateSession(assets, 749, 1);
        NativeSeeDState before = session.SeeD!;
        MenuController menu = CreateMenu(assets, session);
        Answer(menu, catalog, 2, incorrect: true);
        Assert.Equal(90, menu.NativeTutorialExamResult!.Score);
        Assert.Equal(before, session.SeeD);
        menu.Open("tutorial");
        menu.SelectEntry("tutorial", "category-4");
        menu.Navigate(MenuInput.Confirm);
        menu.SelectEntry(menu.ScreenId, "review-1");
        menu.Navigate(MenuInput.Confirm);
        AnswerQuestions(menu, catalog, 1, false);
        Assert.True(menu.NativeTutorialExamResult!.Passed);
        Assert.Equal(before, session.SeeD);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(before, session.SeeD);
    }

    [Theory]
    [InlineData(3099, 3100)]
    [InlineData(3100, 3100)]
    [InlineData(3199, 3199)]
    public void RankACapsPromotionAndStillPersistsCompletedTests(int initial, int expected)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = CreateSession(assets, (ushort)initial);
        MenuController menu = CreateMenu(assets, session);
        Answer(menu, assets.ReadNativeTutorialExamCatalog(), 1);
        Assert.Equal((ushort)expected, session.SeeD!.RankPoints);
        Assert.Equal((byte)1, session.SeeD.TestLevel);
        Assert.Equal(31, menu.NativeTutorialExamProgress!.SeeDRank);
    }

    [Fact]
    public void RankACanCompleteTestsAboveCharacterLevelAndStopsAtThirty()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = new(GameSession.CreateColdProcess().State with { SeeD = NativeSeeDState.CreateNewGame() with { SalaryUiEnabled = true, RankPoints = 3100, TestLevel = 29 } });
        MenuController menu = CreateMenu(assets, session);
        Answer(menu, assets.ReadNativeTutorialExamCatalog(), 30);
        menu = CreateMenu(assets, session);
        Assert.Equal((byte)30, session.SeeD!.TestLevel);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == "yes");
    }

    [Fact]
    public void ReconnectingAndExternalStateChangesDiscardThePreviousMenusProgress()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = CreateSession(assets, 700, 4);
        MenuController menu = CreateMenu(assets, session);
        Assert.Equal("5", menu.Entries.Single(entry => entry.Id == "yes").Action!.SubjectId);
        session.SetSeeD(session.SeeD! with { TestLevel = 2, RankPoints = 800 });
        menu.Execute(new MenuAction("native-exam-open", "test"));
        Assert.Equal(2, menu.NativeTutorialExamProgress!.PassedTests);
        menu.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), CreateSession(assets, 700));
        menu.Execute(new MenuAction("native-exam-open", "test"));
        Assert.Equal(0, menu.NativeTutorialExamProgress!.PassedTests);
    }

    private static GameSession CreateSession(GameAssets assets, ushort rank, byte passed = 0)
    {
        // Diagnostic owners for menu tests; no gameplay route is reconstructed.
        return new GameSession(GameSession.CreateColdProcess().State with
        {
            SeeD = NativeSeeDState.CreateNewGame() with { SalaryUiEnabled = true, RankPoints = rank, TestLevel = passed, SalaryMovementCounter = 123, KillBaseline18 = 9, PreviousRank186 = 5 },
            CharacterVitals = assets.ReadNewGameCharacterVitals()
        });
    }

    private static MenuController CreateMenu(GameAssets assets, GameSession session)
    {
        MenuController menu = new();
        menu.ConnectNativeTutorials(assets.ReadNativeTutorialCatalog(), assets.ReadNativeTutorialBrowserCatalog(), () => new HashSet<int>(), _ => throw new InvalidOperationException("Written tests do not start demonstrations."));
        menu.ConnectNativeTutorialExam(assets.ReadNativeTutorialExamCatalog(), session);
        menu.Open("tutorial");
        menu.SelectEntry("tutorial", "category-3");
        menu.Navigate(MenuInput.Confirm);
        return menu;
    }

    private static void Answer(MenuController menu, NativeTutorialExamCatalog catalog, int level, bool incorrect = false)
    {
        menu.SelectEntry(menu.ScreenId, "yes");
        menu.Navigate(MenuInput.Confirm);
        AnswerQuestions(menu, catalog, level, incorrect);
    }

    private static void AnswerQuestions(MenuController menu, NativeTutorialExamCatalog catalog, int level, bool incorrect)
    {
        foreach (NativeTutorialExamQuestion question in catalog.Levels[level - 1].Questions)
        {
            int answer = question.CorrectAnswer ^ (incorrect && question.Index == 0 ? 1 : 0);
            menu.SelectEntry(menu.ScreenId, answer.ToString(System.Globalization.CultureInfo.InvariantCulture));
            menu.Navigate(MenuInput.Confirm);
        }
        Assert.Equal("tutorial-native-exam-result", menu.ScreenId);
    }
}
