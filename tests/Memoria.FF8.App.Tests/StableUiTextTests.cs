using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class StableUiTextTests
{
    [Fact]
    public void Installed_timer_console_starts_ten_minutes_from_an_external_standalone_save()
    {
        string source = TestResources.FixturePath("App.Tests.StableUiTextTests.1");
        GameSession session = new(PlayerStateLoader.Load(source, TestResources.GameDirectory));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        Assert.Equal(770, world.Events.FieldId);
        Assert.Equal(121, world.Events.Frame);
        for (int index = 0; index < 40; index++)
        {
            Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        }
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        for (int index = 0; index < 600; index++)
        {
            PlayerAction action = world.Events.CurrentDialogue is { CanConfirm: true } dialog ? new(dialog.FirstChoice is null ? PlayerActionKind.Confirm : PlayerActionKind.Choose, Value: dialog.FirstChoice ?? 0) : new(PlayerActionKind.Wait);
            Assert.True(world.Apply(action).Accepted);
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(770, world.Events.FieldId);
        Assert.Null(world.Events.CurrentDialogue);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.True(session.ExamTimer!.DisplayEnabled);
        Assert.InRange(session.ExamTimer.RequireRemaining(), 580, 600);
        session.SetEventState(world.Events.Capture());
        string saved = TestResources.ArtifactFile("timer.compact.json");
        session.Save(saved, compact: true);
        using GameWorld restored = new(new GameSession(GameSession.Load(saved)), TestResources.GameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TIMER_LAYOUT_SAVE_OUTPUT") is { Length: > 0 } output)
        {
            File.Copy(saved, output, overwrite: false);
        }
    }

    [Theory]
    [InlineData(24)]
    [InlineData(48)]
    public void Changing_keycap_digits_keeps_the_common_prefix_stationary(float size)
    {
        float height = SystemTextLayout.LineHeight(size);
        using SKBitmap bitmap = new(320, 160);
        using BattlePreviewCanvas canvas = new(bitmap, "");
        int[]? reference = null;
        for (int digit = 0; digit <= 9; digit++)
        {
            canvas.Fill(new(0, 0, bitmap.Width, bitmap.Height), new(0, 0, 0));
            ButtonPrompt.DrawCanvas(canvas, "N" + digit, size, new(10, 10, 300, height), 1, null);
            int prefixRight = (int)(10 + size * 0.5f);
            int[] pixels = Enumerable.Range(0, bitmap.Height).SelectMany(y => Enumerable.Range(0, prefixRight).Select(x => bitmap.GetPixel(x, y).ToArgb())).ToArray();
            Assert.Contains(pixels, pixel => pixel == SKColors.White.ToArgb());
            if (reference is not null)
            {
                Assert.Equal(reference, pixels);
            }
            reference = pixels;
        }
    }

    [Theory]
    [InlineData(10, UiAlignment.Start, false)]
    [InlineData(20, UiAlignment.Center, false)]
    [InlineData(40, UiAlignment.End, false)]
    [InlineData(20, UiAlignment.Center, true)]
    public void Changing_timer_digits_keeps_the_unchanged_prefix_at_the_same_position(float size, UiAlignment alignment, bool outlined)
    {
        const string prefix = "11:1";
        UiRect bounds = new(20, 20, 260, 100);
        string family = SystemFontCatalog.DefaultFamily;
        int prefixRight = (int)(bounds.X + SystemTextLayout.MeasureAdvance(prefix, size, family) - size / 4);
        int[]? reference = null;
        using SKBitmap bitmap = new(320, 160);
        using BattlePreviewCanvas canvas = new(bitmap, "");
        for (int digit = 0; digit <= 9; digit++)
        {
            canvas.BeginFrame(TimeSpan.Zero);
            canvas.Fill(new(0, 0, bitmap.Width, bitmap.Height), new(0, 0, 0));
            if (outlined)
            {
                canvas.OutlinedText(prefix + digit, size, bounds, new(255, 255, 255), new(30, 30, 30), 1);
            }
            else
            {
                canvas.Text(prefix + digit, size, bounds, new(255, 255, 255), family, alignment);
            }
            int[] pixels = Enumerable.Range(0, bitmap.Height).SelectMany(y => Enumerable.Range(0, prefixRight).Select(x => bitmap.GetPixel(x, y).ToArgb())).ToArray();
            Assert.Contains(pixels, pixel => pixel != SKColors.Black.ToArgb());
            if (reference is not null)
            {
                Assert.Equal(reference, pixels);
            }
            reference = pixels;
        }
    }
}
