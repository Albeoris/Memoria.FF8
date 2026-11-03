using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging.Tim;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class RemasteredWorldStaticSheetTests
{
    [Theory]
    [InlineData("bcport_2")]
    [InlineData("dosea_2")]
    [InlineData("dotown_1")]
    public void NativeFieldAndUploadSelectTheFullBoatSheet(string fieldName)
    {
        string directory = RemasteredDirectory();
        using WorldAssets assets = new(directory, nativeSourceFieldName: fieldName);
        WorldStaticModel model = assets.ReadScriptedStaticModel(78);
        Assert.Equal(217, model.NativeVertices.Count);
        Assert.Equal(253, model.Faces.Count);
        Assert.All(model.Textures, image => Assert.Equal((768, 768), (image.Width, image.Height)));
        Assert.Equal(new System.Numerics.Vector2(256, 256), ResourceTextureList.LogicalSize(model.Textures, 0));
        Assert.Contains(model.Faces.SelectMany(face => face.TextureCoordinates), uv => uv.Y >= 128);
        Assert.All(model.Faces.SelectMany(face => face.TextureCoordinates), uv =>
        {
            Assert.InRange(uv.X, 0, 255);
            Assert.InRange(uv.Y, 0, 255);
        });
    }

    [Theory]
    [InlineData(null, 15, false)]
    [InlineData("bgroom_1", 15, false)]
    [InlineData("bcport_2", 14, false)]
    [InlineData("bcport_2", 15, true)]
    public void UnknownFieldPageOrUploadDoesNotAcquireTheNativeSheet(string? fieldName, int page, bool changeUpload)
    {
        using RemasteredGameDataSource source = new(RemasteredDirectory());
        ResourceService resources = new(source);
        byte[] bytes = resources.Read(NativeResourceNames.WorldSettings(resources.ResourceLanguage));
        int table = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(41 * 4));
        int start = table + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(table + 14 * 4));
        int image = start + 8;
        image += BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(image));
        int end = image + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(image));
        if (changeUpload)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(image + 4), 991);
        }
        TimImage tim = Assert.Single(TimFile.Read(bytes.AsSpan(start, end - start)).Images);
        Assert.Null(source.ReadWorldStaticTextureSheet(fieldName, tim, (ushort)page));
    }

    private static string RemasteredDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(Path.Combine(directory!, "main.zzz")), "Select optional Remastered resources with MEMORIA_FF8_REMASTERED_GAME_DIR.");
        return directory!;
    }
}
