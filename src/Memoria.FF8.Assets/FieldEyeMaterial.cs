using System.Globalization;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Immutable visual projection of an original field eye-bank copy, independent of simulation and VRAM ownership.</summary>
public static class FieldEyeMaterial
{
    private static readonly ConditionalWeakTable<NativeFieldEyeDefinition, ConditionalWeakTable<PixelImage, EyeImages>> images = new();

    public static bool HasNativeVariants(FieldModel model)
    {
        return model.Textures.Count > 0 && HasNativeVariants(model.NativeEyes, model.Textures[0].Width, model.Textures[0].Height);
    }

    internal static bool HasNativeVariants(NativeFieldEyeDefinition? eyes, int width, int height)
    {
        if (eyes is not { Bank.Length: >= 16, Texture: { Depth: 1 } texture } || width != texture.WidthWords * 2 || height != texture.Height)
        {
            return false;
        }
        try
        {
            NativeEyeCopy? open = eyes.Copy(false);
            NativeEyeCopy? closed = eyes.Copy(true);
            return open is not null && closed is not null && !open.Words.AsSpan().SequenceEqual(closed.Words);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            return false;
        }
    }

    public static IReadOnlyDictionary<string, PixelImage> NativeVariants(FieldModel model)
    {
        if (!HasNativeVariants(model))
        {
            throw new NotSupportedException("The selected material has no proved complete native eye-copy variants.");
        }
        return NativeVariants(model.NativeEyes!, model.Textures[0]);
    }

    public static IReadOnlyDictionary<string, PixelImage> NativeVariants(NativeFieldEyeDefinition eyes, PixelImage source)
    {
        if (!HasNativeVariants(eyes, source.Width, source.Height))
        {
            throw new NotSupportedException("The selected material has no proved complete native eye-copy variants.");
        }
        EyeImages selected = images.GetValue(eyes, static _ => new()).GetValue(source, image => new(Copy(image, eyes.Copy(false)), Copy(image, eyes.Copy(true))));
        return new Dictionary<string, PixelImage> { ["eyes-open"] = selected.Open, ["eyes-closed"] = selected.Closed };
    }

    private const string FramePrefix = "eyes-frame-";

    internal static string FrameVariant(int frame) => FramePrefix + frame.ToString(CultureInfo.InvariantCulture);

    internal static Func<IReadOnlyDictionary<string, PixelImage>> VariantFactory(NativeFieldEyeDefinition eyes, PixelImage image, string? requested = null) => () =>
    {
        IReadOnlyDictionary<string, PixelImage> variants = NativeVariants(eyes, image);
        if (requested?.StartsWith(FramePrefix, StringComparison.Ordinal) != true)
        {
            return variants;
        }
        return new Dictionary<string, PixelImage>(variants) { [requested] = Project(eyes, image, requested) };
    };
    internal static Func<PixelImage, string, PixelImage> VariantProjector(NativeFieldEyeDefinition eyes) => (image, variant) => Project(eyes, image, variant);

    /// <summary>Projects the existing native bank copy onto an edited complete image without advancing the eye process.</summary>
    public static PixelImage Project(NativeFieldEyeDefinition eyes, PixelImage source, string variant)
    {
        if (eyes.Texture is not { Depth: 1 } texture)
        {
            throw new NotSupportedException("The requested eye variant has no proved complete 8bpp source copy.");
        }
        NativeEyeCopy copy = variant switch
        {
            "eyes-open" or "eyes-closed" => eyes.Copy(variant == "eyes-closed") ?? throw new NotSupportedException("The requested eye bank is absent."),
            _ when variant.StartsWith(FramePrefix, StringComparison.Ordinal) && int.TryParse(variant.AsSpan(FramePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int frame) && frame > 1 && eyes.Bank.Length > 5 && frame < eyes.Bank[5] => eyes.CopyFrame(frame),
            _ => throw new NotSupportedException("The requested eye variant has no proved complete 8bpp source copy.")
        };
        int logicalWidth = texture.WidthWords * 2;
        int logicalHeight = texture.Height;
        if ((long)source.Width * logicalHeight != (long)source.Height * logicalWidth)
        {
            throw new InvalidDataException("An edited native eye image must retain its complete source aspect ratio.");
        }
        int X(int value)
        {
            if ((long)value * source.Width % logicalWidth != 0)
            {
                throw new InvalidDataException("The edited image splits a native eye-copy texel boundary. Use a compatible uniform scale.");
            }
            return checked((int)((long)value * source.Width / logicalWidth));
        }
        int Y(int value)
        {
            if ((long)value * source.Height % logicalHeight != 0)
            {
                throw new InvalidDataException("The edited image splits a native eye-copy row boundary. Use a compatible uniform scale.");
            }
            return checked((int)((long)value * source.Height / logicalHeight));
        }
        int sourceX = X(copy.SourceXWords * 2);
        int sourceY = Y(copy.SourceY);
        int destinationX = X(copy.DestinationXWords * 2);
        int destinationY = Y(copy.DestinationY);
        int width = X(copy.WidthWords * 2);
        int height = Y(copy.Height);
        Rgba32[] pixels = source.Pixels.ToArray();
        for (int row = 0; row < height; row++)
        {
            source.Pixels.AsSpan((sourceY + row) * source.Width + sourceX, width).CopyTo(pixels.AsSpan((destinationY + row) * source.Width + destinationX, width));
        }
        return new(source.Width, source.Height, pixels);
    }

    public static PixelImage Resolve(FieldModel model, bool closed)
    {
        ArgumentNullException.ThrowIfNull(model);
        NativeFieldEyeDefinition eyes = model.NativeEyes ?? throw new NotSupportedException("The selected field appearance has no original eye-bank definition.");
        if (model.Textures.Count == 0)
        {
            throw new InvalidDataException("The selected eye-bank appearance has no texture0.");
        }
        PixelImage source = model.Textures[0];
        if (model.Textures is ResourceTextureList resources)
        {
            var value = resources.GetTexture(0);
            if (value.Variants is not null && value.Variants.TryGetValue("eyes-closed", out PixelImage? variant))
            {
                if (variant.Width != source.Width || variant.Height != source.Height)
                {
                    throw new InvalidDataException("A field eye material variant must retain the selected texture's canvas.");
                }
                return closed ? variant : value.Variants.TryGetValue("eyes-open", out PixelImage? open) ? open : source;
            }
            if (value.IsReplacement)
            {
                return !closed ? source : throw new NotSupportedException("The selected field texture replacement requires an explicit closed-eye material variant.");
            }
        }
        NativeEyeTexture texture = eyes.Texture ?? throw new NotSupportedException("The selected eye-bank appearance has no original texture allocation.");
        if (texture.Depth != 1 || source.Width != texture.WidthWords * 2 || source.Height != texture.Height || ResourceTextureList.LogicalSize(model.Textures, 0) != new System.Numerics.Vector2(source.Width, source.Height))
        {
            throw new NotSupportedException("An original field eye copy requires its complete unscaled 8bpp texture0 canvas.");
        }
        ConditionalWeakTable<PixelImage, EyeImages> byImage = images.GetValue(eyes, static _ => new());
        EyeImages selected = byImage.GetValue(source, image => new(Copy(image, eyes.Copy(false)), Copy(image, eyes.Copy(true))));
        return closed ? selected.Closed : selected.Open;
    }

    private static PixelImage Copy(PixelImage source, NativeEyeCopy? copy)
    {
        if (copy is null)
        {
            throw new NotSupportedException("The selected field appearance has no applicable native eye-bank copy.");
        }
        int width = checked(copy.WidthWords * 2);
        int sourceX = checked(copy.SourceXWords * 2);
        int destinationX = checked(copy.DestinationXWords * 2);
        Rgba32[] pixels = source.Pixels.ToArray();
        for (int row = 0; row < copy.Height; row++)
        {
            source.Pixels.AsSpan((copy.SourceY + row) * source.Width + sourceX, width).CopyTo(pixels.AsSpan((copy.DestinationY + row) * source.Width + destinationX, width));
        }
        return new(source.Width, source.Height, pixels);
    }

    private sealed record EyeImages(PixelImage Open, PixelImage Closed);
}
