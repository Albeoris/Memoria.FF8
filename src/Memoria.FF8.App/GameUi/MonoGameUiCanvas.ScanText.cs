using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas : INativeScanTextCanvas
{
    public float ScanTextAdvance(string value, float size)
    {
        using SystemTextMeasurer measure = new(size, text.FontFamily);
        return measure.Measure(value);
    }

    public void ScanTextRun(string value, float size, float x, float y, UiColor color)
    {
        text.DrawWrappedClipped(batch, value, size, new Vector2(x, y), float.PositiveInfinity, ColorFor(color), CurrentClip);
    }
}
