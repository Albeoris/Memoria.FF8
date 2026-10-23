using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleGuardianForceLearningMenuTests
{
    [Fact]
    public void ReviewFocusesCompletedAbilityAndHighlightsItAfterTheCursorMoves()
    {
        MenuController menu = new(() => Data());
        menu.OpenBattleLearning(Learned());

        Assert.Equal("gf-learn", menu.ScreenId);
        Assert.Equal("gf-0", menu.GuardianForce?.Id);
        Assert.Equal("11", menu.SelectedEntry?.Id);
        Assert.False(menu.SelectedEntry!.Enabled);
        UiList list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.Null(list.ItemTextColor!(0));
        Assert.NotNull(list.ItemTextColor(1));
        Assert.Null(list.ItemTextColor(2));

        menu.Navigate(MenuInput.Down);

        Assert.Equal("12", menu.SelectedEntry?.Id);
        list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.NotNull(list.ItemTextColor!(1));
        Assert.Null(list.ItemTextColor(2));
    }

    [Fact]
    public void CyclingUsesTheFilteredRosterAndRefocusesEachCompletedAbility()
    {
        MenuData data = Data();
        data.GuardianForces.RemoveAt(1);
        MenuController menu = new(() => data);
        menu.OpenBattleLearning(Learned());

        menu.Navigate(MenuInput.PageLeft);

        Assert.Equal("gf-7", menu.GuardianForce?.Id);
        Assert.Equal("71", menu.SelectedEntry?.Id);
        menu.Navigate(MenuInput.PageRight);
        Assert.Equal("gf-0", menu.GuardianForce?.Id);
        Assert.Equal("11", menu.SelectedEntry?.Id);
        menu.Back();
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void CancelVisitsOnlyUnvisitedForcesThenCloses()
    {
        MenuController menu = new(() => Data());
        menu.OpenBattleLearning(Learned());
        menu.Navigate(MenuInput.PageLeft);
        Assert.Equal("gf-7", menu.GuardianForce?.Id);
        menu.Navigate(MenuInput.PageRight);
        Assert.Equal("gf-0", menu.GuardianForce?.Id);

        menu.Navigate(MenuInput.Cancel);

        Assert.True(menu.IsOpen);
        Assert.Equal("gf-3", menu.GuardianForce?.Id);
        Assert.Equal("31", menu.SelectedEntry?.Id);
        menu.Back();
        Assert.False(menu.IsOpen);
        Assert.False(menu.IsBattleLearningReview);
    }

    [Fact]
    public void ChoosingAnotherTargetPersistsThroughTheLiveActionAndKeepsReviewOpen()
    {
        MenuData data = Data();
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, action =>
        {
            actions.Add(action);
            MenuGuardianForce force = data.GuardianForces.Single(force => force.Id == action.TargetId);
            force.LearningAbilityId = action.SubjectId;
        });
        menu.OpenBattleLearning(Learned());
        menu.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
        menu.Navigate(MenuInput.Down);

        menu.Navigate(MenuInput.Confirm);

        MenuAction action = Assert.Single(actions);
        Assert.Equal("gf-learn", action.Kind);
        Assert.Equal("gf-0", action.TargetId);
        Assert.Equal("12", action.SubjectId);
        Assert.Equal("12", data.GuardianForces[0].LearningAbilityId);
        Assert.True(menu.IsOpen);
        Assert.True(menu.IsBattleLearningReview);
        Assert.Equal("gf-learn", menu.ScreenId);
    }

    [Fact]
    public void ReviewWithNoRemainingUnlearnedAbilitiesCanStillBeDismissed()
    {
        MenuData data = Data();
        data.GuardianForces.RemoveRange(1, 2);
        MenuGuardianForce force = data.GuardianForces[0];
        force.LearningChoices.Clear();
        force.LearningChoices.Add(new("11", "Completed", Learned: true));
        List<MenuAction> actions = [];
        MenuController menu = new(() => data, actions.Add);
        menu.OpenBattleLearning(Learned());
        Assert.All(menu.Entries, entry => Assert.False(entry.Enabled));
        menu.Navigate(MenuInput.Confirm);
        Assert.Empty(actions);
        Assert.True(menu.IsOpen);
        menu.Navigate(MenuInput.Cancel);
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void OrdinaryOpenClearsReviewNavigationAndHighlighting()
    {
        MenuController menu = new(() => Data());
        menu.OpenBattleLearning(Learned());

        menu.Open("gf-learn");

        Assert.False(menu.IsBattleLearningReview);
        UiList list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
        Assert.All(Enumerable.Range(0, list.Items.Count), index => Assert.Null(list.ItemTextColor!(index)));
        menu.Back();
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void EmptyReviewClosesImmediately()
    {
        MenuController menu = new(() => new MenuData());
        menu.OpenBattleLearning(new Dictionary<string, string>());
        Assert.False(menu.IsOpen);
        Assert.False(menu.IsBattleLearningReview);
    }

    [Fact]
    public void ItemColorOverrideAppliesToPrimaryAndSecondaryTextOnlyForItsRow()
    {
        UiColor highlight = new(255, 210, 180);
        UiTheme theme = new();
        UiList list = new()
        {
            Items = ["Completed", "Other"],
            SecondaryItems = ["Complete!", "0 / 30"],
            ItemTextColor = index => index == 0 ? highlight : null,
            SelectedIndex = 1,
            RowHeight = 30
        };
        UiView view = new(list);
        view.Resize(400, 100);
        RecordingCanvas canvas = new();

        view.Draw(canvas, theme);

        Assert.Contains(("Completed", highlight), canvas.Texts);
        Assert.Contains(("Complete!", highlight), canvas.Texts);
        Assert.Contains(("Other", theme.Foreground), canvas.Texts);
        Assert.Contains(("0 / 30", theme.Foreground), canvas.Texts);
    }

    private static Dictionary<string, string> Learned()
    {
        return new Dictionary<string, string> { ["gf-0"] = "11", ["gf-3"] = "31", ["gf-7"] = "71" };
    }

    private static MenuData Data()
    {
        MenuData data = new();
        foreach (int id in new[] { 0, 3, 7 })
        {
            int ability = id == 0 ? 10 : id * 10;
            MenuGuardianForce force = new() { Id = $"gf-{id}", Name = $"GF {id}" };
            force.LearningChoices.AddRange([new($"{ability}", "Earlier completed", Learned: true), new($"{ability + 1}", "Just completed", Learned: true), new($"{ability + 2}", "Next target", RequiredPoints: 30)]);
            data.GuardianForces.Add(force);
        }
        data.SupportedActions.Add("gf-learn");
        return data;
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Text, UiColor Color)> Texts { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add((text, color));
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
