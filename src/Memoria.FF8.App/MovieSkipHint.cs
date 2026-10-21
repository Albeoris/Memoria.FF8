using Memoria.FF8.Ui;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class MovieSkipHint
{
    public static void Draw(IUiCanvas canvas, UiRect viewport, SceneKind scene, MovieSkipGesture gesture, string binding, string fontFamily)
    {
        if (scene != SceneKind.Movie || !gesture.Visible)
        {
            return;
        }
        float scale = Math.Min(viewport.Width / 960, viewport.Height / 720);
        float fontSize = 16 * scale;
        using var measurer = SystemTextLayout.CreateMeasurer(fontSize, fontFamily);
        binding = ButtonPromptText.PlainText(binding);
        float keyWidth = ButtonPrompt.Advance(measurer.Measure(binding), measurer.LineHeight);
        float labelWidth = measurer.Measure("Hold to skip");
        float radius = Math.Max(22 * scale, keyWidth / 2 + 10 * scale);
        float margin = 24 * scale;
        float centerX = viewport.Right - margin - radius;
        float centerY = viewport.Bottom - margin - radius;
        float labelX = centerX - radius - 16 * scale - labelWidth;
        float lineY = centerY - measurer.LineHeight / 2;
        canvas.Fill(new UiRect(labelX - 12 * scale, centerY - radius - 8 * scale, labelWidth + 2 * radius + 40 * scale, 2 * radius + 16 * scale), new UiColor(0, 0, 0, 150));
        canvas.Text("Hold to skip", fontSize, new UiRect(labelX, lineY, labelWidth + 2 * scale, measurer.LineHeight + 2 * scale), new UiColor(240, 240, 240), fontFamily);
        ButtonPrompt.DrawCanvas(canvas, binding, fontSize, new UiRect(centerX - keyWidth / 2, lineY, keyWidth, measurer.LineHeight), 1, fontFamily);
        DrawArc(canvas, centerX, centerY, radius, 1, new UiColor(150, 150, 150, 180), 2 * scale);
        DrawArc(canvas, centerX, centerY, radius, Math.Clamp(gesture.Progress, 0, 1), new UiColor(255, 255, 255), 3 * scale);
    }

    private static void DrawArc(IUiCanvas canvas, float x, float y, float radius, float progress, UiColor color, float thickness)
    {
        const int segments = 96;
        float end = progress * segments;
        for (int index = 0; index < MathF.Ceiling(end); index++)
        {
            float startAngle = index * MathF.Tau / segments - MathF.PI / 2;
            float endAngle = Math.Min(index + 1, end) * MathF.Tau / segments - MathF.PI / 2;
            canvas.Line(x + MathF.Cos(startAngle) * radius, y + MathF.Sin(startAngle) * radius, x + MathF.Cos(endAngle) * radius, y + MathF.Sin(endAngle) * radius, color, thickness);
        }
    }
}
