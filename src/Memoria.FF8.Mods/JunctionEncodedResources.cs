using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

/// <summary>Freezes supported encoded Junction replacements in effective catalog order.</summary>
public static class JunctionEncodedResources
{
    public const string MainCharacterFormat = "mch";

    public static bool TryPrepare(ModCatalogSnapshot catalog, out IReadOnlyList<EncodedResource> resources, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<ResourceKey, EncodedResource> selected = [];
        HashSet<ResourceKey> encountered = [];
        List<ModDiagnostic> failures = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Junction))
        {
            try
            {
                ResourceKey? address = Address(file.RelativePath);
                if (address is not ResourceKey key || !encountered.Add(key))
                {
                    continue;
                }
                byte[] bytes = file.ReadBytes();
                if (bytes.Length < 16)
                {
                    throw new InvalidDataException("A main-character MCH replacement is empty or truncated.");
                }
                selected.Add(key, new EncodedResource(key, MainCharacterFormat, file.Revision, $"{file.PackageId}:{file.RelativePath}", bytes));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
            {
                failures.Add(new("Junction.InvalidEncodedResource", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        resources = selected.Values.ToArray();
        diagnostics = failures;
        return failures.Count == 0;
    }

    public static ResourceKey? Address(string relativePath)
    {
        string[] parts = relativePath.Replace('\\', '/').ToLowerInvariant().Split('/');
        int start = parts.Length == 5 && parts[0] == "direct" ? 1 : 0;
        if (parts.Length - start != 4 || parts[start] != "field" || parts[start + 1] != "model" || parts[start + 2] != "main_chr")
        {
            return null;
        }
        string name = parts[start + 3];
        if (name.Length != 8 || !char.IsAsciiLetterLower(name[0]) || name[4..] != ".mch" || name.AsSpan(1, 3).ContainsAnyExceptInRange('0', '9'))
        {
            return null;
        }
        return new ResourceKey($"field/main_chr/{name}");
    }
}
