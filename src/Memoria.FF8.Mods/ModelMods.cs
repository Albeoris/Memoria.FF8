using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public sealed record ModelModVariant(string ModelId, string PackageId, string RelativePath);

/// <summary>Immutable model replacements in package precedence order.</summary>
public sealed class ModelMods : IModelOverrideSource
{
    private readonly IReadOnlyList<(ModelModVariant Variant, ModFile File)> files;
    private readonly Dictionary<(ModFile File, object Original), object> replacements = [];

    private ModelMods(IReadOnlyList<(ModelModVariant Variant, ModFile File)> files)
    {
        this.files = files;
        Variants = Array.AsReadOnly(files.Select(item => item.Variant).ToArray());
    }

    public static ModelMods Empty { get; } = new([]);
    public IReadOnlyList<ModelModVariant> Variants { get; }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out ModelMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        List<(ModelModVariant Variant, ModFile File)> files = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("models/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                string id = file.RelativePath[7..^4].ToLowerInvariant();
                if (!IsModelId(id))
                {
                    throw new InvalidDataException("Model replacements require models/<field|battle|world>/<model address>.glb.");
                }
                _ = ModelExchange.ImportGlb(file.ReadBytes(), allowBoneCountChange: true);
                files.Add((new(id, file.PackageId, file.RelativePath), file));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException or System.Text.Json.JsonException)
            {
                errors.Add(new("Memoria.InvalidModel", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(Array.AsReadOnly(files.ToArray())) : Empty;
        diagnostics = Array.AsReadOnly(errors.ToArray());
        return errors.Count == 0;
    }

    public EditableModel Load(ModelModVariant variant, EditableModel original)
    {
        ModFile file = files.FirstOrDefault(item => item.Variant == variant).File ?? throw new FileNotFoundException("The model variant is no longer present in the active catalog.");
        return ModelExchange.ImportGlb(file.ReadBytes(), original, allowBoneCountChange: true).Model;
    }

    public FieldModel Apply(string modelId, FieldModel original) => Apply(modelId, original, ModelExchange.FromField, ModelExchange.ToField);
    public BattleModel Apply(string modelId, BattleModel original) => Apply(modelId, original, ModelExchange.FromBattle, ModelExchange.ToBattle);
    public BattleStage Apply(string modelId, BattleStage original) => Apply(modelId, original, ModelExchange.FromStage, ModelExchange.ToStage);
    public WorldStaticModel Apply(string modelId, WorldStaticModel original) => Apply(modelId, original, ModelExchange.FromWorld, ModelExchange.ToWorld);

    private T Apply<T>(string modelId, T original, Func<T, EditableModel> from, Func<EditableModel, T, T> to) where T : class
    {
        var match = files.FirstOrDefault(item => item.Variant.ModelId.Equals(modelId, StringComparison.OrdinalIgnoreCase));
        if (match.File is null)
        {
            return original;
        }
        var key = (match.File, (object)original);
        if (replacements.TryGetValue(key, out object? cached))
        {
            return (T)cached;
        }
        EditableModel model = ModelExchange.ImportGlb(match.File.ReadBytes(), from(original), allowBoneCountChange: true).Model;
        T replacement = to(model, original);
        replacements.Add(key, replacement);
        return replacement;
    }

    public static bool IsModelId(string id)
    {
        string[] parts = id.Split('/');
        if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '_' and not '-' and not '.')))
        {
            return false;
        }
        return parts.Length == 3 && parts[0] == "field" && int.TryParse(parts[2], out int fieldIndex) && fieldIndex >= 0
            || parts.Length == 2 && parts[0] == "battle" && (parts[1].EndsWith(".dat", StringComparison.OrdinalIgnoreCase) || parts[1].EndsWith(".x", StringComparison.OrdinalIgnoreCase))
            || parts.Length == 2 && parts[0] == "world" && int.TryParse(parts[1], out int shape) && shape is >= 0 and < 64
            || parts.Length == 3 && parts[0] == "world" && parts[1] == "static" && int.TryParse(parts[2], out int staticShape) && staticShape is >= 64 and <= 255;
    }
}
