using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
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
    public static CardArtwork Load(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(gameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        PixelImage[] fronts = new PixelImage[CardCount];
        PixelImage? back = null;
        for (int atlasIndex = 0; atlasIndex < CardCount / CardsPerAtlas; atlasIndex++)
        {
            string name = $"mc{atlasIndex:00}.TEX";
            IFlArchiveEntry entry = archive.Entries.Single(entry => entry.RelativePath.Replace('\\', '/').Split('/')[^1].Equals(name, StringComparison.OrdinalIgnoreCase));
            using Stream input = entry.OpenForRead();
            using MemoryStream content = new();
            input.CopyTo(content);
            PixelImage atlas = TexFile.Read(content.ToArray()).Decode();
            if (atlas.Width != AtlasSize || atlas.Height != AtlasSize)
            {
                throw new NotSupportedException($"Original Triple Triad atlas {name} must be {AtlasSize}x{AtlasSize}, but is {atlas.Width}x{atlas.Height}.");
            }
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
        int left = cell % AtlasColumns * ArtworkSize;
        int top = cell / AtlasColumns * ArtworkSize;
        PixelImage result = new(ArtworkSize, ArtworkSize);
        for (int row = 0; row < ArtworkSize; row++)
        {
            atlas.Pixels.AsSpan((top + row) * atlas.Width + left, ArtworkSize).CopyTo(result.Pixels.AsSpan(row * ArtworkSize, ArtworkSize));
        }
        return result;
    }
}
