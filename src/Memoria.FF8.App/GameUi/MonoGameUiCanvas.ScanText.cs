using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Resources.Text;

namespace Memoria.FF8.GameUi;

internal sealed partial class MonoGameUiCanvas : INativeScanTextCanvas
{
    public bool ScanOriginalLayout => UseNativeLayout;
    private NativeTextContext NativeBattleTextContext => new(resources?.Source.Edition.ToString() ?? "Classic", "Battle", DictionaryLanguage: resources?.ResourceLanguage ?? "en");

    public float ScanTextAdvance(string value, float size)
    {
        if (UseNativeFonts)
        {
            return NativeTextAdvance(value, size, NativeBattleTextContext);
        }
        using SystemTextMeasurer measure = new(size, text.FontFamily);
        return measure.Measure(value);
    }

    public void ScanTextRun(string value, float size, float x, float y, UiColor color) => ScanTextRun(value, size, x, y, color, null);

    public void ScanTextRun(string value, float size, float x, float y, UiColor color, LocalizationTextBinding? binding)
    {
        UiSize measured;
        if (UseNativeFonts)
        {
            measured = MeasureNativeText(value, size, float.PositiveInfinity, NativeBattleTextContext);
        }
        else
        {
            using SystemTextMeasurer measure = new(size, text.FontFamily);
            measured = new(measure.Measure(value), measure.LineHeight);
        }
        if (binding is not null && InspectText is not null)
        {
            Rectangle visible = Rectangle.Intersect(RectangleFor(new UiRect(x, y, measured.Width, measured.Height)), CurrentClip);
            if (visible.Width > 0 && visible.Height > 0)
            {
                InspectText(binding, new UiRect(visible.X, visible.Y, visible.Width, visible.Height));
            }
        }
        if (DrawNativeText(value, size, new UiRect(x, y, Math.Max(1, measured.Width), Math.Max(1, measured.Height)), color, UiAlignment.Start, NativeBattleTextContext))
        {
            return;
        }
        text.DrawWrappedClipped(batch, value, size, new Vector2(x, y), float.PositiveInfinity, ColorFor(color), CurrentClip);
    }
}
