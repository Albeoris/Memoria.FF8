namespace Memoria.FF8.Mods;

/// <summary>One complete lighting component selected in catalog priority order. Scenes never inherit from lower packages.</summary>
public sealed class FieldLightingMods
{
    private readonly IReadOnlyDictionary<string, FieldLightingScene> scenes;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FieldLightingScene> emptyScenes = new(StringComparer.OrdinalIgnoreCase);
    public static FieldLightingMods Empty { get; } = new(null, new Dictionary<string, FieldLightingScene>(), []);

    private FieldLightingMods(string? packageId, IReadOnlyDictionary<string, FieldLightingScene> scenes, IReadOnlyList<string> packageIds)
    {
        PackageId = packageId;
        this.scenes = scenes;
        PackageIds = packageIds;
    }

    public string? PackageId { get; }
    public bool Active => PackageId is not null;
    public IReadOnlyList<string> PackageIds { get; }

    /// <summary>Null explicitly disables lighting. An active component always resolves a scene, including an empty black one.</summary>
    public FieldLightingScene? ResolveScene(string fieldName)
    {
        FieldLightingSceneIO.ValidateFieldName(fieldName);
        return Active ? scenes.GetValueOrDefault(fieldName) ?? emptyScenes.GetOrAdd(fieldName, FieldLightingScene.Empty) : null;
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out FieldLightingMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<ModDiagnostic> errors = [];
        ModFile[] markers = catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.Equals(FieldLightingSceneIO.ComponentPath, StringComparison.OrdinalIgnoreCase)).ToArray();
        HashSet<string> packages = new(markers.Select(marker => marker.PackageId), StringComparer.OrdinalIgnoreCase);
        string? selected = markers.FirstOrDefault()?.PackageId;
        Dictionary<string, FieldLightingScene> selectedScenes = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("lighting/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                if (file.RelativePath.Equals(FieldLightingSceneIO.ComponentPath, StringComparison.OrdinalIgnoreCase))
                {
                    FieldLightingSceneIO.ValidateComponent(file.ReadBytes());
                    continue;
                }
                if (!packages.Contains(file.PackageId))
                {
                    throw new InvalidDataException("Lighting scenes require lighting/component.json in the same package.");
                }
                string fieldName = Path.GetFileNameWithoutExtension(file.RelativePath);
                if (!file.RelativePath.Equals(FieldLightingSceneIO.ScenePath(fieldName), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Lighting scenes belong only in lighting/fields/<fieldName>.json. Global or inherited lighting is unsupported.");
                }
                FieldLightingScene scene = FieldLightingSceneIO.Deserialize(file.ReadBytes(), fieldName);
                if (file.PackageId.Equals(selected, StringComparison.OrdinalIgnoreCase) && !selectedScenes.TryAdd(fieldName, scene))
                {
                    throw new InvalidDataException("Duplicate field lighting selector.");
                }
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                errors.Add(new("Memoria.InvalidFieldLighting", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(selected, selectedScenes, Array.AsReadOnly(markers.Select(marker => marker.PackageId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray())) : Empty;
        diagnostics = Array.AsReadOnly(errors.ToArray());
        return errors.Count == 0;
    }
}
