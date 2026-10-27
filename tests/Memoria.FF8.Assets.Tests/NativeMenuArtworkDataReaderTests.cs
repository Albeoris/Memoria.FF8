using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Tex;
using Albeoris.Games.FF8.NamedicBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMenuArtworkDataReaderTests
{
    [Fact]
    public void IconDirectoryChecksPartsAndAtlasBoundsBeforeDecoding()
    {
        byte[] bytes = Icons();
        NativeMenuArtworkDataReader.ValidateIconDirectory(bytes);
        bytes[^8] = 255;
        bytes[^4] = 2;
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateIconDirectory(bytes));
        bytes = Icons();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 4);
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateIconDirectory(bytes));
        bytes = Icons();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 2);
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateIconDirectory(bytes));
    }

    [Fact]
    public void FaceDirectoryChecksReachedPortraitCellsAndEveryRecordBoundary()
    {
        byte[] bytes = Faces();
        NativeMenuArtworkDataReader.ValidateFaceDirectory(bytes);
        bytes[^12] = 1;
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateFaceDirectory(bytes));
        bytes = Faces();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)bytes.Length);
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateFaceDirectory(bytes));
    }

    [Theory]
    [InlineData("menu/mc00.tex")]
    [InlineData("menu/mag09.tex")]
    public void ActiveAtlasCanvasIsRequired(string path)
    {
        NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey(path), Texture(256, 256));
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey(path), Texture(128, 128)));
    }

    [Fact]
    public void IconPalettesAndUnusedTextureCanvasesKeepTheirOwnContracts()
    {
        NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/icon.tex"), Texture(256, 256, 16));
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/icon.tex"), Texture(256, 256)));
        NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/start00.tex"), Texture(128, 64));
        NativeMenuArtworkDataReader.ValidateAreaNames(NamedicBin.WriteStrings(["First", "Second"], FF8Encoding.CreateEuropean()));
        Assert.Throws<InvalidDataException>(() => NativeMenuArtworkDataReader.ValidateAreaNames([1, 0, 8, 0]));
    }

    [Fact]
    public void MenuCardAndPointerLoadersUseTheSelectedEncodedResources()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] faces;
        using (Stream faceInput = source.Find(new ResourceKey("menu/face.sp2"))!.OpenRead())
        using (MemoryStream data = new())
        {
            faceInput.CopyTo(data);
            faces = data.ToArray();
        }
        faces.AsSpan(8, 4).CopyTo(faces.AsSpan(4, 4));
        resources.Prepare(null, encodedResources:
        [
            new EncodedResource(new ResourceKey("menu/icon.tex"), "tex", 1, "fixture", Texture(256, 256, 16)),
            new EncodedResource(new ResourceKey("menu/mc00.tex"), "tex", 1, "fixture", Texture(256, 256)),
            new EncodedResource(new ResourceKey("menu/face.sp2"), "sp2", 1, "fixture", faces)
        ]).Commit();
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(directory, resources);
        Assert.Equal(artwork.GetCharacterPortrait(1).Pixels, artwork.GetCharacterPortrait(0).Pixels);
        Assert.All(artwork.GetIcon(0).Pixels, pixel => Assert.Equal(new Albeoris.Games.FF8.Imaging.Rgba32(17, 34, 51, 255), pixel));
        Assert.Equal(artwork.GetIcon(0).Pixels, NativeMenuPointer.Read(directory, resources).Image.Pixels);
        Assert.All(CardArtwork.Load(directory, resources).GetFront(0).Pixels, pixel => Assert.Equal(new Albeoris.Games.FF8.Imaging.Rgba32(17, 34, 51, 255), pixel));
    }

    [Theory]
    [InlineData("menu-data-a")]
    [InlineData("menu-data-b")]
    [InlineData("menu-data-c")]
    public void OptionalSelectedArtworkPassesReachedNativeContracts(string fixture)
    {
        string catalog = TestResources.FixturePath(fixture);
        Dictionary<string, string> files = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog))!;
        byte[] Read(string name) => File.ReadAllBytes(Path.GetFullPath(files[name], Path.GetDirectoryName(catalog)!));
        NativeMenuArtworkDataReader.ValidateIconDirectory(Read("icon.sp1"));
        NativeMenuArtworkDataReader.ValidateFaceDirectory(Read("face.sp2"));
        NativeMenuArtworkDataReader.ValidateAreaNames(Read("areames.dc1"));
        foreach ((string name, string path) in files.Where(pair => pair.Key.EndsWith(".TEX", StringComparison.OrdinalIgnoreCase)))
        {
            string logicalName = name.StartsWith("hires_", StringComparison.Ordinal) ? name[6..] : name;
            NativeMenuArtworkDataReader.ValidateTexture(new ResourceKey("menu/" + logicalName), File.ReadAllBytes(path));
        }
    }

    private static byte[] Icons()
    {
        const int count = 144;
        const int offset = 4 + count * 4;
        byte[] bytes = new byte[offset + 8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, count);
        for (int id = 0; id < count; id++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + id * 4), offset);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6 + id * 4), 1);
        }
        bytes[offset + 4] = 1;
        bytes[offset + 6] = 1;
        return bytes;
    }

    private static byte[] Faces()
    {
        const int count = 48;
        const int offset = 4 + count * 4;
        byte[] bytes = new byte[offset + 16];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, count);
        for (int id = 0; id < count; id++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + id * 4), offset);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 8), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 10), 48);
        return bytes;
    }

    private static byte[] Texture(int width, int height, int palettes = 1)
    {
        TexHeader header = new()
        {
            Width = width,
            Height = height,
            Pitch = width,
            BitDepth = 8,
            BitsPerPixel = 8,
            BytesPerPixel = 1,
            BitsPerIndex = 8,
            PaletteFlags = TexPaletteFlags.HasPalette,
            PaletteCount = palettes,
            ColorsPerPalette = 256,
            RepeatedColorsPerPalette = 256,
            PaletteSize = palettes * 256
        };
        return new TexFile(header, Enumerable.Range(0, palettes).Select(_ => Enumerable.Repeat(0xFF112233u, 256).ToArray()), new byte[width * height]).Write();
    }
}
