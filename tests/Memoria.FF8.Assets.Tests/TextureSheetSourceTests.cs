using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class TextureSheetSourceTests
{
    [Fact]
    public void CompleteNativeRegionPadsIntoTheSharedLogicalCanvasAtItsExactHdDensity()
    {
        PixelImage source = new(6, 6, Enumerable.Range(0, 36).Select(index => new Rgba32((byte)index, 7, 9, 128)).ToArray());
        TextureSourceAddress sheet = new(new("field/model/second_chr/n000"), "row/0", 0, 4, 8, 2, 2, 128, 128);
        PixelImage projected = GameAssets.ProjectTextureSheetImage(source, 2, 2, sheet);
        Assert.Equal(384, projected.Width);
        Assert.Equal(384, projected.Height);
        Assert.Equal(source[5, 5], projected[17, 29]);
        Assert.Equal((byte)0, projected[0, 0].A);
        Assert.Equal((byte)128, source[0, 0].A);
        Assert.Throws<InvalidDataException>(() => GameAssets.ProjectTextureSheetImage(new(6, 4), 2, 2, sheet));
    }

    [Fact]
    public void EditedNativeBankProjectionKeepsBodyPaintAndSourceBuffersImmutable()
    {
        NativeFieldEyeDefinition eyes = Eyes();
        PixelImage image = new(16, 16, Enumerable.Repeat(new Rgba32(10, 20, 30, 255), 256).ToArray());
        image[15, 15] = new(77, 88, 99, 255);
        for (int y = 8; y < 12; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                image[x, y] = new(90, 0, 0, 128);
            }
        }
        PixelImage closed = FieldEyeMaterial.Project(eyes, image, "eyes-closed");
        Assert.Equal(new Rgba32(90, 0, 0, 128), closed[0, 0]);
        Assert.Equal(image[15, 15], closed[15, 15]);
        Assert.Equal(new Rgba32(10, 20, 30, 255), image[0, 0]);
        Assert.Throws<InvalidDataException>(() => FieldEyeMaterial.Project(eyes, new(14, 14), "eyes-closed"));
        Assert.Throws<NotSupportedException>(() => FieldEyeMaterial.Project(eyes, image, "unknown"));
    }

    [Fact]
    public void NativeRegisteredEyePhaseDistinguishesNeutralOpenAndClosedWithoutChangingCopies()
    {
        string hash = new('A', 64);
        byte[] bank = new byte[20];
        bank[2] = 8;
        bank[3] = 2;
        bank[4] = 1;
        bank[5] = 2;
        bank[12] = 2;
        bank[16] = 4;
        FieldModel model = new("d000", [], [], [], [new PixelImage(6, 1)], [])
        {
            NativeEyes = new(hash, 0, 0, bank, new(1, 3, 1, [3, 0, 2, 0, 1, 0])),
            NativeParts = new(hash, 0, [], [])
        };
        NativeFieldRegisteredResource resource = new(0, 0, 0, 0, 0, 0, hash, 0, [new(1, 32, 10, 3, 1, 0, 0, 0, 0)]);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(32, 10, 3, 1, model.NativeEyes.Texture!.Words);
        Assert.Null(GameAssets.ReadClassicFieldModelVariant(model, resource, textures, distinguishOpen: true));
        textures = textures.EnqueueCopy505EB0(33, 10, 1, 1, 32, 10).Drain505D20().State;
        Assert.Equal("eyes-closed", GameAssets.ReadClassicFieldModelVariant(model, resource, textures, distinguishOpen: true));
        textures = textures.EnqueueCopy505EB0(34, 10, 1, 1, 32, 10).Drain505D20().State;
        Assert.Equal("eyes-open", GameAssets.ReadClassicFieldModelVariant(model, resource, textures, distinguishOpen: true));
        Assert.Null(GameAssets.ReadClassicFieldModelVariant(model, resource, textures));
        Assert.Equal(new byte[] { 3, 0, 2, 0, 1, 0 }, model.NativeEyes.Texture.Words);
    }

    [Theory]
    [InlineData("MEMORIA_FF8_GAME_DIR")]
    [InlineData("MEMORIA_FF8_REMASTERED_GAME_DIR")]
    public void SharedMainCharacterSheetAndRegisteredVariantResolveWithoutSceneInitialization(string variable)
    {
        string? directory = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(Directory.Exists(directory), "An explicitly configured installed source is required.");
        using IResourceSource source = GameDataSourceFactory.OpenTextureAuthoring(directory!);
        using GameAssets assets = new(directory!, resources: new(source));
        CancellationToken token = TestContext.Current.CancellationToken;
        TextureTarget shared = new("ff8", "field/model/main_chr/d000", "row/0");
        TextureCatalogEntry entry = Assert.IsType<TextureCatalogEntry>(assets.FindCatalogTextureTarget(shared, token));
        TextureBuildContext context = assets.LoadCatalogTexture(entry);
        Assert.Equal("native-field-model-sheet", context.Layout);
        Assert.Equal(128, context.LogicalWidth);
        TextureValue original = assets.Resources.ResolveOriginalForExport(context);
        Assert.True(original.Image.Width >= 128);
        Assert.True(original.Variants?.ContainsKey("eyes-closed"));
        TextureSourceSnapshot snapshot = new(context, assets.Resources.ResolveForExport(context), 0) { InstalledOriginal = original };
        IReadOnlyList<TextureBuildContext> authoring = assets.LoadAuthoringTextures(snapshot);
        Assert.Contains(authoring, source => source.RequestedVariant == "eyes-closed");
        foreach (TextureBuildContext companion in authoring)
        {
            Assert.True(assets.Resources.ResolveForExport(companion).Image.Width >= 128);
        }
        ModelCatalogEntry model = assets.GetFieldModelSource("bghall_1", 0);
        TextureKey native = assets.LoadFieldModelSource(model).Textures is ResourceTextureList images ? images.TextureKeys[0] : throw new InvalidDataException("The installed model has no source texture key.");
        string registered = native.Subresource.Replace("/texture/", "/registered/texture/", StringComparison.Ordinal);
        TextureCatalogEntry registeredEntry = Assert.IsType<TextureCatalogEntry>(assets.FindCatalogTextureTarget(TextureTarget.FromKey(new(native.Owner, registered), "eyes-closed"), token));
        TextureValue closed = assets.Resources.ResolveOriginalForExport(assets.LoadCatalogTexture(registeredEntry));
        Assert.True(closed.Image.Width >= 128);
        Assert.NotNull(closed.Variants);
    }

    private static NativeFieldEyeDefinition Eyes()
    {
        byte[] bank = new byte[20];
        bank[2] = 8;
        bank[3] = 4;
        bank[4] = 2;
        bank[5] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(14), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(18), 2);
        return new("synthetic", 0, 0, bank, new(1, 4, 8, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray()));
    }
}
