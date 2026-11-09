using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Immutable visual projection of an original field eye-bank copy, independent of simulation and VRAM ownership.</summary>
public static class FieldEyeMaterial
{
    private static readonly ConditionalWeakTable<NativeFieldEyeDefinition, ConditionalWeakTable<PixelImage, EyeImages>> images = new();

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
                return closed ? variant : source;
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
