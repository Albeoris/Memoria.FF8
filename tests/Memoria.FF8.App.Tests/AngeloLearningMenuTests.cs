using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class AngeloLearningMenuTests
{
    private static NativeLimitProgress Progress(byte flags) => new(new string('A', 64), 0, 0, flags, 2, [0, 0, 0, 100, 0, 0, 0, 0]);

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void CompletionSelectsRinoaAndTheNewMoveAndKeepsHighlightWhenCursorMoves(MenuPresentation presentation)
    {
        MenuController menu = CreateMenu();
        menu.Presentation = presentation;
        menu.Open("status");
        menu.Execute(new MenuAction("select-character", "character-0"));

        Assert.True(menu.PresentAngeloLearning(Progress(1), Progress(5)));

        Assert.Equal("status-angelo", menu.ScreenId);
        Assert.Equal("character-4", menu.Character!.Id);
        Assert.Equal("limit-2", menu.SelectedEntry!.Id);
        UiList list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.NotNull(list.ItemTextColor!(1));
        Assert.Null(list.ItemTextColor(0));
        Assert.Null(list.ItemTextColor(2));
        menu.Navigate(MenuInput.Down);
        Assert.Equal("limit-3", menu.SelectedEntry!.Id);
        list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.NotNull(list.ItemTextColor!(1));
        menu.Navigate(MenuInput.Cancel);
        Assert.False(menu.IsOpen);
        menu.Open("status-angelo");
        list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.All(Enumerable.Range(0, list.Items.Count), index => Assert.Null(list.ItemTextColor!(index)));
    }

    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.WorldMap)]
    public void ClosingReviewPreservesTheUnderlyingSceneAndSavedProgress(SceneKind scene)
    {
        GameState state = new() { Scene = scene, WorldArrival = scene == SceneKind.WorldMap ? new WorldTransition(0, 0, 0) : null, LimitProgress = Progress(5) };
        GameSession session = new(state);
        MenuController menu = CreateMenu();

        Assert.True(menu.PresentAngeloLearning(Progress(1), session.LimitProgress));
        menu.Back();

        Assert.False(menu.IsOpen);
        Assert.Same(state, session.State);
        Assert.False(menu.PresentAngeloLearning(session.LimitProgress, session.LimitProgress));
    }

    [Fact]
    public void CounterProgressExistingLearningAndAbsentOwnersDoNotOpenReview()
    {
        MenuController menu = CreateMenu();
        Assert.False(menu.PresentAngeloLearning(null, Progress(5)));
        Assert.False(menu.PresentAngeloLearning(Progress(1), null));
        Assert.False(menu.PresentAngeloLearning(Progress(1), Progress(1) with { TrainingCounters = [0, 0, 0, 99, 0, 0, 0, 0] }));
        Assert.False(menu.PresentAngeloLearning(Progress(5), Progress(1)));
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void NextTrainingCanBeSelectedBeforeClosingReview()
    {
        List<MenuAction> actions = [];
        MenuController menu = CreateMenu(actions.Add);
        menu.PresentAngeloLearning(Progress(1), Progress(5));
        menu.Navigate(MenuInput.Down);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(new MenuAction("angelo-learn", "limit-3", "character-4"), Assert.Single(actions));
        Assert.True(menu.IsOpen);
        menu.Back();
        Assert.False(menu.IsOpen);
    }

    private static MenuController CreateMenu(Action<MenuAction>? action = null)
    {
        MenuData data = new();
        data.SupportedActions.Add("angelo-learn");
        data.Characters.Add(new MenuCharacter { Id = "character-0", Name = "Squall" });
        MenuCharacter rinoa = new() { Id = "character-4", Name = "Rinoa", NativeLimitCommand = 19 };
        rinoa.LimitMoves.AddRange([
            new() { Id = "limit-0", Name = "Old", Learned = true },
            new() { Id = "limit-2", Name = "New", Learned = true },
            new() { Id = "limit-3", Name = "Next", Learned = false, TrainingAvailable = true }
        ]);
        data.Characters.Add(rinoa);
        return new MenuController(() => data, action);
    }
}
