using Memoria.FF7.Runtime;

namespace Memoria.FF8.Mods;

public static class Ff7EnemyExtensionMods
{
    public const string PayloadDirectory = "ff7/gameplay/enemy-extensions/";

    public static bool TryPrepare(ModCatalogSnapshot catalog, out Ff7EnemyExtensionCatalog result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<Ff7EnemyExtensionDocument> documents = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile file in catalog.Files.Where(f => f.Format == ModFormat.Memoria && f.RelativePath.StartsWith(PayloadDirectory, StringComparison.OrdinalIgnoreCase) && f.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                documents.Add(Ff7EnemyExtensionDocument.Parse(new System.Text.UTF8Encoding(false, true).GetString(file.ReadBytes()).TrimStart('\uFEFF')));
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or IOException or ArgumentException)
            {
                errors.Add(new("Memoria.InvalidFf7EnemyExtension", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        try
        {
            result = errors.Count == 0 ? new(documents) : Ff7EnemyExtensionCatalog.Empty;
        }
        catch (InvalidDataException exception)
        {
            errors.Add(new("Memoria.ConflictingFf7EnemyExtension", exception.Message));
            result = Ff7EnemyExtensionCatalog.Empty;
        }
        diagnostics = errors.AsReadOnly();
        return errors.Count == 0;
    }
}
