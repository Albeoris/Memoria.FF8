using System.Buffers.Binary;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldEyeMaterialTests
{
    [Fact]
    public void ExplicitEyeCopiesChangeOnlyTheDestinationAndKeepSharedSourcesImmutable()
    {
        FieldModel model = Model();
        Rgba32[] originalPixels = model.Textures[0].Pixels.ToArray();
        byte[] originalWords = model.NativeEyes!.Texture!.Words.ToArray();
        PixelImage open = FieldEyeMaterial.Resolve(model, false);
        PixelImage closed = FieldEyeMaterial.Resolve(model, true);
        Assert.Same(open, FieldEyeMaterial.Resolve(model, false));
        Assert.Same(closed, FieldEyeMaterial.Resolve(model, true));
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                Assert.Equal(x < 4 && y < 2 ? model.Textures[0][x + 4, y + 2] : model.Textures[0][x, y], open[x, y]);
                Assert.Equal(x < 4 && y < 2 ? model.Textures[0][x, y + 4] : model.Textures[0][x, y], closed[x, y]);
            }
        }
        Assert.Equal(originalPixels, model.Textures[0].Pixels);
        Assert.Equal(originalWords, model.NativeEyes.Texture.Words);
        Ff7ActorVisual actor = new(0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Null(Ff7ModelAppearance.EyeMaterial(actor, model));
        Assert.Same(open, Ff7ModelAppearance.EyeMaterial(actor with { NativeEyeLeft = 1, NativeEyeRight = 1 }, model));
        Assert.Same(closed, Ff7ModelAppearance.EyeMaterial(actor with { NativeEyeLeft = 2, NativeEyeRight = 2 }, model));
        Assert.Throws<NotSupportedException>(() => Ff7ModelAppearance.EyeMaterial(actor with { NativeEyeLeft = 1, NativeEyeRight = 2 }, model));
    }

    [Fact]
    public void ReplacementVariantsFollowTheCurrentResourceGenerationWithoutClassicPixelPatching()
    {
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        FieldModel native = Model();
        AssetHandle<TextureValue> handle = resources.GetTexture(new(new(new("field/model"), "texture/0"), source.Descriptor, 1, "classic-field-model", 8, 8, () => native.Textures[0]));
        FieldModel model = native with { Textures = new ResourceTextureList([handle]) };
        resources.Prepare(new Planes(new(10, 20, 30, 255))).Commit();
        Assert.Same(handle.Value.Image, FieldEyeMaterial.Resolve(model, false));
        PixelImage first = FieldEyeMaterial.Resolve(model, true);
        Assert.All(first.Pixels, pixel => Assert.Equal(new Rgba32(10, 20, 30, 255), pixel));
        resources.Prepare(new Planes(new(40, 50, 60, 255))).Commit();
        PixelImage second = FieldEyeMaterial.Resolve(model, true);
        Assert.NotSame(first, second);
        Assert.All(second.Pixels, pixel => Assert.Equal(new Rgba32(40, 50, 60, 255), pixel));
        resources.Prepare(new Planes(new(70, 80, 90, 255), false)).Commit();
        Assert.Throws<NotSupportedException>(() => FieldEyeMaterial.Resolve(model, true));
        Assert.Equal(8, ResourceTextureList.LogicalSize(model.Textures, 0).X);
        Assert.Equal(16, handle.Value.Image.Width);
        Assert.Equal(native.Textures[0].Pixels, Model().Textures[0].Pixels);
    }

    [Fact]
    public void InstalledStandardFieldSquallUsesItsOwnWholeEyeBankWithoutChangingTheModel()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Set MEMORIA_FF8_GAME_DIR for the original field Squall eye bank.");
        using GameAssets assets = new(root!);
        FieldModel model = assets.LoadFieldModelSource(assets.GetFieldModelSource("bghall_1", 0));
        NativeEyeCopy copy = model.NativeEyes!.Copy(true)!;
        Rgba32[] original = model.Textures[0].Pixels.ToArray();
        byte[] words = model.NativeEyes.Texture!.Words.ToArray();
        PixelImage open = FieldEyeMaterial.Resolve(model, false);
        PixelImage closed = FieldEyeMaterial.Resolve(model, true);
        Assert.False(open.Pixels.SequenceEqual(closed.Pixels));
        for (int y = 0; y < closed.Height; y++)
        {
            for (int x = 0; x < closed.Width; x++)
            {
                bool destination = x >= copy.DestinationXWords * 2 && x < (copy.DestinationXWords + copy.WidthWords) * 2 && y >= copy.DestinationY && y < copy.DestinationY + copy.Height;
                Assert.Equal(destination ? model.Textures[0][copy.SourceXWords * 2 + x - copy.DestinationXWords * 2, copy.SourceY + y - copy.DestinationY] : model.Textures[0][x, y], closed[x, y]);
            }
        }
        Assert.Equal(original, model.Textures[0].Pixels);
        Assert.Equal(words, model.NativeEyes.Texture.Words);
    }

    private static FieldModel Model()
    {
        byte[] bank = new byte[20];
        bank[2] = 8;
        bank[3] = 4;
        bank[4] = 2;
        bank[5] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(14), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(18), 2);
        PixelImage image = new(8, 8, Enumerable.Range(0, 64).Select(value => new Rgba32((byte)value, 0, 0, 255)).ToArray());
        return new("synthetic", [], [], [], [image], []) { NativeEyes = new("synthetic", 0, 0, bank, new(1, 4, 8, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray())) };
    }

    private sealed class Planes(Rgba32 closedColor, bool includeClosed = true) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            PixelImage open = new(16, 16);
            PixelImage closed = new(16, 16, Enumerable.Repeat(closedColor, 256).ToArray());
            return new(open, "synthetic:eyes", context.LogicalWidth, context.LogicalHeight, Variants: includeClosed ? new Dictionary<string, PixelImage> { ["eyes-closed"] = closed } : null);
        }
    }
}
