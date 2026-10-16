using System.Reflection;
using System.Text.Json;
using Albeoris.Games.Core.NsResources;

namespace Albeoris.Games.FF8.TextEncoding.Internal;

/// <summary>
/// Loads the raw per-byte character tables embedded as JSON resources.
/// Each table entry is either a single-character string, or an empty string for an unused byte.
/// </summary>
internal static class CodepageResources
{
    private static readonly Lazy<String[]> EuropeanTable = new(LoadEuropean);
    private static readonly Lazy<String[]> JapaneseTable = new(LoadJapanese);
    private static readonly Lazy<String[]> RussianTable = new(LoadRussian);

    public static String[] European => EuropeanTable.Value;
    public static String[] Japanese => JapaneseTable.Value;
    public static String[] Russian => RussianTable.Value;

    private static String[] LoadEuropean() => Load("european.codepage.json");
    private static String[] LoadJapanese() => Load("japanese.codepage.json");
    private static String[] LoadRussian() => Load("russian.codepage.json");

    private static String[] Load(String fileName)
    {
        Assembly assembly = typeof(CodepageResources).Assembly;
        using (Stream stream = EmbeddedResources.Open(assembly, "/Resources/" + fileName))
        {
            String[]? result = JsonSerializer.Deserialize<String[]>(stream);
            return result ?? throw new InvalidOperationException($"Embedded resource [{fileName}] could not be parsed.");
        }
    }
}
