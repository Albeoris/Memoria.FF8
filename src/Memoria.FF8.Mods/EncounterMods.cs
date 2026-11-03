using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

/// <summary>All enabled semantic encounter documents in committed catalog order.</summary>
public sealed class EncounterMods
{
    public const string PayloadDirectory = "gameplay/encounters/";
    public static EncounterMods Empty { get; } = new([]);

    public EncounterMods(ImmutableArray<EncounterPatchLayer> layers)
    {
        Layers = layers;
    }

    public ImmutableArray<EncounterPatchLayer> Layers { get; }

    public static bool IsPayloadPath(string relativePath)
    {
        return relativePath.StartsWith(PayloadDirectory, StringComparison.OrdinalIgnoreCase) && relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out EncounterMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<EncounterPatchLayer> layers = [];
        List<ModDiagnostic> errors = [];
        Dictionary<string, ModFile> identities = new(StringComparer.Ordinal);
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && IsPayloadPath(file.RelativePath)))
        {
            try
            {
                EncounterPatchDocument document = EncounterPatchDocument.Parse(new UTF8Encoding(false, true).GetString(file.ReadBytes()).TrimStart('\uFEFF'));
                if (!identities.TryAdd(document.DocumentId, file))
                {
                    ModFile owner = identities[document.DocumentId];
                    throw new InvalidDataException($"Duplicate encounter document identity {document.DocumentId}; already declared by {owner.PackageId}:{owner.RelativePath}.");
                }
                layers.Add(new(file.PackageId, file.RelativePath, document));
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                errors.Add(new("Memoria.InvalidEncounterPatch", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(layers.ToImmutableArray()) : Empty;
        diagnostics = errors.AsReadOnly();
        return errors.Count == 0;
    }
}
