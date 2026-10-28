using System.Collections.Frozen;
using System.Text.Json;

namespace Memoria.FF8.Mods;

public enum TextureLayoutMode
{
    WholeNativeAtlas
}

/// <summary>Declares the complete logical canvas covered by one physical image; the source owns sampling coordinates.</summary>
public sealed record TextureLayoutMetadata(TextureLayoutMode Mode, int CanvasWidth, int CanvasHeight);

public static class TextureLayoutManifest
{
    public const string FileName = "texture-layouts.json";
    public const int MaximumBytes = 1024 * 1024;

    public static IReadOnlyDictionary<string, TextureLayoutMetadata> Parse(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Texture layout metadata exceeds its size limit.");
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            Dictionary<string, JsonElement> root = ReadObject(document.RootElement, "version", "textures");
            if (ReadInteger(root["version"]) != 1)
            {
                throw new InvalidDataException("Texture layout metadata requires version 1.");
            }
            if (root["textures"].ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Texture layouts require an object of exact relative image paths.");
            }
            Dictionary<string, TextureLayoutMetadata> layouts = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in root["textures"].EnumerateObject())
            {
                string path = ValidateImagePath(property.Name);
                Dictionary<string, JsonElement> declaration = ReadObject(property.Value, "layout", "canvasWidth", "canvasHeight");
                if (declaration["layout"].ValueKind != JsonValueKind.String || declaration["layout"].GetString() != "whole-native-atlas")
                {
                    throw new InvalidDataException("Texture layout declarations require the supported whole-native-atlas layout.");
                }
                int width = ReadInteger(declaration["canvasWidth"]);
                int height = ReadInteger(declaration["canvasHeight"]);
                if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                {
                    throw new InvalidDataException("Texture layout canvases require positive bounded pixel dimensions.");
                }
                if (!layouts.TryAdd(path, new(TextureLayoutMode.WholeNativeAtlas, width, height)))
                {
                    throw new InvalidDataException($"Duplicate texture layout image path '{path}'.");
                }
            }
            return layouts.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Texture layout metadata is not valid strict JSON.", exception);
        }
    }

    private static string ValidateImagePath(string path)
    {
        string normalized;
        try
        {
            normalized = ModCatalogSnapshot.NormalizePath(path);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Texture layout image paths must stay inside their package.", exception);
        }
        if (normalized != path || path.Any(character => char.IsControl(character) || "<>\"|?*".Contains(character)))
        {
            throw new InvalidDataException("Texture layout image paths require canonical relative forward-slash addresses.");
        }
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".dds", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Texture layout declarations must identify an exact DDS or PNG image.");
        }
        return normalized;
    }

    private static Dictionary<string, JsonElement> ReadObject(JsonElement element, params string[] members)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Texture layout metadata requires JSON objects.");
        }
        Dictionary<string, JsonElement> result = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!members.Contains(property.Name, StringComparer.Ordinal) || !result.TryAdd(property.Name, property.Value))
            {
                throw new InvalidDataException($"Unknown or duplicate texture layout property '{property.Name}'.");
            }
        }
        if (result.Count != members.Length)
        {
            throw new InvalidDataException("Texture layout metadata is missing required properties.");
        }
        return result;
    }

    private static int ReadInteger(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw new InvalidDataException("Texture layout metadata requires exact 32-bit integer values.");
        }
        return value;
    }
}
