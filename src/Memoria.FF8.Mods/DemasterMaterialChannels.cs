using System.Globalization;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;

namespace Memoria.FF8.Mods;

public readonly record struct PreparedSurfaceTexel(float Occlusion, float Roughness, float Metalness);

public readonly record struct PreparedGlowTexel(Vector3 LinearColor, float Coverage)
{
    public Vector3 Composite(Vector3 lit, float showing, float strength)
    {
        showing = Math.Clamp(showing, 0, 1);
        return lit * (1 - Coverage * showing) + LinearColor * (showing * strength);
    }
}

public readonly record struct PreparedFieldTexel(Vector3 LinearAlbedo, float Coverage, Vector3 BakedLight, float SteadyLight, Vector3 ViewNormal, float Depth, PreparedSurfaceTexel Surface, float EmissiveMask, PreparedGlowTexel? Glow)
{
    public Vector3 PaintedLight => LinearAlbedo * BakedLight;

    public Vector3 CompositeGlow(Vector3 lit, float showing, float strength)
    {
        if (Glow is { } glow)
        {
            return glow.Composite(lit, showing, strength);
        }
        return Vector3.Lerp(lit, PaintedLight * strength, Math.Clamp(EmissiveMask * showing, 0, 1));
    }
}

public readonly record struct PreparedSceneTexel(PreparedSurfaceTexel Surface, float Height, PreparedGlowTexel Glow);

public sealed class PreparedFieldMaterial
{
    internal PreparedFieldMaterial(PixelImage albedo, PixelImage lightmap, float lightmapScale, PixelImage? normalDepth, PixelImage? surface, PixelImage? glow)
    {
        Albedo = albedo;
        Lightmap = lightmap;
        LightmapScale = lightmapScale;
        NormalDepth = normalDepth;
        Surface = surface;
        Glow = glow;
    }

    public PixelImage Albedo { get; }
    public PixelImage Lightmap { get; }
    public float LightmapScale { get; }
    public PixelImage? NormalDepth { get; }
    public PixelImage? Surface { get; }
    public PixelImage? Glow { get; }

    public PreparedFieldTexel ReadTexel(int x, int y)
    {
        Rgba32 albedo = Albedo[x, y];
        Rgba32 light = Lightmap[x, y];
        Rgba32? normal = NormalDepth?[x, y];
        Rgba32? surface = Surface?[x, y];
        PreparedGlowTexel? glow = Glow is { } glowImage ? DemasterMaterialChannels.ReadGlow(glowImage[x, y]) : null;
        Vector3 viewNormal = normal is { } n ? Vector3.Normalize(new Vector3(2 * n.R / 255f - 1, 1 - 2 * n.G / 255f, 1 - 2 * n.B / 255f)) : -Vector3.UnitZ;
        return new(DemasterMaterialChannels.LinearRgb(albedo), albedo.A / 255f, DemasterMaterialChannels.LinearRgb(light) * LightmapScale, light.A / 255f, viewNormal, normal?.A / 255f ?? 0.5f, DemasterMaterialChannels.ReadSurface(surface, 0.8f), glow is null ? surface?.A / 255f ?? 0 : 0, glow);
    }

    public PixelImage BakePaintedLight()
    {
        PixelImage result = new(Albedo.Width, Albedo.Height);
        for (int index = 0; index < result.Pixels.Length; index++)
        {
            Rgba32 albedo = Albedo.Pixels[index];
            Vector3 paint = DemasterMaterialChannels.LinearRgb(albedo) * DemasterMaterialChannels.LinearRgb(Lightmap.Pixels[index]) * LightmapScale;
            result.Pixels[index] = new(DemasterMaterialChannels.SrgbByte(paint.X), DemasterMaterialChannels.SrgbByte(paint.Y), DemasterMaterialChannels.SrgbByte(paint.Z), albedo.A);
        }
        return result;
    }
}

public sealed class PreparedSceneMaterial
{
    internal PreparedSceneMaterial(PixelImage? surface, PixelImage? glow, bool additiveGlow)
    {
        Surface = surface;
        Glow = glow;
        AdditiveGlow = additiveGlow;
    }

    public PixelImage? Surface { get; }
    public PixelImage? Glow { get; }
    public bool AdditiveGlow { get; }

    public PreparedSceneTexel ReadTexel(int x, int y)
    {
        Rgba32? surface = Surface?[x, y];
        PreparedGlowTexel glow = Glow is { } glowImage ? DemasterMaterialChannels.ReadGlow(glowImage[x, y], AdditiveGlow) : default;
        return new(DemasterMaterialChannels.ReadSurface(surface, 0.7f), surface?.A / 255f ?? 1, glow);
    }
}

/// <summary>Reads prepared texture channels while preserving each role's original alpha data.</summary>
public static class DemasterMaterialChannels
{
    private static readonly float[] linearByByte = Enumerable.Range(0, 256).Select(value => ToLinear(value / 255f)).ToArray();

    public static PreparedFieldMaterial LoadField(ModFile albedo, ModFile lightmap, float lightmapScale, ModFile? normalDepth = null, ModFile? surface = null, ModFile? glow = null)
    {
        if (!float.IsFinite(lightmapScale) || lightmapScale <= 0)
        {
            throw new InvalidDataException("Prepared material requires a finite positive lightmap scale.");
        }
        PixelImage albedoImage = ModImageDecoder.Decode(albedo).Image;
        return new(albedoImage, Decode(lightmap, albedoImage), lightmapScale, DecodeOptional(normalDepth, albedoImage), DecodeOptional(surface, albedoImage), DecodeOptional(glow, albedoImage));
    }

    public static PreparedSceneMaterial LoadScene(ModFile? surface, ModFile? glowPicture, ModFile? additiveGlow)
    {
        PixelImage? surfaceImage = surface is null ? null : ModImageDecoder.Decode(surface).Image;
        ModFile? glow = glowPicture ?? additiveGlow;
        PixelImage? glowImage = glow is null ? null : ModImageDecoder.Decode(glow).Image;
        return new(surfaceImage, glowImage, glowPicture is null && additiveGlow is not null);
    }

    public static float ReadLayerLightmapScale(string text, uint layerId)
    {
        float scale = 1;
        float? layerScale = null;
        string layerKey = $"lightmap_scale.{layerId:X8}";
        using StringReader reader = new(text);
        while (reader.ReadLine() is { } line)
        {
            int comment = line.AsSpan().IndexOfAny(';', '#');
            ReadOnlySpan<char> statement = comment < 0 ? line.AsSpan() : line.AsSpan(0, comment);
            int equals = statement.IndexOf('=');
            if (equals < 0)
            {
                continue;
            }
            ReadOnlySpan<char> key = statement[..equals].Trim();
            bool layer = key.Equals(layerKey, StringComparison.OrdinalIgnoreCase);
            if (!layer && !key.Equals("lightmap_scale", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!float.TryParse(statement[(equals + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || !float.IsFinite(value) || value <= 0)
            {
                throw new InvalidDataException($"Invalid prepared material scale for {key}.");
            }
            if (layer)
            {
                layerScale = value;
            }
            else
            {
                scale = value;
            }
        }
        return layerScale ?? scale;
    }

    internal static PreparedGlowTexel ReadGlow(Rgba32 color, bool additive = false)
    {
        return new(LinearRgb(color), additive ? 0 : color.A / 255f);
    }

    internal static PreparedSurfaceTexel ReadSurface(Rgba32? color, float fallbackRoughness)
    {
        return color is { } packed ? new(packed.R / 255f, packed.G / 255f, packed.B / 255f) : new(1, fallbackRoughness, 0);
    }

    internal static Vector3 LinearRgb(Rgba32 color)
    {
        return new(linearByByte[color.R], linearByByte[color.G], linearByByte[color.B]);
    }

    internal static byte SrgbByte(float linear)
    {
        linear = Math.Clamp(linear, 0, 1);
        float srgb = linear <= 0.0031308f ? 12.92f * linear : 1.055f * MathF.Pow(linear, 1 / 2.4f) - 0.055f;
        return (byte)MathF.Round(srgb * 255);
    }

    private static float ToLinear(float srgb)
    {
        return srgb <= 0.04045f ? srgb / 12.92f : MathF.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    private static PixelImage? DecodeOptional(ModFile? file, PixelImage albedo)
    {
        return file is null ? null : Decode(file, albedo);
    }

    private static PixelImage Decode(ModFile file, PixelImage albedo)
    {
        PixelImage image = ModImageDecoder.Decode(file).Image;
        if (image.Width != albedo.Width || image.Height != albedo.Height)
        {
            throw new InvalidDataException($"Prepared material {file.RelativePath} does not match the {albedo.Width}x{albedo.Height} layer.");
        }
        return image;
    }
}
