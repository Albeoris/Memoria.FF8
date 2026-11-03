using Memoria.FF8.Ui;

namespace Memoria.FF8.GameUi;

internal static class LearnedAbilityHighlight
{
    public static UiColor Color(double seconds)
    {
        double phase = seconds * Math.PI;
        return new UiColor((byte)(208 + 47 * Math.Sin(phase)), (byte)(208 + 47 * Math.Sin(phase + 2 * Math.PI / 3)), (byte)(208 + 47 * Math.Sin(phase + 4 * Math.PI / 3)));
    }
}
