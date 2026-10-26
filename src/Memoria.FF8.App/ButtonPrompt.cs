using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

/// <summary>Inline input labels keep their own silhouette and spacing next to authored text.</summary>
internal interface IControllerButtonCanvas
{
    bool TryDrawControllerButton(int nativeIconId, ControllerPresentation presentation, UiRect bounds, float opacity);
}

internal static class ButtonPrompt
{
    internal static bool HasVectorArtwork(string label) => ControllerIcon(label) is int icon && ControllerVectorArtwork.Supports(icon, Presentation(label));

    internal static float Measure(string label, Func<string, float> measure, float lineHeight) => HasVectorArtwork(label) ? lineHeight * 1.24f : Advance(measure(Label(label)), lineHeight);

    internal static float Advance(float textWidth, float lineHeight) => Math.Max(lineHeight, textWidth * 0.82f + lineHeight * 0.45f) + lineHeight * 0.24f;

    internal static string VisibleText(DialogueRun run, ref int remaining)
    {
        if (!run.IsButton)
        {
            return DialoguePager.VisiblePrefix(run.Text, ref remaining);
        }
        if (remaining <= 0)
        {
            return "";
        }
        remaining--;
        return run.Text;
    }

    internal static void Draw(SpriteBatch batch, Texture2D pixel, SystemTextRenderer text, string label, float size, Vector2 position, float lineHeight, float opacity, string family, Rectangle clip, IUiCanvas? canvas = null)
    {
        if (label.Length == 0)
        {
            return;
        }
        int? icon = ControllerIcon(label);
        ControllerPresentation presentation = Presentation(label);
        bool vector = HasVectorArtwork(label);
        label = Label(label);
        float textWidth = SystemTextLayout.MeasureAdvance(label, size, family);
        float width = vector ? lineHeight : Advance(textWidth, lineHeight) - lineHeight * 0.24f;
        float inset = Math.Max(1, lineHeight * 0.055f);
        float top = position.Y + lineHeight * 0.04f;
        float height = lineHeight * 0.92f;
        if (icon is int id && canvas is IControllerButtonCanvas controller && controller.TryDrawControllerButton(id, presentation, new UiRect(position.X, top, width, height), opacity))
        {
            return;
        }
        DrawShape(position.X, top, width, height, inset, Fill);
        float labelSize = size * 0.82f;
        Vector2 labelBounds = text.MeasureWrapped(label, labelSize, float.PositiveInfinity, family);
        text.DrawWrappedClipped(batch, label, labelSize, new Vector2(position.X + (width - labelBounds.X) / 2, top + (height - labelBounds.Y) / 2), float.PositiveInfinity, Color.White * opacity, clip, family);

        void Fill(float x, float y, float w, float h, Color color)
        {
            Rectangle bounds = new((int)MathF.Round(x), (int)MathF.Round(y), Math.Max(1, (int)MathF.Round(w)), Math.Max(1, (int)MathF.Round(h)));
            bounds = Rectangle.Intersect(bounds, clip);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                batch.Draw(pixel, bounds, color * opacity);
            }
        }
    }

    internal static void DrawCanvas(IUiCanvas canvas, string label, float size, UiRect bounds, float opacity, string? family)
    {
        float lineHeight = bounds.Height;
        int? icon = ControllerIcon(label);
        ControllerPresentation presentation = Presentation(label);
        bool vector = HasVectorArtwork(label);
        label = Label(label);
        float textWidth = SystemTextLayout.MeasureAdvance(label, size, family);
        float width = vector ? lineHeight : Advance(textWidth, lineHeight) - lineHeight * 0.24f;
        float inset = Math.Max(1, lineHeight * 0.055f);
        float top = bounds.Y + lineHeight * 0.04f;
        float height = lineHeight * 0.92f;
        if (icon is int id && canvas is IControllerButtonCanvas controller && controller.TryDrawControllerButton(id, presentation, new UiRect(bounds.X, top, width, height), opacity))
        {
            return;
        }
        DrawShape(bounds.X, top, width, height, inset, (x, y, w, h, color) => canvas.Fill(new UiRect(x, y, w, h), new UiColor(color.R, color.G, color.B, (byte)(255 * opacity))));
        float labelSize = size * 0.82f;
        using SystemTextMeasurer measurer = SystemTextLayout.CreateMeasurer(labelSize, family);
        using var labelRaster = SystemTextLayout.Rasterize(label, labelSize, float.PositiveInfinity, family);
        float labelHeight = labelRaster.Height / (float)SystemTextLayout.RasterScale;
        canvas.Text(label, labelSize, new UiRect(bounds.X + (width - textWidth * 0.82f) / 2, top + (height - labelHeight) / 2, width, measurer.LineHeight), new UiColor(255, 255, 255, (byte)(255 * opacity)), family);
    }

    internal static int? ControllerIcon(string label) => label.Length > 0 && label[0] is >= '\uF100' and <= '\uF10F' ? 128 + label[0] - '\uF100' : null;

    internal static ControllerPresentation Presentation(string label) => label.Length > 1 && label[1] is >= '\uF180' and <= '\uF184' ? (ControllerPresentation)(label[1] - '\uF180') : ControllerPresentation.Generic;

    internal static string Label(string label) => ControllerIcon(label) is null ? label : label[(label.Length > 1 && label[1] is >= '\uF180' and <= '\uF184' ? 2 : 1)..];

    private static void DrawShape(float x, float top, float width, float height, float inset, Action<float, float, float, float, Color> fill)
    {
        fill(x, top + inset, width, height - inset, new Color(16, 18, 22));
        fill(x + inset, top, width - 2 * inset, height, new Color(16, 18, 22));
        fill(x + inset, top + inset, width - 2 * inset, height - 3 * inset, new Color(104, 110, 120));
        fill(x + 2 * inset, top + 2 * inset, width - 4 * inset, height - 4 * inset, new Color(53, 57, 64));
        fill(x + 2 * inset, top + height * 0.48f, width - 4 * inset, height * 0.42f - 2 * inset, new Color(43, 47, 54));
    }
}
