using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Albeoris.Games.FF8.FlArchives;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods.Tests;

public sealed class TonberryTests
{
    private static readonly ResourceSourceDescriptor Classic = new("memory", GameDataEdition.Classic, ["classic-world-texl", "tonberry-classic-page"]);
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP4////fwAJ+wP9KobjigAAAABJRU5ErkJggg==");

    [Fact]
    public void CircularComparisonsPreserveAllNinetyEightBitsAndIgnoreAlpha()
    {
        PixelImage image = new(256, 256);
        TonberrySignatures empty = TonberrySignatures.Calculate(image);
        Assert.Equal(ulong.MaxValue, empty.Primary);
        Assert.Equal((BigInteger.One << 98) - 1, empty.Secondary);
        Assert.Equal(uint.MaxValue, empty.ObjectTop);
        Assert.Equal(uint.MaxValue, empty.ObjectBottom);
        image[0, 0] = new Rgba32(255, 255, 255, 0);
        TonberrySignatures colored = TonberrySignatures.Calculate(image);
        Assert.NotEqual(empty.Primary, colored.Primary);
        image[0, 0] = new Rgba32(255, 255, 255, 255);
        Assert.Equal(colored, TonberrySignatures.Calculate(image));
        Assert.Equal("04a9ac6f6cb87490d312705161079a55e347a637", TonberrySignatures.ReferenceRevision);
    }

    [Fact]
    public void RowsPreserveFirstWinnerPersistenceAndIndependentTablePrecedence()
    {
        TonberrySignatures signature = TonberrySignatures.Calculate(new PixelImage(256, 256));
        ModCatalogSnapshot catalog = Catalog(
            Table("tonberry/hashmap/a.csv", $"\uFEFF*disabled_13,{signature.Primary}\r\n!first_13,{signature.Primary}\r\nsecond_13,{signature.Primary}\r\nbroken\r\n"),
            Table("tonberry/hashmap/disabled/x.csv", $"bad_13,{signature.Primary}"),
            Table("tonberry/collisions.csv", $"collision_13,{signature.Primary}"),
            Table("tonberry/hash2map.csv", $"secondary_13,{signature.Secondary}"),
            Table("tonberry/objmap/a.csv", $"object_13,{signature.ObjectTop}"));
        TonberryTables tables = new(catalog);
        TonberryRule rule = Assert.IsType<TonberryRule>(tables.Match(signature));
        Assert.Equal("first_13", rule.Name);
        Assert.True(rule.Persistent);
        Assert.Contains(tables.Diagnostics, diagnostic => diagnostic.Code == "Tonberry.MalformedRow");
        Assert.Contains(tables.Diagnostics, diagnostic => diagnostic.Code == "Tonberry.DuplicateKey");
        TonberryTables collision = new(Catalog(catalog.Files.Skip(2).ToArray()));
        Assert.Equal("secondary_13", collision.Match(signature)?.Name);
        TonberryTables unresolved = new(Catalog(Table("collisions.csv", $"collision_13,{signature.Primary}"), Table("objmap.csv", $"object_13,{signature.ObjectTop}")));
        Assert.Null(unresolved.Match(signature));
    }

    [Fact]
    public void MissingWinningPngDoesNotChooseLaterHashRow()
    {
        TonberrySignatures signature = TonberrySignatures.Calculate(new PixelImage(256, 256));
        TonberryTables tables = new(Catalog(Table("hash1map.csv", $"missing_13,{signature.Primary}\nexists_13,{signature.Primary}"), Image("textures/ex/exists/exists_13.png")));
        Assert.False(tables.HasUsableReplacement);
        Assert.Equal("missing_13", tables.Match(signature)?.Name);
    }

    [Fact]
    public void RemasteredAndEmptyCatalogsDoNotReadTablesOrOriginalPixels()
    {
        bool read = false;
        ModFile table = new("Tonberry/test", ModFormat.Tonberry, "hash1map.csv", 1, () =>
        {
            read = true;
            throw new InvalidOperationException();
        });
        TonberryRecognitionCache cache = new();
        Assert.Null(TonberryTextureResolver.Create(Classic with { Edition = GameDataEdition.Remastered }, Catalog(table), out _, cache));
        Assert.Null(TonberryTextureResolver.Create(Classic, Catalog(), out _, cache));
        Assert.False(read);
        Assert.Equal(0, cache.CalculationCount);
    }

    [Fact]
    public void CatalogChangesReuseSourceSignaturesAndUnsupportedLayoutsDoNotRecognize()
    {
        ulong hash = ulong.MaxValue;
        TonberryRecognitionCache cache = new();
        ModFile[] files = [Table("hash1map.csv", $"exists_13,{hash}"), Image("textures/ex/exists/exists_13.png")];
        TonberryTextureResolver resolver = Assert.IsType<TonberryTextureResolver>(TonberryTextureResolver.Create(Classic, Catalog(files), out _, cache));
        TextureBuildContext context = new(new TextureKey(new ResourceKey("world/texl.obj"), "texl/0"), Classic, 1, "tonberry-classic-page", 256, 256, () => new PixelImage(256, 256));
        Assert.NotNull(resolver.Resolve(context));
        Assert.NotNull(resolver.Resolve(context));
        Assert.Equal(1, cache.CalculationCount);
        TonberryTextureResolver reloaded = Assert.IsType<TonberryTextureResolver>(TonberryTextureResolver.Create(Classic, new ModCatalogSnapshot(2, [], files), out _, cache));
        Assert.NotNull(reloaded.Resolve(context));
        Assert.Equal(1, cache.CalculationCount);
        TextureBuildContext unsupported = new(context.Key, Classic, 2, "classic-field-tile", 16, 16, () => throw new InvalidOperationException("Unsupported layout must stay lazy."));
        Assert.Null(reloaded.Resolve(unsupported));
        Assert.Equal(1, cache.CalculationCount);
    }

    [Fact]
    public void InstalledUnchangedHorizonPackMatchesNativeFloorExpandedClassicTexl()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        string package = Path.Combine(directory, "Mods", "Tonberry", "HorizonPackRemaster_v1-0");
        Assert.SkipUnless(Directory.Exists(package), "Installed HorizonPack corpus is required.");
        ModFile[] files = Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path => new ModFile("Tonberry/HorizonPackRemaster_v1-0", ModFormat.Tonberry, Path.GetRelativePath(package, path), 1, () => File.ReadAllBytes(path))).ToArray();
        ModCatalogSnapshot catalog = Catalog(files);
        TonberryTables tables = new(catalog);
        TonberryTextureResolver resolver = Assert.IsType<TonberryTextureResolver>(TonberryTextureResolver.Create(Classic, catalog, out _));
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(directory, "Data", "lang-en", "world.fs"), FlArchiveRepresentation.Files);
        using Stream source = archive.Entries.Single(entry => entry.RelativePath.EndsWith("texl.obj", StringComparison.OrdinalIgnoreCase)).OpenForRead();
        using MemoryStream data = new();
        source.CopyTo(data);
        byte[] bytes = data.ToArray();
        int matches = 0;
        int blanks = 0;
        HashSet<int> matchedPages = [];
        for (int page = 0; page < bytes.Length / 0x12800; page++)
        {
            int offset = page * 0x12800;
            int position = offset + 8;
            if ((BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4)) & 8) != 0)
            {
                position += BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position));
            }
            int end = position + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position));
            TimImage tim = TimFile.Read(bytes.AsSpan(offset, end - offset)).Images.Single();
            for (int palette = 0; palette < tim.Palettes.Count; palette++)
            {
                PixelImage original = tim.Decode(palette);
                PixelImage native = NativePaletteImage(original);
                TonberrySignatures signature = TonberrySignatures.Calculate(native);
                TonberryRule? match = tables.Match(signature);
                if (signature.Primary == ulong.MaxValue)
                {
                    blanks++;
                }
                if (match is null)
                {
                    continue;
                }
                matches++;
                matchedPages.Add(page);
                Assert.NotNull(tables.FindImage(match.Name));
                if (page == 0 && palette == 0)
                {
                    Assert.Equal(15408413730505935451UL, signature.Primary);
                    Assert.Equal("hp_texpg_19", match.Name);
                    TextureBuildContext context = new(new TextureKey(new ResourceKey("world/texl.obj"), "texl/0", palette), Classic, 1, "classic-world-texl", 256, 256, () => original, new TextureSourceRegion(BitsPerPixel: 8));
                    TextureReplacement replacement = Assert.IsType<TextureReplacement>(resolver.Resolve(context));
                    Assert.Equal(1024, replacement.Image.Width);
                    Assert.Contains("hp_texpg_19.png", replacement.Provenance);
                }
            }
        }
        // These are all palette rows, including unused and empty variants; a no-match keeps native content.
        Assert.Equal(259, matches);
        Assert.Equal(18, matchedPages.Count);
        Assert.Equal(37, blanks);
    }

    private static PixelImage NativePaletteImage(PixelImage original)
    {
        PixelImage image = new(original.Width, original.Height);
        for (int i = 0; i < original.Pixels.Length; i++)
        {
            Rgba32 color = original.Pixels[i];
            image.Pixels[i] = color.A == 128 && color.R == 0 && color.G == 0 && color.B == 0 ? new Rgba32(1, 1, 1) : new Rgba32((byte)((color.R >> 3) * 255 / 31), (byte)((color.G >> 3) * 255 / 31), (byte)((color.B >> 3) * 255 / 31), color.A);
        }
        return image;
    }

    [Fact]
    public void InstalledRoadRegionsMatchBeforePackingAndReturnOnlyTheirSourceRectangle()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII";
        string package = Path.Combine(directory, "Mods", "Tonberry", "HorizonPackRemaster_v1-0");
        Assert.SkipUnless(Directory.Exists(package), "Installed HorizonPack corpus is required.");
        ModFile[] files = Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path => new ModFile("Tonberry/HorizonPackRemaster_v1-0", ModFormat.Tonberry, Path.GetRelativePath(package, path), 1, () => File.ReadAllBytes(path))).ToArray();
        TonberryRecognitionCache cache = new();
        TonberryTextureResolver resolver = Assert.IsType<TonberryTextureResolver>(TonberryTextureResolver.Create(Classic, Catalog(files), out _, cache));
        using IFlArchive archive = FlArchive.OpenForRead(Path.Combine(directory, "Data", "lang-en", "world.fs"), FlArchiveRepresentation.Files);
        IFlArchiveEntry entry = archive.Entries.Single(entry => entry.RelativePath.EndsWith("wmsetus.obj", StringComparison.OrdinalIgnoreCase));
        using Stream source = entry.OpenForRead();
        using MemoryStream data = new();
        source.CopyTo(data);
        byte[] bytes = data.ToArray();
        ResourceKey owner = new("world/wmsetus.obj");
        ResourceContent content = new(owner, "wmset", 1, "memory source", () => new MemoryStream(bytes, false));
        int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(38 * 4));
        PixelImage replacementPage = ModImageDecoder.Decode(files.Single(file => file.RelativePath.Replace('\\', '/').EndsWith("/hp_texpg_39.png", StringComparison.OrdinalIgnoreCase))).Image;
        int matched = 0;
        for (int index = 0; index < 13; index++)
        {
            int offset = section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + index * 4));
            int position = offset + 8;
            if ((BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4)) & 8) != 0)
            {
                position += BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position));
            }
            int end = position + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(position));
            TimImage tim = TimFile.Read(bytes.AsSpan(offset, end - offset)).Images.Single();
            TextureBuildContext context = new(new TextureKey(owner, $"roads/{index}"), Classic, 1, "classic-world-road-region", tim.PixelWidth, tim.Height, () => throw new InvalidOperationException("Recognition must use original encoded source, before decoded atlas packing."), new TextureSourceRegion(tim.X, tim.Y, 4), encodedSource: content);
            TextureReplacement? replacement = resolver.Resolve(context);
            if (index == 7)
            {
                Assert.Null(replacement);
                Assert.Null(resolver.Resolve(context));
                continue;
            }
            matched++;
            Assert.NotNull(replacement);
            Assert.Contains("hp_texpg_39.png", replacement.Provenance);
            Assert.Equal(tim.PixelWidth, replacement.LogicalWidth);
            Assert.Equal(tim.Height, replacement.LogicalHeight);
            Assert.Equal(tim.PixelWidth * 4, replacement.Image.Width);
            Assert.Equal(tim.Height * 4, replacement.Image.Height);
            Assert.Equal(replacementPage[tim.X % 64 * 4 * 4, tim.Y % 256 * 4], replacement.Image[0, 0]);
            Assert.NotNull(resolver.Resolve(context));
        }
        Assert.Equal(12, matched);
        Assert.Equal(13, cache.CalculationCount);
    }

    private static ModCatalogSnapshot Catalog(params ModFile[] files) => new(1, [], files);
    private static ModFile Table(string path, string text) => new("Tonberry/test", ModFormat.Tonberry, path, 1, () => Encoding.UTF8.GetBytes(text));
    private static ModFile Image(string path) => new("Tonberry/test", ModFormat.Tonberry, path, 1, () => Png);
}
