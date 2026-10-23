using System.Buffers.Binary;
using System.Security.Cryptography;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Native Triple Triad sprites read from authenticated edition data.</summary>
public sealed class NativeTripleTriadArtwork
{
    private readonly PixelImage[] elements;
    private readonly PixelImage[] fronts;

    private NativeTripleTriadArtwork(PixelImage[] elements, PixelImage[] fronts, PixelImage back, PixelImage background, PixelImage boardBackground)
    {
        this.elements = elements;
        this.fronts = fronts;
        Back = back;
        Background = background;
        BoardBackground = boardBackground;
    }

    /// <summary>The original gold back used by native 53A590 for hidden opponent cards.</summary>
    public PixelImage Back { get; }

    /// <summary>The original 384 by 224 parchment background used by the native rules screen.</summary>
    public PixelImage Background { get; }

    /// <summary>The original 384 by 224 board background used during card selection and play.</summary>
    public PixelImage BoardBackground { get; }

    public PixelImage GetFront(int cardId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cardId);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(cardId, fronts.Length);
        return fronts[cardId];
    }

    public PixelImage GetElement(TripleTriadElement element)
    {
        int index = (int)element - 1;
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, elements.Length);
        return elements[index];
    }

    public static NativeTripleTriadArtwork Load(string gameDirectory, ResourceService? resources = null)
    {
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        NativeTripleTriadArtwork original = Read(GameDataResources.ReadNativeMemory(new ResourceService(source)));
        PixelImage[] fronts = Enumerable.Range(0, CardArtwork.CardCount).Select(id => NativeArtworkTexture.Resolve(source, new ResourceKey("cards/cards"), original.fronts[id], subresource: $"front/{id}", resources: resources)).ToArray();
        PixelImage back = NativeArtworkTexture.Resolve(source, new ResourceKey("cards/cards"), original.Back, subresource: "back", resources: resources);
        PixelImage[] elements = Enumerable.Range(0, original.elements.Length).Select(id => NativeArtworkTexture.Resolve(source, new ResourceKey("cards/cards"), original.elements[id], subresource: $"element/{id}", resources: resources)).ToArray();
        PixelImage background = NativeArtworkTexture.Resolve(source, new ResourceKey("cards/cards"), original.Background, subresource: "background", resources: resources);
        PixelImage boardBackground = NativeArtworkTexture.Resolve(source, new ResourceKey("cards/cards"), original.BoardBackground, subresource: "board-background", resources: resources);
        return new NativeTripleTriadArtwork(elements, fronts, back, background, boardBackground);
    }

    public static NativeTripleTriadArtwork ReadExecutable(byte[] executable)
    {
        return Read(new NativeExecutableMemory(executable));
    }

    public static NativeTripleTriadArtwork Read(IGameNativeMemorySource memory)
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(memory.SourceHash))
        {
            throw new NotSupportedException("Triple Triad artwork requires authenticated English native data.");
        }
        // Native 5343xx loads B96A90 via 539450. 53A100 draws each element with tpage 0xC,
        // UV (index%4*64,(index/4+1)*16), CLUT (x0,y225+index); the CLUT is 48 words wide.
        TimImage sprites = ReadTim(memory, 0xB96A90, 76832);
        if (sprites.PixelWidth != 768 || sprites.Height != 192 || sprites.Clut is not { Width: 48, Height: 32 })
        {
            throw new InvalidDataException("Native Triple Triad sprite texture has unexpected dimensions.");
        }
        PixelImage[] elements = new PixelImage[8];
        for (int index = 0; index < elements.Length; index++)
        {
            elements[index] = CropIndexed(sprites, index % 4 * 64, (index / 4 + 1) * 16, 16, 16, (index + 1) * 48);
        }
        // Native 53A428..53A4BA uses a separate 128-color half-row CLUT for each card.
        // Its keyed background differs from the opaque inventory-menu mcXX.TEX artwork.
        TimImage cards = ReadTim(memory, 0xBA9B10, 487456);
        if (cards.PixelWidth != 1792 || cards.Height != 256 || cards.Clut is not { Width: 256, Height: 56 })
        {
            throw new InvalidDataException("Native Triple Triad card texture has unexpected dimensions.");
        }
        PixelImage[] fronts = new PixelImage[CardArtwork.CardCount];
        for (int cardId = 0; cardId < fronts.Length; cardId++)
        {
            fronts[cardId] = CropIndexed(cards, (cardId >> 3) * 128 + (cardId % 2) * 64, ((cardId >> 1) & 3) * 64, 64, 64, (cardId >> 1) * 256 + (cardId % 2) * 128);
        }
        // Native 53A590: tpage 0x9D, UV (0,192)..(63,255), CLUT 0x3FF0.
        PixelImage back = CropIndexed(cards, 1664, 192, 64, 64, 55 * 256);
        // Native 536C80 queues C4AB58 through 539410 as a raw framebuffer image.
        // The display environment initialized by 537DD0 supplies its 384 by 224 extent.
        PixelImage background = ReadFramebuffer(memory.Read(0xC4AB58, 384 * 224 * 2));
        // Native 5370E0 queues C20B44 through the same framebuffer drawing contract.
        PixelImage boardBackground = ReadFramebuffer(memory.Read(0xC20B44, 384 * 224 * 2));
        return new NativeTripleTriadArtwork(elements, fronts, back, background, boardBackground);
    }

    private static PixelImage ReadFramebuffer(byte[] bytes)
    {
        PixelImage background = new(384, 224);
        for (int index = 0; index < background.Pixels.Length; index++)
        {
            ushort pixel = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2));
            Rgba32 color = Rgba32.FromPsxBgr555Display(pixel);
            background.Pixels[index] = new Rgba32(color.R, color.G, color.B);
        }
        return background;
    }

    private static TimImage ReadTim(IGameNativeMemorySource memory, uint address, int expectedLength)
    {
        byte[] bytes = memory.Read(address, expectedLength);
        int clutBytes = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
        if (clutBytes < 12 || clutBytes > bytes.Length - 20)
        {
            throw new InvalidDataException("Native Triple Triad palette block exceeds its bounded texture.");
        }
        int imageBytes = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8 + clutBytes));
        if (imageBytes < 12 || imageBytes != bytes.Length - 8 - clutBytes)
        {
            throw new InvalidDataException("Native Triple Triad pixel block exceeds its bounded texture.");
        }
        return TimFile.Read(bytes).Images.Single();
    }

    private static PixelImage CropIndexed(TimImage image, int left, int top, int width, int height, int paletteOffset)
    {
        PixelImage result = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = image.Pixels[(top + y) * image.PixelWidth + left + x];
                result[x, y] = Rgba32.FromPsxBgr555Display(image.Clut!.Colors[paletteOffset + index]);
            }
        }
        return result;
    }
}
