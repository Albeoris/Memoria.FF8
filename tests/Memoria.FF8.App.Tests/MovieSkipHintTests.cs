using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

public sealed class MovieSkipHintTests
{
    [Fact]
    public void Single_cancel_press_draws_the_binding_and_hint_until_the_release_timeout()
    {
        KeyboardInput input = new();
        MovieSkipGesture gesture = new();

        Assert.Empty(Draw().Texts);
        input.Read(new KeyboardState(Keys.C), true);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.1), input.CancelHeld));
        RecordingCanvas pressed = Draw();
        Assert.Equal(["Hold to skip", "C"], pressed.Texts);
        Assert.True(pressed.Lines > 96);

        input.Read(default, true);
        Assert.False(gesture.Update(TimeSpan.FromSeconds(2.9), input.CancelHeld));
        RecordingCanvas released = Draw();
        Assert.Equal(["Hold to skip", "C"], released.Texts);
        Assert.Equal(96, released.Lines);

        Assert.False(gesture.Update(TimeSpan.FromSeconds(0.1), input.CancelHeld));
        Assert.Empty(Draw().Texts);

        RecordingCanvas Draw()
        {
            RecordingCanvas canvas = new();
            MovieSkipHint.Draw(canvas, new UiRect(0, 0, 960, 720), SceneKind.Movie, gesture, input.CancelBindingHint, "Arial");
            return canvas;
        }
    }

    [Theory]
    [InlineData(SceneKind.Field)]
    [InlineData(SceneKind.Title)]
    [InlineData(SceneKind.WorldMap)]
    public void A_hint_from_the_previous_movie_does_not_draw_over_other_scenes(SceneKind scene)
    {
        MovieSkipGesture gesture = new();
        gesture.Update(TimeSpan.FromSeconds(0.1), cancelHeld: true);
        RecordingCanvas canvas = new();

        MovieSkipHint.Draw(canvas, new UiRect(0, 0, 960, 720), scene, gesture, "C", "Arial");

        Assert.Empty(canvas.Texts);
        Assert.Equal(0, canvas.Lines);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<string> Texts { get; } = [];
        public int Lines { get; private set; }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            Texts.Add(text);
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Image(UiImageSource source, UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
            Lines++;
        }

        public void PushClip(UiRect bounds)
        {
        }

        public void PopClip()
        {
        }
    }
}
