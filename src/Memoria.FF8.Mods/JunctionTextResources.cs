using System.Collections.Immutable;
using Albeoris.Games.FF8.Msd;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Prepares selected executable text tables in effective package order.</summary>
public static class JunctionTextResources
{
    public static bool TryPrepare(ModCatalogSnapshot catalog, out IReadOnlyList<StaticTextResource> resources, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<ResourceKey, StaticTextResource> selected = [];
        List<ModDiagnostic> failures = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Junction))
        {
            try
            {
                ResourceKey? address = StaticTextResource.Address(file.RelativePath);
                if (address is not ResourceKey key || selected.ContainsKey(key))
                {
                    continue;
                }
                ImmutableArray<string> texts = MsdFile.Read(file.ReadBytes(), FF8Encoding.CreateEuropean()).Texts.Select(WithoutTerminator).ToImmutableArray();
                if (texts.IsEmpty || key == StaticTextResource.CardNames && texts.Length != 110)
                {
                    throw new InvalidDataException("Static text table is empty or differs from its required identity count.");
                }
                selected.Add(key, new StaticTextResource(key, texts, file.PackageId, file.RelativePath));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException or FormatException)
            {
                failures.Add(new("Junction.InvalidTextResource", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        resources = selected.Values.ToArray();
        diagnostics = failures;
        return failures.Count == 0;
    }

    private static string WithoutTerminator(string text)
    {
        int end = text.IndexOf("{End}", StringComparison.Ordinal);
        int speedEnd = text.IndexOf("{Speed 0}", StringComparison.Ordinal);
        if (speedEnd >= 0 && (end < 0 || speedEnd < end))
        {
            end = speedEnd;
        }
        return end < 0 ? text : text[..end];
    }
}
