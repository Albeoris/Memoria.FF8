using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class ConfirmationOverlayTests
{
    [Fact]
    public async Task Retained_confirmation_draws_while_waiting_and_requires_a_new_mouse_press()
    {
        using GameConfirmationOverlay overlay = new();
        string review = string.Join("\n", Enumerable.Range(1, 35).Select(index => $"Change {index}: review preserved model data and progression."));
        Task<bool> decision = overlay.ShowAsync(review, "Review import changes", TestContext.Current.CancellationToken);
        using SKBitmap image = new(960, 672, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using BattlePreviewCanvas canvas = new(image, "");
        overlay.Draw(canvas, new VectorUiTheme(), new UiRect(0, 0, 960, 672), SystemFontCatalog.DefaultFamily);
        Assert.False(decision.IsCompleted);
        Assert.Contains(image.Pixels, color => color.Red > 160);
        overlay.Pointer(720, 578, true);
        Assert.False(decision.IsCompleted);
        overlay.Handle(UiKey.PageDown);
        overlay.Draw(canvas, new VectorUiTheme(), new UiRect(0, 0, 960, 672), SystemFontCatalog.DefaultFamily);
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_PRESENTATION_OUTPUT") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            image.SavePng(Path.Combine(directory, "in-game-confirmation.png"));
        }
        overlay.Pointer(720, 578, false);
        overlay.Pointer(720, 578, true);
        Assert.True(await decision);
    }
}
