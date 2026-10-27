using Memoria.FF8.Resources;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tex;

namespace Memoria.FF8.Assets;

/// <summary>The original Triple Triad artwork, retained at its native square texture aspect ratio.</summary>
public sealed class CardArtwork
{
    public const int CardCount = 110;
    public const int ArtworkSize = 64;
    private const int CardsPerAtlas = 11;
    private const int AtlasColumns = 4;
    private const int AtlasSize = 256;
    private readonly PixelImage[] fronts;

    private CardArtwork(PixelImage[] fronts, PixelImage back)
    {
        this.fronts = fronts;
        Back = back;
    }

    /// <summary>The original gold card back from mc00, cell 11.</summary>
    public PixelImage Back { get; }

    /// <summary>Returns the unscaled artwork for a zero-based card ID, from Geezard (0) through Squall (109).</summary>
    public PixelImage GetFront(int cardId)
    {
        if ((uint)cardId >= CardCount)
        {
            throw new ArgumentOutOfRangeException(nameof(cardId));
        }
        return fronts[cardId];
    }

    /// <summary>Loads the original mc00.TEX through mc09.TEX atlases from menu.fs without extracting or modifying them.</summary>
    public static CardArtwork Load(string gameDirectory, ResourceService? resources = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IResourceSource provider = GameDataSourceFactory.Open(gameDirectory);
        resources ??= new(provider);
        PixelImage[] fronts = new PixelImage[CardCount];
        PixelImage? back = null;
        for (int atlasIndex = 0; atlasIndex < CardCount / CardsPerAtlas; atlasIndex++)
        {
            string name = $"mc{atlasIndex:00}.TEX";
            byte[] bytes = resources.Read(new ResourceKey("menu/" + name));
            NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/" + name), bytes);
            PixelImage atlas = TexFile.Read(bytes).Decode();
            if (atlas.Width != AtlasSize || atlas.Height != AtlasSize)
            {
                throw new NotSupportedException($"Original Triple Triad atlas {name} must be {AtlasSize}x{AtlasSize}, but is {atlas.Width}x{atlas.Height}.");
            }
            atlas = NativeArtworkTexture.Resolve(provider, new ResourceKey("menu/" + name), atlas, resources: resources);
            for (int cell = 0; cell < CardsPerAtlas; cell++)
            {
                fronts[atlasIndex * CardsPerAtlas + cell] = CropCell(atlas, cell);
            }
            if (atlasIndex == 0)
            {
                back = CropCell(atlas, CardsPerAtlas);
            }
        }
        return new CardArtwork(fronts, back!);
    }

    private static PixelImage CropCell(PixelImage atlas, int cell)
    {
        int size = ArtworkSize * NativeArtworkTexture.Scale(atlas, AtlasSize, AtlasSize);
        int left = cell % AtlasColumns * size;
        int top = cell / AtlasColumns * size;
        PixelImage result = new(size, size);
        for (int row = 0; row < size; row++)
        {
            atlas.Pixels.AsSpan((top + row) * atlas.Width + left, size).CopyTo(result.Pixels.AsSpan(row * size, size));
        }
        return result;
    }
}
