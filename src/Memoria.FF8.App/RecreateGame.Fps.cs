using System.Diagnostics;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private VisualFrameRate? visualFrameRate;
    private readonly long presentationClockStarted = Stopwatch.GetTimestamp();
    private TimeSpan nextFpsLabelUpdate;
    private string fpsLabel = "FPS: --";

    private void BeginMeasuredFrame()
    {
        if (!uiPreferences.ShowFps)
        {
            DisposeFrameRate();
            return;
        }
        visualFrameRate ??= new VisualFrameRate(GraphicsDevice);
        visualFrameRate.BeginFrame();
    }

    protected override void EndDraw()
    {
        base.EndDraw();
        TimeSpan now = Stopwatch.GetElapsedTime(presentationClockStarted);
        visualFrameRate?.Presented(now);
        if (now >= nextFpsLabelUpdate)
        {
            double? fps = visualFrameRate?.PresentationsPerSecond;
            double? changes = visualFrameRate?.FramesPerSecond;
            string changesLabel = changes is double visual ? FormattableString.Invariant($"Changes/s: {visual:F1}") : "Changes/s: --";
            fpsLabel = fps is double value ? FormattableString.Invariant($"FPS: {value:F1} | {changesLabel}") : $"FPS: -- | {changesLabel}";
            nextFpsLabelUpdate = now + TimeSpan.FromSeconds(0.25);
        }
    }

    private void DrawFps(Rectangle bounds)
    {
        if (visualFrameRate is not null)
        {
            batch.End();
            visualFrameRate.CompleteFrame();
            batch.Begin(samplerState: SamplerState.LinearClamp);
        }
        if (!uiPreferences.ShowFps)
        {
            return;
        }
        float scale = ViewportLayout.UiScale(bounds.Width, bounds.Height);
        float margin = 8 * scale;
        float fontSize = 16 * scale;
        Vector2 size = text.MeasureWrapped(fpsLabel, fontSize, float.PositiveInfinity);
        Vector2 position = new(bounds.Right - size.X - margin * 2, bounds.Top + margin);
        Rectangle background = new((int)(position.X - margin), (int)position.Y, (int)MathF.Ceiling(size.X + margin * 2), (int)MathF.Ceiling(size.Y));
        batch.Draw(pixel, background, Color.Black * 0.75f);
        text.Draw(batch, fpsLabel, fontSize, position, Color.White);
    }

    private void DisposeFrameRate()
    {
        visualFrameRate?.Dispose();
        visualFrameRate = null;
        fpsLabel = "FPS: --";
        nextFpsLabelUpdate = TimeSpan.Zero;
    }
}
