namespace Memoria.FF8.Mods;

public sealed class FieldScene3DMods
{
    private readonly IReadOnlyDictionary<string, List<PreparedFieldScene>> fields;
    public IReadOnlyList<string> PackageIds { get; }
    public static FieldScene3DMods Empty { get; } = new(new Dictionary<string, List<PreparedFieldScene>>(), []);

    private FieldScene3DMods(IReadOnlyDictionary<string, List<PreparedFieldScene>> fields, IReadOnlyList<string> packageIds)
    {
        this.fields = fields;
        PackageIds = packageIds;
    }

    public IReadOnlyList<PreparedFieldSceneView> Resolve(string field, int slot, IReadOnlyDictionary<int, int> states)
    {
        if (!fields.TryGetValue(field, out List<PreparedFieldScene>? scenes))
        {
            return [];
        }
        List<PreparedFieldSceneView> views = [];
        bool replacementSelected = false;
        foreach (PreparedFieldScene scene in scenes)
        {
            foreach (FieldSceneView view in scene.Document.Views.Where(view => view.Matches(slot, states)))
            {
                if (view.Background != FieldSceneBackgroundMode.Overlay)
                {
                    if (replacementSelected)
                    {
                        continue;
                    }
                    replacementSelected = true;
                }
                views.Add(new(scene, view));
            }
        }
        return views;
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out FieldScene3DMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<ModDiagnostic> messages = [];
        Dictionary<string, List<PreparedFieldScene>> fields = new(StringComparer.OrdinalIgnoreCase);
        List<string> packages = [];
        bool failed = false;
        foreach (IGrouping<string, ModFile> package in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("scene3d/", StringComparison.OrdinalIgnoreCase)).GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase))
        {
            string path = FieldSceneDocuments.ComponentPath;
            try
            {
                Dictionary<string, ModFile> files = package.ToDictionary(file => file.RelativePath, StringComparer.OrdinalIgnoreCase);
                if (!files.TryGetValue(path, out ModFile? marker))
                {
                    throw new InvalidDataException("A 3D scene payload requires scene3d/component.json.");
                }
                FieldSceneDocuments.ValidateComponent(ReadBounded(marker, FieldSceneDocuments.MaximumDocumentBytes));
                packages.Add(package.Key);
                Dictionary<string, byte[]> imported = new(StringComparer.OrdinalIgnoreCase);
                foreach (ModFile file in package.Where(file => file.RelativePath.StartsWith("scene3d/fields/", StringComparison.OrdinalIgnoreCase)))
                {
                    path = file.RelativePath;
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (FieldSceneDocuments.ScenePath(name) != path)
                    {
                        throw new InvalidDataException("Field documents use scene3d/fields/<field>.json.");
                    }
                    FieldSceneDocument document = FieldSceneDocuments.Read(ReadBounded(file, FieldSceneDocuments.MaximumDocumentBytes), name);
                    if (!fields.TryGetValue(name, out List<PreparedFieldScene>? higher))
                    {
                        fields.Add(name, higher = []);
                    }
                    List<FieldSceneView> accepted = [];
                    foreach (FieldSceneView view in document.Views)
                    {
                        PreparedFieldScene? conflict = view.Background == FieldSceneBackgroundMode.Overlay ? null : higher.FirstOrDefault(scene => scene.Document.Views.Any(candidate => candidate.Background != FieldSceneBackgroundMode.Overlay && FieldSceneDocuments.Overlap(candidate, view)));
                        if (conflict is not null)
                        {
                            messages.Add(new("Memoria.FieldSceneViewConflict", $"View '{view.Id}' overlaps '{conflict.PackageId}' in camera slot {view.CameraSlot}. The higher priority view owns the overlap.", package.Key, path));
                            if (conflict.Document.Views.Any(candidate => Covers(candidate, view)))
                            {
                                continue;
                            }
                        }
                        accepted.Add(view);
                    }
                    if (accepted.Count == 0)
                    {
                        continue;
                    }
                    HashSet<string> used = accepted.SelectMany(view => view.Instances).Select(instance => instance.AssetId).ToHashSet(StringComparer.Ordinal);
                    PreparedFieldScene prepared = FieldSceneDocuments.Prepare(package.Key, document with { Views = [.. accepted], Assets = document.Assets.Where(asset => used.Contains(asset.Id)).ToArray() }, assetPath =>
                    {
                        if (!imported.TryGetValue(assetPath, out byte[]? content))
                        {
                            if (!files.TryGetValue(assetPath, out ModFile? assetFile))
                            {
                                throw new InvalidDataException($"The package is missing '{assetPath}'.");
                            }
                            imported.Add(assetPath, content = ReadBounded(assetFile, FieldSceneDocuments.MaximumGlbBytes));
                        }
                        return content;
                    });
                    higher.Add(prepared);
                }
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                failed = true;
                messages.Add(new("Memoria.InvalidFieldScene3D", exception.Message, package.Key, path));
            }
        }
        result = failed ? Empty : new(fields, packages.AsReadOnly());
        diagnostics = messages.AsReadOnly();
        return !failed;
    }

    private static bool Covers(FieldSceneView higher, FieldSceneView lower) => higher.Background != FieldSceneBackgroundMode.Overlay &&
        higher.CameraSlot == lower.CameraSlot && higher.SelectedModules.All(module => lower.AllowedStates(module) is { } states && states.All(higher.AllowedStates(module)!.Contains));

    private static byte[] ReadBounded(ModFile file, int limit)
    {
        long? length = file.ContentStamp?.Length ?? file.ResolveFileSegment()?.Length;
        if (length > limit)
        {
            throw new InvalidDataException($"'{file.RelativePath}' exceeds its size limit.");
        }
        byte[] bytes = file.ReadBytes();
        if (bytes.Length > limit)
        {
            throw new InvalidDataException($"'{file.RelativePath}' exceeds its size limit.");
        }
        return bytes;
    }
}
