using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text.Json;

namespace Memoria.FF8.Mods;

public enum ItemType : byte
{
    Magazine = 9
}

public sealed record ShowImageItemUse(ModFile Image);

public sealed record ItemOverride(ItemType? Type, ShowImageItemUse? Use);

/// <summary>Effective item overrides from the active native packages.</summary>
public sealed class ItemOverrideMods
{
    public static ItemOverrideMods Empty { get; } = new([], []);
    private readonly FrozenDictionary<int, ItemOverride> items;

    private ItemOverrideMods(Dictionary<int, ItemOverride> items, IEnumerable<string> packageIds)
    {
        this.items = items.ToFrozenDictionary();
        PackageIds = Array.AsReadOnly(packageIds.ToArray());
    }

    public IReadOnlyList<string> PackageIds { get; }
    public ItemOverride? Find(int itemId) => items.GetValueOrDefault(itemId);

    public static bool IsItemDocument(ModFile file) => file.Format == ModFormat.Memoria && file.RelativePath.StartsWith("items/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public static bool TryPrepare(ModCatalogSnapshot catalog, int itemCount, out ItemOverrideMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<int, ItemOverride> selected = [];
        List<string> packages = [];
        List<ModDiagnostic> errors = [];
        var roots = catalog.Packages.ToDictionary(package => package.Id, StringComparer.OrdinalIgnoreCase);
        var packageFiles = catalog.Files.Where(file => file.Format == ModFormat.Memoria).GroupBy(file => file.PackageId, StringComparer.OrdinalIgnoreCase);
        foreach (var group in packageFiles)
        {
            ModFile[] documents = group.Where(IsItemDocument).ToArray();
            if (documents.Length == 0)
            {
                continue;
            }
            var files = group.ToDictionary(file => file.RelativePath, StringComparer.Ordinal);
            string root = roots[group.Key].RootDirectory;
            HashSet<int> seen = [];
            HashSet<ModFile> validatedImages = [];
            foreach (ModFile document in documents)
            {
                try
                {
                    (int id, ItemOverride change) = Read(document, root, itemCount, files, seen, validatedImages);
                    if (selected.TryGetValue(id, out ItemOverride? higher))
                    {
                        change = new(higher.Type ?? change.Type, higher.Use ?? change.Use);
                    }
                    selected[id] = change;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or ModTextureLoadException)
                {
                    errors.Add(new("Memoria.InvalidItemOverride", exception.Message, document.PackageId, exception is ModTextureLoadException load ? load.RelativePath : document.RelativePath));
                }
            }
            packages.Add(group.Key);
        }
        diagnostics = errors.AsReadOnly();
        result = errors.Count == 0 ? new(selected, packages) : Empty;
        return errors.Count == 0;
    }

    private static (int Id, ItemOverride Override) Read(ModFile manifest, string root, int itemCount, IReadOnlyDictionary<string, ModFile> files, HashSet<int> seen, HashSet<ModFile> validatedImages)
    {
        const int maximumDocumentBytes = 128 * 1024;
        if (manifest.ContentStamp is { Length: > maximumDocumentBytes })
        {
            throw new InvalidDataException("The item override exceeds its size limit.");
        }
        byte[] content = manifest.ReadBytes();
        if (content.Length > maximumDocumentBytes)
        {
            throw new InvalidDataException("The item override exceeds its size limit.");
        }

        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement item = document.RootElement;
        RequireFields(item, "id", "type", "use");
        if (!item.TryGetProperty("id", out JsonElement idElement) || idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt32(out int id) || id <= 0 || id > byte.MaxValue || id >= itemCount)
        {
            throw new InvalidDataException("An item override needs the ID of an existing inventory item.");
        }
        if (!seen.Add(id))
        {
            throw new InvalidDataException($"Item {id} has more than one override document in this mod.");
        }

        ItemType? type = null;
        if (item.TryGetProperty("type", out JsonElement typeElement))
        {
            if (typeElement.ValueKind != JsonValueKind.String || typeElement.GetString() != nameof(ItemType.Magazine))
            {
                throw new InvalidDataException("The item type must be Magazine.");
            }
            type = ItemType.Magazine;
        }

        ShowImageItemUse? use = null;
        if (item.TryGetProperty("use", out JsonElement useElement))
        {
            RequireFields(useElement, "action", "image");
            if (!useElement.TryGetProperty("action", out JsonElement action) || action.ValueKind != JsonValueKind.String || action.GetString() != "ShowImage"
                || !useElement.TryGetProperty("image", out JsonElement imageElement) || imageElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Item use must declare the ShowImage action and a PNG image path.");
            }

            string reference = imageElement.GetString()!;
            string absolute = ModResourcePath.GetAbsolutePath(root, manifest.RelativePath, reference);
            string relative = Path.GetRelativePath(root, absolute).Replace('\\', '/');
            if (!relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || !files.TryGetValue(relative, out ModFile? image) || image.AbsolutePath != absolute)
            {
                throw new InvalidDataException($"Image '{reference}' must name a PNG in this mod with matching case.");
            }
            if (validatedImages.Add(image))
            {
                ValidateImage(image);
            }
            use = new(image);
        }

        if (type is null && use is null)
        {
            throw new InvalidDataException("An item document needs a type or use override.");
        }
        return (id, new(type, use));
    }

    private static void RequireFields(JsonElement value, params string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Expected an item override object.");
        }
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
            {
                throw new InvalidDataException($"Unknown or repeated item property '{property.Name}'.");
            }
        }
    }

    private static void ValidateImage(ModFile file)
    {
        const int maximumImageBytes = 64 * 1024 * 1024;
        if (file.ContentStamp is { Length: > maximumImageBytes })
        {
            throw new InvalidDataException($"Image '{file.RelativePath}' exceeds the PNG file size limit.");
        }
        byte[] png = file.ReadBytes();
        if (png.Length is < 24 or > maximumImageBytes || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException($"Image '{file.RelativePath}' must be a PNG.");
        }
        uint width = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
        uint height = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
        if (width is 0 or > 8192 || height is 0 or > 8192 || (ulong)width * height > 32_000_000)
        {
            throw new InvalidDataException($"Image '{file.RelativePath}' exceeds the supported PNG dimensions.");
        }
        ModImageDecoder.Decode(file);
    }
}
