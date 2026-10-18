using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.GameUi;

internal interface INativeScanTextCanvas
{
    float ScanTextAdvance(string value, float size);
    void ScanTextRun(string value, float size, float x, float y, UiColor color);
}

/// <summary>Draws current Scan window semantics using the product's system fonts.</summary>
internal static class NativeScanWindowDrawing
{
    public static void Draw(IUiCanvas canvas, UiTheme theme, IReadOnlyList<NativeScanWindowText> windows, UiRect viewport, double seconds, float textScale)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(windows);
        if (!float.IsFinite(textScale) || textScale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(textScale));
        }
        if (viewport.IsEmpty)
        {
            return;
        }
        float scale = Math.Min(viewport.Width / 320, viewport.Height / 224);
        float left = viewport.X + (viewport.Width - 320 * scale) / 2;
        float top = viewport.Y + (viewport.Height - 224 * scale) / 2;
        float size = theme.FontSize * scale * textScale;
        UiRect Map(NativeBattleScanRect rectangle) => new(left + rectangle.X * scale, top + rectangle.Y * scale, rectangle.Width * scale, rectangle.Height * scale);
        canvas.PushClip(viewport);
        try
        {
            foreach (NativeScanWindowText window in windows.OrderBy(window => window.Priority).ThenBy(window => window.Index))
            {
                if ((window.BackendFlags38 & 2) != 0 && window.Outer.Width > 0 && window.Outer.Height > 0)
                {
                    theme.Panel(canvas, Map(window.Outer));
                }
                UiRect clip = Map(window.Inner).Intersect(viewport);
                if (clip.IsEmpty)
                {
                    continue;
                }
                canvas.PushClip(clip);
                try
                {
                    byte intensity = unchecked((byte)(unchecked((short)window.Intensity) / 32));
                    foreach (NativeScanTextLine line in window.Lines)
                    {
                        float x = left + (window.OriginalRectangle.X + 6 + line.X) * scale;
                        float y = top + (window.OriginalRectangle.Y + 6 + line.Y) * scale;
                        foreach (DialogueRun run in line.Line.Runs)
                        {
                            Color source = run.Color;
                            float opacity = run.Blink ? DialogueBlink.Opacity(seconds) : 1;
                            UiColor color = new(Channel(source.R, intensity), Channel(source.G, intensity), Channel(source.B, intensity), (byte)Math.Clamp((int)(source.A * opacity), 0, 255));
                            if (canvas is INativeScanTextCanvas native)
                            {
                                native.ScanTextRun(run.Text, size, x, y, color);
                                x += native.ScanTextAdvance(run.Text, size);
                            }
                            else
                            {
                                UiSize measured = canvas.MeasureText(run.Text, size, float.PositiveInfinity);
                                canvas.Text(run.Text, size, new UiRect(x, y, Math.Max(1, measured.Width + size), Math.Max(16 * scale, measured.Height)), color, UiAlignment.Start);
                                x += measured.Width;
                            }
                        }
                    }
                }
                finally
                {
                    canvas.PopClip();
                }
            }
        }
        finally
        {
            canvas.PopClip();
        }
    }

    private static byte Channel(byte value, byte intensity)
    {
        return (byte)Math.Min(255, value * intensity / 128);
    }
}
