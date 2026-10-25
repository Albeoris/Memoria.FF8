using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleResultsSequenceTests
{
    [Fact]
    public void ExplicitZeroNoticeSuppressesGuardianSummaryDespiteNonzeroAwards()
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
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Settle(hud);
        RecordingCanvas canvas = new();

        hud.BuildView().Draw(canvas, new UiTheme());

        Assert.Contains("Received 7 AP!", canvas.Texts);
        Assert.Empty(hud.ResultGuardianForceRows);
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
    public void GuardianLevelChangesShareOneSummaryAndOneConfirmation()
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
        Assert.Equal(new[] { 0, 1 }, hud.ResultGuardianForceRows.Select(row => row.Growth.Id));
        hud.ConfirmResult();
        Assert.Equal(0, completions);
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
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Assert.Equal(new[] { (0, (int?)11), (1, (int?)null), (7, (int?)71) }, hud.ResultGuardianForceRows.Select(row => (row.Growth.Id, row.AbilityId)));
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
    public void SixteenLearnedGuardiansShareTwoColumnsAndEightRowsAndOneConfirmation()
    {
        BattleRewardSummary rewards = Summary() with { GuardianForceGrowth = Enumerable.Range(0, 16).Select(id => new BattleGuardianForceRewardPresentation(id, 1, 1, [20 + id])).ToArray() };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards, guardianForceName: id => $"Guardian {id}");
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Settle(hud);
        RecordingCanvas canvas = new();
        UiView view = hud.BuildView();
        view.Resize(960, 672);

        view.Draw(canvas, new UiTheme());

        var portraits = canvas.Images.Where(image => image.Source.Kind == UiImageKind.GuardianForcePortrait).ToArray();
        Assert.Equal(Enumerable.Range(0, 16), portraits.Select(image => image.Source.Id));
        Assert.Equal(2, portraits.Select(image => image.Bounds.X).Distinct().Count());
        Assert.Equal(8, portraits.Select(image => image.Bounds.Y).Distinct().Count());
        Assert.All(portraits.GroupBy(image => image.Bounds.X), column => Assert.Equal(8, column.Count()));
        Assert.All(portraits.GroupBy(image => image.Bounds.Y), row => Assert.Equal(2, row.Count()));
        var levels = canvas.TextDraws.Where(draw => draw.Text == "Lv. 1").ToArray();
        var abilities = canvas.TextDraws.Where(draw => draw.Text.StartsWith("Ability ", StringComparison.Ordinal)).ToArray();
        Assert.Equal(16, levels.Length);
        Assert.Equal(16, abilities.Length);
        Assert.Equal(2, levels.Select(draw => draw.Bounds.X).Distinct().Count());
        Assert.Equal(2, abilities.Select(draw => draw.Bounds.X).Distinct().Count());
        for (int index = 0; index < portraits.Length; index++)
        {
            Assert.Equal(levels[index].Bounds.Y, abilities[index].Bounds.Y);
            Assert.True(levels[index].Bounds.X > portraits[index].Bounds.X);
            Assert.True(abilities[index].Bounds.X > levels[index].Bounds.X + levels[index].Bounds.Width);
        }
        Assert.All(portraits, portrait =>
        {
            Assert.True(portrait.Bounds.Width > 0);
            Assert.True(portrait.Bounds.Height > 0);
            Assert.InRange(portrait.Bounds.X, 0, 960 - portrait.Bounds.Width);
            Assert.InRange(portrait.Bounds.Y, 0, 672 - portrait.Bounds.Height);
        });
        Assert.DoesNotContain(canvas.Texts, text => text.StartsWith("Guardian ", StringComparison.Ordinal));
        Assert.All(Enumerable.Range(20, 16), ability => Assert.Contains(canvas.Texts, text => text.Contains($"Ability {ability}", StringComparison.Ordinal)));
        Assert.Equal(0, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
    }

    [Fact]
    public void LearnedSummaryPreservesEveryAbilityAcrossPages()
    {
        int[] abilities = Enumerable.Range(20, 18).ToArray();
        BattleRewardSummary rewards = Summary() with { GuardianForceGrowth = [new(7, 1, 1, abilities)] };
        BattleSession session = Session();
        int completions = 0;
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, () => completions++, previewRewards: () => rewards);
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Assert.Equal(abilities.Take(16).Select(ability => (7, (int?)ability)), hud.ResultGuardianForceRows.Select(row => (row.Growth.Id, row.AbilityId)));

        Confirm(hud);

        Assert.Equal(BattleResultPage.GuardianForce, hud.ResultPage);
        Assert.Equal(abilities.Skip(16).Select(ability => (7, (int?)ability)), hud.ResultGuardianForceRows.Select(row => (row.Growth.Id, row.AbilityId)));
        Assert.Equal(0f, hud.ResultOpacity);
        hud.ConfirmResult();
        Assert.Equal(0, completions);
        Confirm(hud);
        Assert.Equal(1, completions);
    }

    [Fact]
    public void MixedGuardianSummaryAlignsRowsAndAnimatesOnlyLearnedAbilitiesAndIncreasedLevels()
    {
        BattleRewardSummary rewards = Summary() with
        {
            AbilityPointsNotice = 7,
            GuardianForceGrowth = [new(0, 1, 2, [20, 21]), new(1, 3, 3, [22]), new(2, 4, 5, []), new(3, 6, 6, [])]
        };
        BattleSession session = Session();
        BattleHudController hud = new(session, (_, _) => { }, () => { }, () => { }, previewRewards: () => rewards);
        session.InstantVictory();
        Confirm(hud);
        Confirm(hud);
        Confirm(hud);
        Settle(hud);
        Assert.Equal(new[] { (0, (int?)20), (0, (int?)21), (1, (int?)22), (2, (int?)null) }, hud.ResultGuardianForceRows.Select(row => (row.Growth.Id, row.AbilityId)));
        UiView view = hud.BuildView();
        view.Resize(960, 672);
        RecordingCanvas first = new();
        view.Draw(first, new UiTheme());
        hud.AdvancePresentation(TimeSpan.FromSeconds(0.25));
        RecordingCanvas later = new();
        view.Draw(later, new UiTheme());

        Assert.Contains("Received 7 AP!", first.Texts);
        Assert.DoesNotContain("Lv. 6", first.Texts);
        Assert.Equal(4, first.Images.Count(image => image.Source.Kind == UiImageKind.GuardianForcePortrait));
        Assert.Equal(3, first.TextDraws.Count(draw => draw.Text == "↑"));
        Assert.Equal(2, first.TextDraws.Count(draw => draw.Text == "Lv. 2"));
        Assert.Single(first.TextDraws, draw => draw.Text == "Lv. 3");
        Assert.Single(first.TextDraws, draw => draw.Text == "Lv. 5");
        foreach (var portrait in first.Images.Where(image => image.Source.Kind == UiImageKind.GuardianForcePortrait))
        {
            var level = Assert.Single(first.TextDraws, draw => draw.Text.StartsWith("Lv. ", StringComparison.Ordinal) && draw.Bounds.Y >= portrait.Bounds.Y && draw.Bounds.Y < portrait.Bounds.Y + portrait.Bounds.Height);
            Assert.True(level.Bounds.X > portrait.Bounds.X);
        }
        foreach (string text in new[] { "Ability 20", "Ability 21", "Ability 22", "Lv. 2", "Lv. 5", "↑" })
        {
            UiColor[] before = first.TextDraws.Where(draw => draw.Text == text).Select(draw => draw.Color).ToArray();
            UiColor[] after = later.TextDraws.Where(draw => draw.Text == text).Select(draw => draw.Color).ToArray();
            Assert.NotEmpty(before);
            Assert.Equal(before.Length, after.Length);
            Assert.All(before.Zip(after), pair => Assert.NotEqual(pair.First, pair.Second));
        }
        foreach (string text in new[] { "Lv. 3", "Received 7 AP!" })
        {
            Assert.Equal(first.TextDraws.Where(draw => draw.Text == text).Select(draw => draw.Color), later.TextDraws.Where(draw => draw.Text == text).Select(draw => draw.Color));
        }
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
        public List<(string Text, UiRect Bounds, UiColor Color)> TextDraws { get; } = [];
        public List<(UiImageSource Source, UiRect Bounds)> Images { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth)
        {
            return new UiSize(Math.Min(maximumWidth, text.Length * fontSize / 2), fontSize);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
            TextDraws.Add((text, bounds, color));
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
