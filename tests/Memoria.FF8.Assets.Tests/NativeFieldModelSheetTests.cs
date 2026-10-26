using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeFieldModelSheetTests
{
    [Fact]
    public void OriginalClassicModelsDoNotAdvertiseRemasteredMaterialAtlasCompatibility()
    {
        using ClassicGameDataSource source = new(Path.GetTempPath());
        Assert.DoesNotContain("native-field-model-sheet", source.Descriptor.Layouts);
    }

    [Theory]
    [InlineData(true, "main_chr")]
    [InlineData(false, "second_chr")]
    public void SourceOrdinalsAndAliasedNamesStayIndependentOfTimAllocation(bool main, string kind)
    {
        FieldModel model = Model() with { Name = "o099alias", TextureResourceName = "c021", MainCharacterTextures = main, Textures = [new(128, 96), new(128, 128)], TextureRegions = [new(640, 128, 8, 128, 96), new(704, 0, 8, 128, 128)] };
        TextureSourceAddress first = Address(model, 0)!;
        TextureSourceAddress second = Address(model, 1)!;
        Assert.Equal(new ResourceKey($"field/model/{kind}/c021"), first.Owner);
        Assert.Equal("row/0", first.Subresource);
        Assert.Equal("row/1", second.Subresource);
        Assert.Equal((0, 0, 128, 96, 128, 128), (first.X, first.Y, first.Width, first.Height, first.CanvasWidth, first.CanvasHeight));
        Assert.Null(Address(model with { TextureResourceName = null }, 0));
        Assert.Null(Address(model with { TextureSourceVariant = "unknown" }, 0));
        Assert.Null(Address(model with { TextureRegions = [new(0, 0, 8, 256, 128)] }, 0));
    }

    [Theory]
    [InlineData(0x36F, "alpha")]
    [InlineData(0x370, "alpha")]
    [InlineData(0x36E, null)]
    public void SourceAlternateMaterialRequiresItsOriginalFieldContext(int fieldId, string? expected)
    {
        Type type = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.NativeFieldModelTextureSheets")!;
        MethodInfo method = type.GetMethod("Variant")!;
        Assert.Equal(expected, method.Invoke(null, ["o006", fieldId]));
        Assert.Null(method.Invoke(null, ["o007", fieldId]));
        TextureSourceAddress address = Address(Model() with { TextureSourceVariant = expected }, 0)!;
        Assert.Equal(expected is null ? "row/0" : "row/0/alpha", address.Subresource);
    }

    private static TextureSourceAddress? Address(FieldModel model, int ordinal)
    {
        Type type = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.NativeFieldModelTextureSheets")!;
        return (TextureSourceAddress?)type.GetMethod("Address")!.Invoke(null, [model, ordinal]);
    }

    [Fact]
    public void MainLibraryMaterialTablesDecodeOriginalMaskedPointersWithoutGeometryOrAFieldPlacement()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(0x20000010u);
        writer.Write(0x100002B0u);
        writer.Write(uint.MaxValue);
        writer.Write(1360);
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            writer.Write(16);
            writer.Write(9);
            writer.Write(524);
            writer.Write((ushort)0);
            writer.Write((ushort)(224 + ordinal));
            writer.Write((ushort)256);
            writer.Write((ushort)1);
            for (int index = 0; index < 256; index++)
            {
                writer.Write((ushort)(index == 1 ? 31 : index == 2 ? 992 : 0));
            }
            writer.Write(140);
            writer.Write((ushort)640);
            writer.Write((ushort)(ordinal == 0 ? 128 : 0));
            writer.Write((ushort)64);
            writer.Write((ushort)1);
            writer.Write(Enumerable.Repeat((byte)(ordinal + 1), 128).ToArray());
        }
        Assert.Equal(1360, stream.Length);
        Type reader = typeof(GameAssets).Assembly.GetType("Memoria.FF8.Assets.FieldModelReader")!;
        FieldModel model = (FieldModel)reader.GetMethod("ReadMainCharacterMaterials", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, ["d026", stream.ToArray()])!;
        Assert.True(model.MainCharacterTextures);
        Assert.Equal("d026", model.TextureResourceName);
        Assert.Empty(model.Vertices);
        Assert.Empty(model.Animations);
        Assert.Equal(2, model.Textures.Count);
        Assert.Equal(Rgba32.FromPsxBgr555(31), model.Textures[0][0, 0]);
        Assert.Equal(Rgba32.FromPsxBgr555(992), model.Textures[1][0, 0]);
        Assert.Equal(new[] { 128, 0 }, model.TextureRegions.Select(region => region.Y));
        Assert.Equal(new[] { "row/0", "row/1" }, Enumerable.Range(0, 2).Select(ordinal => Address(model, ordinal)!.Subresource));
    }

    private static FieldModel Model() => new("d000", [], [], [], [new(16, 8)], [])
    {
        TextureResourceName = "d000",
        MainCharacterTextures = true,
        TextureRegions = [new(640, 128, 8, 16, 8)]
    };
}
