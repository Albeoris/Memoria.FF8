using Memoria.FF8.Runtime;
using Memoria.FF8.Mods;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.Tests;

public sealed class StartupSequenceTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void Policy_skips_only_selected_stages_and_keeps_manual_skip_intrinsic(int mask)
    {
        StartupPolicy policy = new((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0);
        List<StartupStage> expected = [];
        if (!policy.SkipPublisherMovie)
        {
            expected.Add(StartupStage.SquareEnixMovie);
            expected.Add(StartupStage.PublisherMovie);
        }
        if (!policy.SkipSquareLogo)
        {
            expected.Add(StartupStage.SquareLogo);
        }
        if (!policy.SkipCreditsImages)
        {
            expected.Add(StartupStage.Credits);
        }
        StartupSequence sequence = new(true);
        sequence.Configure(policy);
        List<StartupStage> actual = [];
        while (sequence.Active)
        {
            actual.Add(sequence.Stage);
            sequence.Advance(TimeSpan.FromSeconds(1));
            Assert.True(sequence.HandleInput(true, false));
            Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
        }
        Assert.Equal(expected, actual);
        Assert.Same(policy, sequence.Policy);

        StartupSequence cancelled = new(true);
        cancelled.Configure(policy);
        if (cancelled.Active)
        {
            cancelled.Advance(TimeSpan.FromSeconds(1));
            Assert.True(cancelled.HandleInput(false, true));
        }
        Assert.False(cancelled.Active);
        Assert.Equal(TimeSpan.Zero, cancelled.Elapsed);

        StartupSequence disabled = new(false);
        disabled.Configure(policy);
        disabled.RepeatCredits();
        Assert.False(disabled.Active);
    }

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 3)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 4)]
    public void Natural_repeat_respects_skips_and_resets_elapsed(bool skipSquare, bool skipCredits, int expected)
    {
        StartupSequence sequence = new(true);
        sequence.Configure(new(SkipSquareLogo: skipSquare, SkipCreditsImages: skipCredits));
        sequence.Advance(TimeSpan.FromSeconds(3));
        sequence.RepeatCredits();
        Assert.Equal((StartupStage)expected, sequence.Stage);
        Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
    }

    [Fact]
    public void Initial_unpublished_catalog_does_not_freeze_default_policy_before_real_publication()
    {
        StartupSequence sequence = new(true);
        Assert.False(sequence.TryConfigure(StartupPolicy.Default, 0));
        Assert.False(sequence.IsConfigured);
        Assert.Equal(StartupStage.SquareEnixMovie, sequence.Stage);
        Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
        Assert.False(sequence.TryConfigure(StartupPolicy.Default, 0));
        Assert.True(sequence.TryConfigure(new(true, true, true, true), 1));
        Assert.True(sequence.IsConfigured);
        Assert.False(sequence.Active);
        Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
    }

    [Fact]
    public void First_published_policy_keeps_disabled_startup_complete()
    {
        StartupSequence sequence = new(false);
        Assert.False(sequence.TryConfigure(StartupPolicy.Default, 0));
        Assert.False(sequence.Active);
        Assert.True(sequence.TryConfigure(new(true, true, true, true), 1));
        Assert.False(sequence.Active);
    }

    [Fact]
    public void Policy_is_frozen_for_the_current_presentation()
    {
        StartupSequence sequence = new(true);
        sequence.Configure(new(SkipPublisherMovie: true));
        Assert.Throws<InvalidOperationException>(() => sequence.Configure(StartupPolicy.Default));
        Assert.Equal(StartupStage.SquareLogo, sequence.Stage);
    }

    [Fact]
    public void Only_active_junction_packages_contribute_optional_policy()
    {
        JunctionManifestMetadata metadata = new() { Behaviors = [JunctionBehavior.SkipPublisherMovie, JunctionBehavior.SkipSquareLogo, JunctionBehavior.SkipCreditsPause, JunctionBehavior.SkipCreditsImages, JunctionBehavior.InstantManualSkip] };
        ModPackage Package(ModEffectiveStatus status, ModFormat format = ModFormat.Junction) => new("policy", "Policy", format, true, true, status, "package") { JunctionMetadata = metadata };
        foreach (ModEffectiveStatus status in Enum.GetValues<ModEffectiveStatus>())
        {
            StartupPolicy policy = StartupPolicy.FromCatalog(new(1, [Package(status)], []));
            Assert.Equal(status == ModEffectiveStatus.Active ? new(true, true, true, true) : StartupPolicy.Default, policy);
        }
        Assert.Equal(StartupPolicy.Default, StartupPolicy.FromCatalog(new(2, [Package(ModEffectiveStatus.Active, ModFormat.Memoria)], [])));
        ModPackage intrinsic = Package(ModEffectiveStatus.Active) with { JunctionMetadata = new() { Behaviors = [JunctionBehavior.InstantManualSkip] } };
        Assert.Equal(StartupPolicy.Default, StartupPolicy.FromCatalog(new(3, [intrinsic], [])));
    }

    [Fact]
    public void Held_confirmation_skips_one_stage_and_release_allows_the_next_skip()
    {
        KeyboardInput keyboard = new() { Profile = ControlsProfile.CreateDefault().WithBinding(ControlAction.Confirm, ControlDevice.Keyboard, new InputChord([Keys.K], [])) };
        StartupSequence sequence = new(true);
        InputFrame first = keyboard.Read(new KeyboardState(Keys.K), true);
        Assert.True(sequence.HandleInput(first.Confirm, first.Cancel));
        InputFrame held = keyboard.Read(new KeyboardState(Keys.K), true);
        Assert.False(sequence.HandleInput(held.Confirm, held.Cancel));
        Assert.Equal(StartupStage.PublisherMovie, sequence.Stage);
        keyboard.Read(default, true);
        InputFrame next = keyboard.Read(new KeyboardState(Keys.K), true);
        Assert.True(sequence.HandleInput(next.Confirm, next.Cancel));
        Assert.Equal(StartupStage.SquareLogo, sequence.Stage);
    }

    [Fact]
    public void Native_repeat_returns_through_square_without_replaying_publisher_logos()
    {
        StartupSequence sequence = new(true);
        sequence.RepeatCredits();
        Assert.Equal(StartupStage.SquareLogo, sequence.Stage);
        sequence.Next();
        Assert.Equal(StartupStage.Credits, sequence.Stage);
        sequence.HandleInput(true, false);
        Assert.False(sequence.Active);
    }

    [Fact]
    public void Confirm_skips_each_unpresented_stage_without_starting_a_new_game()
    {
        StartupSequence sequence = new(true);
        Assert.Equal(StartupStage.SquareEnixMovie, sequence.Stage);
        foreach (StartupStage expected in new[] { StartupStage.PublisherMovie, StartupStage.SquareLogo, StartupStage.Credits, StartupStage.Complete })
        {
            Assert.True(sequence.HandleInput(true, false));
            Assert.Equal(expected, sequence.Stage);
            Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
        }
        Assert.False(sequence.HandleInput(false, false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cancel_goes_directly_to_title_before_or_after_presentation(int stage)
    {
        foreach (int elapsed in new[] { 0, 2 })
        {
            StartupSequence sequence = new(true);
            for (int index = 0; index < stage; index++)
            {
                sequence.Next();
            }
            sequence.Advance(TimeSpan.FromSeconds(elapsed));
            Assert.True(sequence.HandleInput(true, true));
            Assert.Equal(StartupStage.Complete, sequence.Stage);
        }
    }

    [Fact]
    public void Confirm_after_visible_content_resets_the_next_stage_clock()
    {
        StartupSequence sequence = new(true);
        sequence.Advance(TimeSpan.FromSeconds(3));
        sequence.HandleInput(true, false);
        Assert.Equal(StartupStage.PublisherMovie, sequence.Stage);
        Assert.Equal(TimeSpan.Zero, sequence.Elapsed);
        sequence.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), sequence.Elapsed);
    }

    [Fact]
    public void Startup_only_runs_on_a_fresh_title_launch()
    {
        LaunchOptions options = new();
        Assert.True(StartupSequence.ShouldPlay(options));
        Assert.False(StartupSequence.ShouldPlay(options with { StatePath = "standalone.json" }));
        Assert.False(StartupSequence.ShouldPlay(options with { InitialState = new GameState { Scene = SceneKind.Field } }));
        Assert.False(StartupSequence.ShouldPlay(options with { DebugUiScreen = "debug-room" }));
        StartupSequence disabled = new(false);
        Assert.False(disabled.HandleInput(true, true));
        disabled.Next();
        Assert.False(disabled.Active);
    }
}
