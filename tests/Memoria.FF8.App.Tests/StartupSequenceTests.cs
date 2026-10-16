using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.Tests;

public sealed class StartupSequenceTests
{
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
