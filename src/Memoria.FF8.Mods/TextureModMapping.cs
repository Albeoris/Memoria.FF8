using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed record TextureNativeCoverage(int Width, int Height, string Alpha);

/// <summary>Exceptional image mapping; ordinary canonical images need no sidecar.</summary>
public sealed record TextureModMapping(int SchemaVersion, TextureTarget Target, string Image, TextureTargetContract? Contract = null)
{
    public const string Extension = ".texture.json";
    public const int MaximumBytes = 96 * 1024 * 1024;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }

    [JsonIgnore]
    public TextureNativeCoverage? NativeCoverage
    {
        get
        {
            if (Extensions is null || !Extensions.TryGetValue("extensions", out JsonElement extensions) || !extensions.TryGetProperty("nativeCoverage", out JsonElement coverage))
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<TextureNativeCoverage>(coverage.GetRawText(), Options) ?? throw new InvalidDataException("Missing native texture coverage.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid native texture coverage metadata.", exception);
            }
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowDuplicateProperties = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public byte[] ToBytes()
    {
        Validate();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(this, Options);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Texture mapping exceeds its metadata size limit.");
        }
        return bytes;
    }

    public static TextureModMapping Parse(byte[] bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Texture mapping exceeds its metadata size limit.");
        }
        try
        {
            TextureModMapping mapping = JsonSerializer.Deserialize<TextureModMapping>(bytes, Options) ?? throw new InvalidDataException("Texture mapping is empty.");
            mapping.Validate();
            return mapping;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid texture mapping JSON or duplicate properties.", exception);
        }
    }

    private void Validate()
    {
        if (SchemaVersion != TextureTarget.SchemaVersion || Target is null)
        {
            throw new InvalidDataException("Unsupported texture mapping version or missing target.");
        }
        Target.Validate();
        TextureModPaths.ValidateRelative(Image);
        if (!TextureModPaths.IsImage(Image))
        {
            throw new InvalidDataException("A texture mapping must reference a PNG or DDS image.");
        }
        if (Extensions?.Keys.Any(key => key != "extensions") == true)
        {
            throw new InvalidDataException("Unknown texture mapping semantics; optional data belongs in extensions.");
        }
        if (Extensions?.TryGetValue("extensions", out JsonElement optional) == true && optional.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Texture optional extensions must be an object.");
        }
        if (NativeCoverage is { } coverage && (coverage.Width <= 0 || coverage.Height <= 0 || coverage.Alpha is null || (long)coverage.Width * coverage.Height > Albeoris.Games.FF8.Imaging.PixelImage.MaximumPixelCount || coverage.Alpha.Length > ((long)coverage.Width * coverage.Height + 2) / 3 * 4))
        {
            throw new InvalidDataException("Native coverage exceeds its source pixel bounds.");
        }
        if (NativeCoverage is { } validCoverage)
        {
            try
            {
                if (Convert.FromBase64String(validCoverage.Alpha).Length != (long)validCoverage.Width * validCoverage.Height)
                {
                    throw new InvalidDataException("Native coverage flag count differs from its source canvas.");
                }
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Invalid native coverage base64.", exception);
            }
        }
        if (Contract is not null && (string.IsNullOrWhiteSpace(Contract.Layout) || Contract.LogicalWidth <= 0 || Contract.LogicalHeight <= 0))
        {
            throw new InvalidDataException("Invalid texture mapping source contract.");
        }
    }
}

internal static class TextureModPaths
{
    public static bool IsImage(string path) => Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".dds", StringComparison.OrdinalIgnoreCase);

    public static void ValidateRelative(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 4096 || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.Split('/').Length > 64 || path.Split('/').Any(part => part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 || part.Any(char.IsControl) || ReservedName(part)))
        {
            throw new InvalidDataException("Texture paths must be portable relative paths without traversal.");
        }
    }

    private static bool ReservedName(string part)
    {
        string stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] is >= '0' and <= '9';
    }
}
