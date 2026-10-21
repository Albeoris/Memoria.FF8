using System.Globalization;
using System.Resources;

namespace Memoria.FF8.GameUi;

internal static class MagicUiText
{
    private static readonly ResourceManager Resources = new("Memoria.FF8.GameUi.MagicUiStrings", typeof(MagicUiText).Assembly);

    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Missing magic interface text '{key}'.");
    public static string SettingLabel(string id) => id switch
    {
        "Shared magic stock" => Get("SharedLabel"),
        "Infinite magic" => Get("InfiniteLabel"),
        "Smart Attack" => Get("SmartLabel"),
        _ => id
    };

    public static string Format(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, Get(key), values);
}
