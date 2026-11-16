using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;

namespace Memoria.FF8.Mods;

/// <summary>Prepared inventory book bindings, resolved from active native packages in catalog order.</summary>
public sealed class BookArtworkMods
{
    public const string ManifestPath = "items/book-artwork.json";
    public static BookArtworkMods Empty { get; } = new(new Dictionary<int, ModFile>(), []);
    private readonly FrozenDictionary<int, ModFile> artwork;

    private BookArtworkMods(Dictionary<int, ModFile> artwork, IEnumerable<string> packageIds)
    {
        this.artwork = artwork.ToFrozenDictionary();
        PackageIds = Array.AsReadOnly(packageIds.ToArray());
    }

    public IReadOnlyList<string> PackageIds { get; }
    public ModFile? Find(int itemId) => artwork.GetValueOrDefault(itemId);

    public static bool TryPrepare(ModCatalogSnapshot catalog, out BookArtworkMods result, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        Dictionary<int, ModFile> selected = [];
        List<string> packages = [];
        List<ModDiagnostic> errors = [];
        foreach (ModFile manifest in catalog.FindAll(ModFormat.Memoria, ManifestPath))
        {
            try
            {
                if (manifest.RelativePath != ManifestPath)
                {
                    throw new InvalidDataException($"Use the exact manifest path '{ManifestPath}'.");
                }
                IReadOnlyList<(int Id, ModFile Image)> bindings = Read(manifest, path =>
                    catalog.FindAll(ModFormat.Memoria, path).FirstOrDefault(file => file.RelativePath == path && file.PackageId.Equals(manifest.PackageId, StringComparison.OrdinalIgnoreCase)));
                foreach ((int itemId, ModFile image) in bindings)
                {
                    selected.TryAdd(itemId, image);
                }
                if (bindings.Count > 0)
                {
                    packages.Add(manifest.PackageId);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException or ModTextureLoadException)
            {
                errors.Add(new("Memoria.InvalidBookArtwork", exception.Message, manifest.PackageId, exception is ModTextureLoadException load ? load.RelativePath : manifest.RelativePath));
            }
        }
        diagnostics = errors.AsReadOnly();
        result = errors.Count == 0 ? new(selected, packages) : Empty;
        return errors.Count == 0;
    }

    internal static IReadOnlyList<(int Id, ModFile Image)> Read(ModFile manifest, Func<string, ModFile?> resolveImage)
    {
        const int maximumManifestBytes = 128 * 1024;
        if (manifest.ContentStamp is { Length: > maximumManifestBytes })
        {
            throw new InvalidDataException("The book artwork manifest exceeds its size limit.");
        }
        byte[] content = manifest.ReadBytes();
        if (content.Length > maximumManifestBytes)
        {
            throw new InvalidDataException("The book artwork manifest exceeds its size limit.");
        }
        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        if (!HasFields(root, "schemaVersion", "items") || !ReadInteger(root.GetProperty("schemaVersion"), out int schema) || schema != 1
            || root.GetProperty("items") is not { ValueKind: JsonValueKind.Array } items || items.GetArrayLength() > 256)
        {
            throw new InvalidDataException("Expected book artwork schema 1 and a list of up to 256 item bindings.");
        }
        HashSet<int> seen = [];
        HashSet<ModFile> validated = [];
        List<(int, ModFile)> bindings = [];
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (!HasFields(item, "itemId", "image") || !ReadInteger(item.GetProperty("itemId"), out int id) || id < byte.MinValue || id > byte.MaxValue || !seen.Add(id)
                || item.GetProperty("image").ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Each book artwork binding requires a unique item ID from 0 to 255 and an image path.");
            }
            string path = item.GetProperty("image").GetString()!;
            if (path.Contains('\\') || ModCatalogSnapshot.NormalizePath(path) != path || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || resolveImage(path) is not { } image)
            {
                throw new InvalidDataException($"Item {id} needs a PNG at a package-relative path with matching case: '{path}'.");
            }
            if (validated.Add(image))
            {
                ValidateImage(image);
            }
            bindings.Add((id, image));
        }
        return bindings;
    }

    private static bool HasFields(JsonElement value, string first, string second) =>
        value.ValueKind == JsonValueKind.Object && value.EnumerateObject().Count() == 2 && value.TryGetProperty(first, out _) && value.TryGetProperty(second, out _);

    private static bool ReadInteger(JsonElement value, out int result)
    {
        result = 0;
        return value.ValueKind == JsonValueKind.Number && int.TryParse(value.GetRawText(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
    }

    private static void ValidateImage(ModFile file)
    {
        const int maximumImageBytes = 64 * 1024 * 1024;
        if (file.ContentStamp is { Length: > maximumImageBytes })
        {
            throw new InvalidDataException($"Book image '{file.RelativePath}' exceeds the file size limit.");
        }
        byte[] png = file.ReadBytes();
        if (png.Length is < 24 or > maximumImageBytes || !png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !png.AsSpan(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException($"Book image '{file.RelativePath}' must be a PNG.");
        }
        uint width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
        uint height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
        if (width is 0 or > 8192 || height is 0 or > 8192 || (ulong)width * height > 32_000_000)
        {
            throw new InvalidDataException($"Book image '{file.RelativePath}' exceeds the supported PNG dimensions.");
        }
        ModImageDecoder.Decode(file);
    }
}
