using System.Windows.Media;

namespace Memoria.FF8;

internal static class SystemFontCatalog
{
    internal const string DefaultFamily = "Arial";
    internal const string UnicodeSample = "É e\u0301 · Я Ω · 日本語 · عربي · 👩🏽‍🚀";

    public static IReadOnlyList<string> Families { get; } = ReadFamilies();

    private static string[] ReadFamilies()
    {
        return Fonts.SystemFontFamilies.Select(family => family.Source).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
