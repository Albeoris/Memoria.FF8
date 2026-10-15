using System.Drawing;
using System.Drawing.Text;

namespace Memoria.FF8;

internal static class SystemFontCatalog
{
    internal const string DefaultFamily = "Arial";
    internal const string UnicodeSample = "É e\u0301 · Я Ω · 日本語 · عربي · 👩🏽‍🚀";

    public static IReadOnlyList<string> Families { get; } = ReadFamilies();

    private static string[] ReadFamilies()
    {
        using var installed = new InstalledFontCollection();
        FontFamily[] families = installed.Families;
        try
        {
            return families.Where(family => family.IsStyleAvailable(FontStyle.Regular)).Select(family => family.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        finally
        {
            foreach (FontFamily family in families)
            {
                family.Dispose();
            }
        }
    }
}
