using Albeoris.Games.FF8.Imaging;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Resources;

/// <summary>Editor pixels use ordinary straight alpha; the native STP bit is retained separately.</summary>
public static class TexturePixelExchange
{
    /// <summary>Only source contracts retaining PSX coverage bits use alpha as STP. Desktop artwork uses straight alpha.</summary>
    public static bool UsesNativeFlags(TextureBuildContext context) => context.ReadInstalledOriginal is { } read ? UsesNativeFlags(context, read()) : UsesNativeFlagsCore(context);

    public static bool UsesNativeFlags(TextureBuildContext context, TextureValue installedOriginal) => context.Layout == "native-field-model-sheet" ? !installedOriginal.IsReplacement : UsesNativeFlagsCore(context) || !installedOriginal.IsReplacement && (context.Layout == "classic-model" || context.Layout is "classic-battle" or "classic-field-model" && context.Region.BitsPerPixel is 4 or 8 or 16);

    private static bool UsesNativeFlagsCore(TextureBuildContext context) => context.Layout is "classic-field-tile" or "native-battle-stage-page" or "classic-world-texl" or "classic-world-section" or "classic-world-sprite" or "classic-world-water-region" or "classic-world-vram-copy" or "native-battle-effect-upload" or "native-battle-effect-atlas" || context.Layout is "classic-battle" or "classic-field-model" && context.Source.Edition == GameDataEdition.Classic;
    public static PixelImage ToEditable(PixelImage native)
    {
        Rgba32[] pixels = native.Pixels.Select(pixel => pixel.A == 128 ? new Rgba32(pixel.R, pixel.G, pixel.B, 255) : pixel).ToArray();
        return new(native.Width, native.Height, pixels);
    }

    public static string CaptureNativeFlags(PixelImage native) => Convert.ToBase64String(native.Pixels.Select(pixel => pixel.A).ToArray());

    public static PixelImage RestoreNativeFlags(PixelImage edited, int width, int height, string alpha)
    {
        if (width <= 0 || height <= 0 || (long)width * height > PixelImage.MaximumPixelCount || alpha.Length > ((long)width * height + 2) / 3 * 4)
        {
            throw new InvalidDataException("Native coverage dimensions or encoded flags exceed their bound.");
        }
        byte[] flags;
        try
        {
            flags = Convert.FromBase64String(alpha);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Native coverage flags are not valid base64.", exception);
        }
        if (flags.Length != (long)width * height)
        {
            throw new InvalidDataException("Native coverage flag count differs from its source canvas.");
        }
        Rgba32[] pixels = edited.Pixels.ToArray();
        for (int y = 0; y < edited.Height; y++)
        {
            for (int x = 0; x < edited.Width; x++)
            {
                int index = y * edited.Width + x;
                Rgba32 pixel = pixels[index];
                byte sourceAlpha = flags[(int)((long)y * height / edited.Height) * width + (int)((long)x * width / edited.Width)];
                if (pixel.A == 255 && sourceAlpha == 128)
                {
                    pixels[index] = new(pixel.R, pixel.G, pixel.B, 128);
                }
            }
        }
        return new(edited.Width, edited.Height, pixels);
    }

    public static string Fingerprint(PixelImage image, string importContract = "")
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> dimensions = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions, image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], image.Height);
        hash.AppendData(dimensions);
        hash.AppendData(Encoding.UTF8.GetBytes(importContract));
        hash.AppendData(MemoryMarshal.AsBytes(image.Pixels.AsSpan()));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public static string SemanticFingerprint(PixelImage image, TextureTarget target, TextureTargetContract contract, string? coverageAlpha = null, int? coverageWidth = null, int? coverageHeight = null)
    {
        target.Validate();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(new { target.Game, target.Owner, target.Subresource, target.Palette, target.Variant, contract.Layout, contract.LogicalWidth, contract.LogicalHeight, Width = coverageAlpha is null ? (int?)null : coverageWidth ?? contract.LogicalWidth, Height = coverageAlpha is null ? (int?)null : coverageHeight ?? contract.LogicalHeight }));
        hash.AppendData(Encoding.ASCII.GetBytes(Fingerprint(image)));
        if (coverageAlpha is not null)
        {
            try
            {
                hash.AppendData(Convert.FromBase64String(coverageAlpha));
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException("Native coverage flags are not valid base64.", exception);
            }
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

public sealed record TextureExchangeMetadata(int SchemaVersion, TextureTarget Target, TextureTargetContract Contract, string? SourceSha256 = null, string? OriginalPixelSha256 = null, string? ExportedPixelSha256 = null, string? Provenance = null, string? NativeFlagsBase64 = null)
{
    public int? NativeWidth { get; init; }
    public int? NativeHeight { get; init; }
    public string? OriginalSemanticSha256 { get; init; }
    public string? ExportedSemanticSha256 { get; init; }
    public IReadOnlyList<string>? SourceAliases { get; init; }
    public const int MaximumBytes = 96 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    public byte[] Serialize() => ToBytes();

    public byte[] ToBytes()
    {
        Validate();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(this, Json);
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Texture authoring metadata exceeds its size limit.");
        }
        return bytes;
    }

    public static TextureExchangeMetadata Parse(byte[] bytes)
    {
        if (bytes.Length > MaximumBytes)
        {
            throw new InvalidDataException("Texture authoring metadata exceeds its size limit.");
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
            RejectDuplicates(document.RootElement);
            TextureExchangeMetadata metadata = JsonSerializer.Deserialize<TextureExchangeMetadata>(bytes, Json) ?? throw new InvalidDataException("Missing texture authoring metadata.");
            metadata.Validate();
            return metadata;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid texture authoring metadata. Re-export or explicitly remap this input.", exception);
        }
    }

    private void Validate()
    {
        if (SchemaVersion != TextureTarget.SchemaVersion || Target is null || Contract is null || string.IsNullOrWhiteSpace(Contract.Layout) || Contract.LogicalWidth <= 0 || Contract.LogicalHeight <= 0)
        {
            throw new InvalidDataException("Unsupported or incomplete texture authoring metadata.");
        }
        Target.Validate();
        if (SourceAliases is not null && (SourceAliases.Count > 4096 || SourceAliases.Any(alias => string.IsNullOrWhiteSpace(alias) || alias.Length > 1024 || alias.StartsWith('/') || alias.Contains(':') || alias.Contains('\\') || alias.Any(char.IsControl) || alias.Split('/').Any(part => part is "." or ".."))))
        {
            throw new InvalidDataException("Texture usage aliases require bounded relative game-resource names, without installation paths or traversal.");
        }
    }

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException($"Duplicate texture metadata property '{property.Name}'.");
                }
                RejectDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement member in element.EnumerateArray())
            {
                RejectDuplicates(member);
            }
        }
    }
}
