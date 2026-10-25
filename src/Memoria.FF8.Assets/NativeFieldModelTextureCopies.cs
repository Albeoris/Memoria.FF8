using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

internal static class NativeFieldModelTextureCopies
{
    public static byte[]? ProjectPalette(FieldModel model, NativeFieldRegistrationResources source, NativeFieldRegistrationState registration, NativeFieldRegisteredTexture texture)
    {
        if (model.NativeTexturePalettes.Count == 0 || model.NativeTexturePalettes[0].Length < 512)
        {
            return null;
        }
        NativeFieldRegisteredResource donor = registration.Resources.OrderBy(resource => resource.Index).First(resource => resource.Textures.Contains(texture));
        NativeFieldOneModelEntry entry = source.Directory[donor.ModelId];
        NativeFieldRegistrationLoaderInputs loader = source.Loader ?? throw new NotSupportedException("Registered field palette guards require their original PCB source.");
        return NativeFieldRegistrationMachine.ProjectPalette532A40(model.NativeTexturePalettes[0].AsSpan().Slice(0, 512), loader.Colors.Data.AsSpan(), entry.RawName.AsSpan());
    }

    public static TextureValue Apply(TextureValue value, PixelImage native, FieldModel model, NativeFieldRegisteredTexture registered, byte[] bank, byte[]? registeredPalette)
    {
        if (!value.IsReplacement || value.Variants is not { } variants || !variants.TryGetValue("eyes-closed", out PixelImage? closed) || model.NativeEyes is not { Texture: { Depth: 1 } original } eyes || eyes.Bank.Length == 0)
        {
            return value;
        }
        NativeEyeCopy? openCopy = eyes.Copy(false);
        NativeEyeCopy? closedCopy = eyes.Copy(true);
        if (openCopy is null || closedCopy is null || registered.Mode != 1 || registered.WidthWords != original.WidthWords || registered.Height != original.Height || registeredPalette is not { Length: 512 })
        {
            return Fallback();
        }
        if (!bank.AsSpan((registered.PaletteY * 1024 + registered.PaletteX) * 2, 512).SequenceEqual(registeredPalette))
        {
            return Fallback();
        }
        byte[] openWords = Copy(openCopy);
        byte[] closedWords = Copy(closedCopy);
        if (Matches(openWords) || Matches(original.Words))
        {
            return value;
        }
        if (openWords.AsSpan().SequenceEqual(closedWords) || !Matches(closedWords))
        {
            return Fallback();
        }
        if (closed.Width != value.Image.Width || closed.Height != value.Image.Height || value.LogicalWidth != registered.WidthWords * 2 || value.LogicalHeight != registered.Height)
        {
            throw new InvalidDataException("Registered eye planes must preserve their source material's logical and physical canvases.");
        }
        int left = checked(closedCopy.DestinationXWords * 2);
        int top = closedCopy.DestinationY;
        int width = checked(closedCopy.WidthWords * 2);
        int height = closedCopy.Height;
        if ((long)left * closed.Width % value.LogicalWidth != 0 || (long)width * closed.Width % value.LogicalWidth != 0 || (long)top * closed.Height % value.LogicalHeight != 0 || (long)height * closed.Height % value.LogicalHeight != 0)
        {
            throw new NotSupportedException("Registered eye-copy boundaries must map to whole replacement pixels.");
        }
        int imageX = left * closed.Width / value.LogicalWidth;
        int imageY = top * closed.Height / value.LogicalHeight;
        int imageWidth = width * closed.Width / value.LogicalWidth;
        int imageHeight = height * closed.Height / value.LogicalHeight;
        PixelImage image = new(value.Image.Width, value.Image.Height, value.Image.Pixels.ToArray());
        for (int row = 0; row < imageHeight; row++)
        {
            closed.Pixels.AsSpan((imageY + row) * closed.Width + imageX, imageWidth).CopyTo(image.Pixels.AsSpan((imageY + row) * image.Width + imageX, imageWidth));
        }
        return value with { Image = image, MipLevels = [image] };

        TextureValue Fallback() => value with { Image = native, IsReplacement = false, MipLevels = null };

        byte[] Copy(NativeEyeCopy copy)
        {
            byte[] words = original.Words.ToArray();
            for (int row = 0; row < copy.Height; row++)
            {
                copy.Words.AsSpan(row * copy.WidthWords * 2, copy.WidthWords * 2).CopyTo(words.AsSpan(((copy.DestinationY + row) * original.WidthWords + copy.DestinationXWords) * 2, copy.WidthWords * 2));
            }
            return words;
        }

        bool Matches(ReadOnlySpan<byte> expected)
        {
            for (int row = 0; row < registered.Height; row++)
            {
                if (!bank.AsSpan(((registered.Y + row) * 1024 + registered.X) * 2, registered.WidthWords * 2).SequenceEqual(expected.Slice(row * registered.WidthWords * 2, registered.WidthWords * 2)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
