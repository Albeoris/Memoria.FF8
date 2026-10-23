using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleResultsSequenceTests
{
    [Fact]
    public void ExplicitZeroNoticeSuppressesApPageDespiteNonzeroAwards()
    {
        BattleRewardSummary rewards = Summary() with { AbilityPointsNotice = 0, GuardianForceAbilityPoints = new Dictionary<int, ushort> { [0] = 12 } };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();

        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.Items, hud.ResultPage);
        Confirm(hud);

        Assert.Equal(1, completions);
        Assert.Equal(0, hud.ResultAbilityPoints);
    }

    [Fact]
    public void ExplicitNoticeRendersWithoutPerGuardianAwards()
    {
        BattleRewardSummary rewards = Summary() with { AbilityPointsNotice = 7 };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.AbilityPoints, hud.ResultPage);
        Settle(hud);
        RecordingCanvas canvas = new();

        hud.BuildView().Draw(canvas, new UiTheme());

        Assert.Contains("Received 7 AP!", canvas.Texts);
        Assert.Empty(rewards.GuardianForceAbilityPoints);
        Assert.Equal(0, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
    }

    [Fact]
    public void GroupedOffersPreserveFirstOrderByteQuantitiesAndMixedFailure()
    {
        BattleRewardOffer[] original = [new(false, 1, 250, true), new(true, 4, 1, true), new(false, 2, 3, true), new(true, 4, 1, false), new(false, 1, 10, true)];
        BattleRewardOffer[] before = original.ToArray();

        BattleRewardOffer[] grouped = NativeBattleResultPresentation.GroupLootOffers(original);

        Assert.Equal(new BattleRewardOffer[] { new(false, 1, 4, true), new(true, 4, 2, false), new(false, 2, 3, true) }, grouped);
        Assert.Equal(before, original);
    }

    [Fact]
    public void ConfiguredNativeMessagesUseDialogTagsAndFailFastWhenMissing()
    {
        BattleSession session = Session();
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { });
        string?[] messages = new string?[128];
        messages[109] = "Received {Dialog CardLevel} AP!";
        messages[110] = "{Dialog 35} Card";
        hud.ConfigureResults(messages);

        Assert.Equal("Received 7 AP!", hud.ResultText(109, "fallback", 7));
        Assert.Equal("Bite Bug Card", hud.ResultText(110, "fallback", subject: "Bite Bug"));
        Assert.Throws<NotSupportedException>(() => hud.ResultText(23, "fallback"));
    }

    [Fact]
    public void ConsecutiveGuardianNoticesRequireTheirOwnTransitionBeforeConfirm()
    {
        BattleRewardSummary rewards = Summary() with { GuardianForceGrowth = [new(0, 1, 2, []), new(1, 1, 2, [])] };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Confirm(hud);
        Assert.Equal(1, hud.ResultGuardianForce!.Id);
        Assert.Equal(0f, hud.ResultOpacity);
        hud.ConfirmResult();
        Assert.Equal(0, completions);
        Assert.Equal(1, hud.ResultGuardianForce!.Id);
        Confirm(hud);
        Assert.Equal(1, completions);
    }
    [Fact]
    public void LearnedGuardiansOpenTheirMenuBeforeTheSingleFinalCompletion()
    {
        BattleRewardSummary rewards = Summary() with { GuardianForceGrowth = [new(0, 1, 2, [11]), new(1, 1, 2, []), new(7, 1, 1, [71])] };
        BattleSession session = Session();
        int completions = 0;
        int menusOpened = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        hud.ConfigureGuardianForceLearning(summary =>
        {
            menusOpened++;
            MenuData data = new();
            foreach (BattleGuardianForceRewardPresentation growth in summary.GuardianForceGrowth.Where(growth => growth.LearnedAbilityIds.Length > 0))
            {
                MenuGuardianForce force = new() { Id = $"gf-{growth.Id}", Name = $"GF {growth.Id}" };
                force.LearningChoices.Add(new(growth.LearnedAbilityIds[0].ToString(), "Completed", Learned: true));
                data.GuardianForces.Add(force);
            }
            MenuController menu = new(() => data);
            menu.OpenBattleLearning(summary.GuardianForceGrowth.Where(growth => growth.LearnedAbilityIds.Length > 0).ToDictionary(growth => $"gf-{growth.Id}", growth => growth.LearnedAbilityIds[0].ToString()));
            return menu;
        });
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Assert.Equal(1, hud.ResultGuardianForce!.Id);
        Confirm(hud);
        Assert.Equal(BattleResultPage.LearnedAbilities, hud.ResultPage);
        Assert.Equal(new[] { (0, 11), (7, 71) }, hud.ResultLearnedAbilities);
        Assert.Equal(0, menusOpened);
        Confirm(hud);
        Assert.Equal(1, menusOpened);
        Assert.Equal(0, completions);
        MenuController learning = hud.GuardianForceLearningMenu!;
        Assert.Equal("gf-0", learning.GuardianForce!.Id);
        Assert.Same(learning.BuildView(), hud.BuildView());
        hud.Navigate(MenuInput.PageLeft);
        Assert.Equal("gf-7", learning.GuardianForce!.Id);
        hud.Navigate(MenuInput.Cancel);
        Assert.Equal(1, completions);
        hud.Navigate(MenuInput.Cancel);
        hud.ConfirmResult();
        Assert.Equal(1, completions);
    }

    [Fact]
    public void TwelveLearnedGuardiansShareThreeColumnsAndFourRowsAndOneConfirmation()
    {
        BattleRewardSummary rewards = Summary() with { GuardianForceGrowth = Enumerable.Range(0, 12).Select(id => new BattleGuardianForceRewardPresentation(id, 1, 1, [20 + id])).ToArray() };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards, guardianForceName: id => $"Guardian {id}");
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.LearnedAbilities, hud.ResultPage);
        Settle(hud);
        RecordingCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);

        view.Draw(canvas, new UiTheme());

        var portraits = canvas.Images.Where(image => image.Source.Kind == UiImageKind.GuardianForcePortrait).ToArray();
        Assert.Equal(Enumerable.Range(0, 12), portraits.Select(image => image.Source.Id));
        Assert.Equal(3, portraits.Select(image => image.Bounds.X).Distinct().Count());
        Assert.Equal(4, portraits.Select(image => image.Bounds.Y).Distinct().Count());
        Assert.All(portraits.GroupBy(image => image.Bounds.X), column => Assert.Equal(4, column.Count()));
        Assert.All(portraits.GroupBy(image => image.Bounds.Y), row => Assert.Equal(3, row.Count()));
        Assert.All(portraits, portrait =>
        {
            Assert.True(portrait.Bounds.Width > 0);
            Assert.True(portrait.Bounds.Height > 0);
            Assert.InRange(portrait.Bounds.X, 0, 960 - portrait.Bounds.Width);
            Assert.InRange(portrait.Bounds.Y, 0, 672 - portrait.Bounds.Height);
        });
        Assert.DoesNotContain(canvas.Texts, text => text.StartsWith("Guardian ", StringComparison.Ordinal));
        Assert.All(Enumerable.Range(20, 12), ability => Assert.Contains(canvas.Texts, text => text.Contains($"Ability {ability}", StringComparison.Ordinal)));
        Assert.Equal(0, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
    }

    [Fact]
    public void LearnedSummaryPreservesEveryAbilityAcrossPages()
    {
        int[] abilities = Enumerable.Range(20, 14).ToArray();
        BattleRewardSummary rewards = Summary() with { GuardianForceGrowth = [new(7, 1, 1, abilities)] };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.LearnedAbilities, hud.ResultPage);
        Assert.Equal(abilities.Take(12).Select(ability => (7, ability)), hud.ResultLearnedAbilities);

        Confirm(hud);

        Assert.Equal(BattleResultPage.LearnedAbilities, hud.ResultPage);
        Assert.Equal(abilities.Skip(12).Select(ability => (7, ability)), hud.ResultLearnedAbilities);
        Assert.Equal(0f, hud.ResultOpacity);
        hud.ConfirmResult();
        Assert.Equal(0, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
    }

    private static BattleRewardSummary Summary()
    {
        return new BattleRewardSummary(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>(), new Dictionary<int, ushort>(), [], []);
    }

    private static BattleSession Session()
    {
        return new BattleSession(new BattleDefinition { Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true }, new() { Id = "enemy-0", Name = "Bite Bug" }] }, BattleAtbMode.Normal);
    }

    private static void Settle(BattleHudController hud)
    {
        double frames = (1 - hud.ResultOpacity) * 16;
        if (frames > 0)
        {
            hud.AdvancePresentation(TimeSpan.FromTicks((long)Math.Ceiling(frames * TimeSpan.TicksPerSecond / BattleSession.FramesPerSecond)));
        }
    }

    private static void Confirm(BattleHudController hud)
    {
        Settle(hud);
        hud.ConfirmResult();
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
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
            Images.Add((source, bounds));
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
