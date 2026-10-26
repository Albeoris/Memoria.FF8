using System.Collections.Frozen;
using SkiaSharp;

namespace Memoria.FF8;

internal static class SystemFontCatalog
{
    private static readonly object PreparationLock = new();
    private static Task? preparation;
    private static Inventory? inventory;
    internal const string UnicodeSample = "É e\u0301 · Я Ω · 日本語 · عربي · 👩🏽‍🚀";

    internal static string DefaultFamily => Prepared.DefaultFamily;
    public static IReadOnlyList<string> Families => Prepared.Families;
    private static Inventory Prepared => Volatile.Read(ref inventory) ?? throw new InvalidOperationException("The system font inventory must be prepared before presentation starts.");

    internal static string ResolveFamily(string? requestedFamily)
    {
        Inventory current = Prepared;
        return requestedFamily is not null && current.FamiliesByName.TryGetValue(requestedFamily, out string? actual) ? actual : current.DefaultFamily;
    }

    internal static Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        Task pending;
        lock (PreparationLock)
        {
            pending = preparation ??= Task.Run(Prepare);
        }
        return pending.WaitAsync(cancellationToken);
    }

    private static void Prepare()
    {
        string[] families = SKFontManager.Default.FontFamilies.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (families.Length == 0)
        {
            throw new InvalidOperationException("No installed system fonts were found. Install a font family before starting Memoria.");
        }
        string selected;
        if (families.Contains("Arial", StringComparer.OrdinalIgnoreCase))
        {
            selected = "Arial";
        }
        else
        {
            using SKTypeface fallback = SKTypeface.FromFamilyName("sans-serif");
            selected = families.FirstOrDefault(family => string.Equals(family, fallback.FamilyName, StringComparison.OrdinalIgnoreCase)) ?? families[0];
        }
        Volatile.Write(ref inventory, new Inventory(selected, Array.AsReadOnly(families), families.ToFrozenDictionary(family => family, family => family, StringComparer.OrdinalIgnoreCase)));
    }

    private sealed record Inventory(string DefaultFamily, IReadOnlyList<string> Families, IReadOnlyDictionary<string, string> FamiliesByName);
}
