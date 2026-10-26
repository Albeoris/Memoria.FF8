using System.Buffers.Binary;
using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>The initialized ordinary PC icon texture and its original sprite-part directory.</summary>
public sealed class NativeBattlePcUiIconResources
{
    private readonly TexFile texture;
    private readonly Dictionary<(int Palette, bool Semi), PixelImage> images = [];

    private NativeBattlePcUiIconResources(byte[] directory, TexFile texture)
    {
        this.texture = texture;
        if (directory.Length < 4)
        {
            throw new InvalidDataException("The native icon directory lacks its count.");
        }
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(directory);
        if (count > (directory.Length - 4) / 4 || texture.Header.Width != 256 || texture.Header.Height != 256 || texture.Header.BytesPerPixel != 1)
        {
            throw new InvalidDataException("The ordinary PC icon initializer requires its bounded directory and indexed256x256 texture.");
        }
        IconCount = (int)count;
        Profile = new(1, 1, false, texture.Header.Unknown56, texture.Header.Unknown57, texture.Header.Width, texture.Header.Height, 1, 1);
        Binding = NativeBattlePcUiBinding.PrimaryOrdinary(ImmutableArray.CreateRange(directory), Profile);
    }

    public int IconCount { get; }
    public NativeBattlePcUiIconProfile Profile { get; }
    public NativeBattlePcUiBinding Binding { get; }

    public static NativeBattlePcUiIconResources ReadOrdinary(ResourceService resources)
    {
        return new(resources.Read(new ResourceKey("menu/icon.sp1")), TexFile.Read(resources.Read(new ResourceKey("menu/icon.tex"))));
    }

    public PixelImage Resolve(int page, int palette, bool semi)
    {
        if (page != 0 || (uint)palette >= (uint)texture.Palettes.Count)
        {
            throw new NotSupportedException("Native ordinary PC icons require their actual selected page and palette owner.");
        }
        if (!images.TryGetValue((palette, semi), out PixelImage? image))
        {
            image = texture.Decode(palette);
            if (semi)
            {
                for (int index = 0; index < image.Pixels.Length; index++)
                {
                    Rgba32 pixel = image.Pixels[index];
                    image.Pixels[index] = pixel with { A = pixel.A == 0 ? (byte)0 : (byte)127 };
                }
            }
            images.Add((palette, semi), image);
        }
        return image;
    }
}

public sealed partial class BattleAssets
{
    private NativeBattlePcUiIconResources? nativePcUiIcons;

    public NativeBattlePcUiIconResources ReadNativePcUiIcons()
    {
        return nativePcUiIcons ??= NativeBattlePcUiIconResources.ReadOrdinary(resources);
    }
}
