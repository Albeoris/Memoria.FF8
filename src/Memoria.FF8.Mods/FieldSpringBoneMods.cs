using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Mods;

public sealed class FieldSpringBoneMod
{
    private readonly FieldSpringBoneAsset asset;
    private readonly ConditionalWeakTable<FieldModel, FieldModel> replacements = new();

    internal FieldSpringBoneMod(ModFile file, string modelId, string field, int? entry, string? mainCharacter, int packageOrder, FieldSpringBoneAsset asset)
    {
        PackageId = file.PackageId;
        RelativePath = file.RelativePath;
        ModelId = modelId;
        Field = field;
        Entry = entry;
        MainCharacter = mainCharacter;
        PackageOrder = packageOrder;
        this.asset = asset;
    }

    public string PackageId { get; }
    public string RelativePath { get; }
    public string Field { get; }
    public int? Entry { get; }
    public string? MainCharacter { get; }
    public string ModelId { get; }
    internal int PackageOrder { get; }

    public void ValidateRig(FieldModel model)
    {
        _ = Apply(model);
    }

    internal FieldModel Apply(FieldModel model) => replacements.GetValue(model, candidate => candidate with { SpringBones = asset.Bind(candidate) });
}

/// <summary>Resolves standard VRMC_springBone rigs in package precedence order.</summary>
public sealed class FieldSpringBoneMods
{
    private const string Prefix = "models/field/";
    private const string GlbSuffix = ".springs.glb";
    private const string VrmSuffix = ".springs.vrm";
    private const string MainCharacterDirectory = "main_chr";

    private readonly Dictionary<string, FieldSpringBoneMod> exactProfiles;
    private readonly Dictionary<string, FieldSpringBoneMod> sharedProfiles;

    private FieldSpringBoneMods(Dictionary<string, FieldSpringBoneMod> profiles, IReadOnlyList<string> packageIds)
    {
        Definitions = Array.AsReadOnly(profiles.Values.ToArray());
        exactProfiles = Definitions.Where(definition => definition.Entry is not null).ToDictionary(definition => definition.ModelId, StringComparer.OrdinalIgnoreCase);
        sharedProfiles = Definitions.Where(definition => definition.MainCharacter is not null).ToDictionary(definition => definition.MainCharacter!, StringComparer.OrdinalIgnoreCase);
        PackageIds = packageIds;
    }

    public static FieldSpringBoneMods Empty { get; } = new(new(StringComparer.OrdinalIgnoreCase), []);
    public IReadOnlyList<FieldSpringBoneMod> Definitions { get; }
    public IReadOnlyList<string> PackageIds { get; }

    public static bool IsSpringDocumentPath(string relativePath)
    {
        return SpringPathAddress(relativePath) is { } id && TryParseAddress(id, out _, out _, out _);
    }

    internal static bool IsObsoleteSpringDocumentPath(string relativePath)
    {
        const string suffix = ".springs.json";
        return relativePath.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) && relativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            TryParseAddress(relativePath["models/".Length..^suffix.Length].ToLowerInvariant(), out _, out _, out _);
    }

    public static bool TryPrepare(ModCatalogSnapshot catalog, out FieldSpringBoneMods result, out IReadOnlyList<ModDiagnostic> diagnostics, IReadOnlyList<ModelModVariant>? modelVariants = null)
    {
        Dictionary<string, FieldSpringBoneMod> profiles = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> packageOrder = new(StringComparer.OrdinalIgnoreCase);
        HashSet<(int PackageOrder, string ModelId)> packageSelectors = [];
        List<ModDiagnostic> errors = [];
        var geometry = (modelVariants ?? []).Where(variant => variant.ClipSlot is null && variant.ModelId.StartsWith("field/", StringComparison.OrdinalIgnoreCase)).ToLookup(variant => (variant.PackageId, variant.RelativePath));
        foreach (ModFile file in catalog.Files.Where(file => file.Format == ModFormat.Memoria))
        {
            string? address = SpringPathAddress(file.RelativePath);
            ModelModVariant[] variants = geometry[(file.PackageId, file.RelativePath)].ToArray();
            if (address is null && variants.Length == 0)
            {
                continue;
            }
            try
            {
                FieldSpringBoneAsset? asset = ModelExchange.ReadFieldSpringBoneAsset(file.ReadBytes());
                if (address is null && asset is null)
                {
                    continue;
                }
                if (asset is null)
                {
                    throw new InvalidDataException("Expected a VRMC_springBone 1.0 extension in the spring GLB.");
                }
                if (address is null && variants.Length != 1)
                {
                    throw new InvalidDataException("A model with spring bones must identify one field rig. Use a spring GLB for a specific field model when the file contains multiple model destinations.");
                }
                string modelId = address ?? variants[0].ModelId;
                if (!TryParseAddress(modelId, out string field, out int? entry, out string? mainCharacter))
                {
                    throw new InvalidDataException("Use models/field/<field>/<entry>.springs.glb or models/field/main_chr/<dNNN>.springs.glb.");
                }
                if (!packageOrder.TryGetValue(file.PackageId, out int order))
                {
                    order = packageOrder.Count;
                    packageOrder.Add(file.PackageId, order);
                }
                if (!packageSelectors.Add((order, modelId.ToLowerInvariant())))
                {
                    throw new InvalidDataException($"The package defines spring bones for {modelId} more than once.");
                }
                profiles.TryAdd(modelId, new(file, modelId, field, entry, mainCharacter, order, asset));
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                errors.Add(new("Memoria.InvalidFieldSpringBones", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        result = errors.Count == 0 ? new(profiles, Array.AsReadOnly(packageOrder.Keys.ToArray())) : Empty;
        diagnostics = Array.AsReadOnly(errors.ToArray());
        return errors.Count == 0;
    }

    public FieldSpringBoneMod? Resolve(string modelId, FieldModel original)
    {
        if (Definitions.Count == 0 || !modelId.StartsWith("field/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        FieldSpringBoneMod? specific = exactProfiles.GetValueOrDefault(modelId);
        FieldSpringBoneMod? shared = original.MainCharacterSourceId is { } mainCharacter ? sharedProfiles.GetValueOrDefault(mainCharacter) : null;
        return shared is null || specific is not null && specific.PackageOrder <= shared.PackageOrder ? specific : shared;
    }

    public FieldModel Apply(string modelId, FieldModel original, FieldModel model) => Resolve(modelId, original)?.Apply(model) ?? model;

    private static string? SpringPathAddress(string path)
    {
        if (!path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        int suffixLength = path.EndsWith(GlbSuffix, StringComparison.OrdinalIgnoreCase) ? GlbSuffix.Length : path.EndsWith(VrmSuffix, StringComparison.OrdinalIgnoreCase) ? VrmSuffix.Length : 0;
        return suffixLength == 0 ? null : path["models/".Length..^suffixLength].ToLowerInvariant();
    }

    private static bool TryParseAddress(string address, out string field, out int? entry, out string? mainCharacter)
    {
        field = string.Empty;
        entry = null;
        mainCharacter = null;
        string[] parts = address.Split('/');
        if (parts.Length != 3 || parts[0] != "field")
        {
            return false;
        }
        field = parts[1];
        string selector = parts[2];
        if (field == MainCharacterDirectory)
        {
            if (selector.Length != 4 || selector[0] != 'd' || !selector.Skip(1).All(char.IsAsciiDigit))
            {
                return false;
            }
            mainCharacter = selector;
            return true;
        }
        if (!ModelImportTarget.IsModelId(address) || !int.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || selector != index.ToString(CultureInfo.InvariantCulture))
        {
            return false;
        }
        entry = index;
        return true;
    }
}
