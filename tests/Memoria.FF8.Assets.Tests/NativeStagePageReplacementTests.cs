using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeStagePageReplacementTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(32, 64)]
    public void OverlappingEightBitPagesKeepIndependentImagesAndPageLocalCoordinates(int timX, int firstU)
    {
        using InMemoryResourceSource source = new(new("fixture", GameDataEdition.Classic, ["native-battle-stage-page"]));
        ResourceService resources = new(source);
        BattleStage stage = Stage(resources, source.Descriptor, timX);
        BattleFace[] faces = stage.Meshes[0].Faces.ToArray();
        PixelImage original = stage.Textures[0];
        Assert.Empty(NativeBattleTextureReplacements.Bind(stage));
        resources.Prepare(new PageResolver()).Commit();
        IReadOnlyList<NativeBattleTextureReplacement> bound = NativeBattleTextureReplacements.Bind(stage);
        Assert.Equal(2, bound.Count);
        NativeBattleTextureReplacement first = bound.Single(value => value.Page == 0x90);
        NativeBattleTextureReplacement second = bound.Single(value => value.Page == 0x91);
        Assert.Equal(firstU, first.U);
        Assert.Equal(firstU, first.ImageSourceX);
        Assert.Equal(256 - firstU, first.Width);
        Assert.Equal(0, second.U);
        Assert.Equal(0, second.ImageSourceX);
        Assert.Equal(256, second.Width);
        foreach (NativeBattleTextureReplacement value in bound)
        {
            Assert.Equal("battle/a0stg001.x", value.SourceOwner);
            Assert.Equal(256, value.LogicalImageWidth);
            Assert.Equal(256, value.LogicalImageHeight);
            Assert.Equal(value.V, value.ImageSourceY);
            Assert.NotNull(value.Animation);
            PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(new(4, 4), NativeBattleEffectTextureState.FromColdProcess(), value.Page, value.Clut, value.U + 3, 5, null, bound, value.SourceOwner);
            Assert.Equal(8, selected.Width);
            Assert.Equal(new Rgba32((byte)(value.U + 3), 5, (byte)(value.Page & 31)), selected[0, 0]);
            Assert.Equal(new Rgba32((byte)(value.U + 6), 8, (byte)(value.Page & 31)), selected[7, 7]);
        }
        Assert.Same(original, stage.Textures[0]);
        Assert.Equal(faces, stage.Meshes[0].Faces);
        resources.Prepare(null).Commit();
        Assert.Empty(NativeBattleTextureReplacements.Bind(stage));
    }

    [Fact]
    public void SourceOwnerPreventsCrossStageReuseAndSurvivesNativePixelAndPaletteUpdates()
    {
        PixelImage image = new(256, 256, Enumerable.Repeat(new Rgba32(41, 52, 63), 256 * 256).ToArray());
        NativeBattleTextureReplacement sourcePage = new(0x90, 0, 0, 0, 4, 4, image, 2, new byte[16].ToImmutableArray(), new byte[512].ToImmutableArray())
        {
            SourceOwner = "battle/a0stg001.x",
            ImageCanvasWidth = 256,
            ImageCanvasHeight = 256
        };
        PixelImage original = new(4, 4);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        foreach (string? owner in new string?[] { null, "battle/a0stg002.x" })
        {
            Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, 0x90, 0, 0, 0, null, [sourcePage], owner));
        }
        NativeBattleEffectTextureState changed = state.Upload45BD30(0, 256, 1, 1, [1, 2]).Upload45BD30(0, 0, 1, 1, [3, 4]);
        PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, changed, 0x90, 1, 0, 0, null, [sourcePage], sourcePage.SourceOwner);
        Assert.Equal(image[0, 0], selected[0, 0]);
        NativeBattleTextureReplacement guarded = sourcePage with { SourceOwner = null, Image = new(4, 4), ImageCanvasWidth = 4, ImageCanvasHeight = 4 };
        Assert.NotSame(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, 0x90, 0, 0, 0, null, [guarded]));
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, changed, 0x90, 0, 0, 0, null, [guarded], sourcePage.SourceOwner));
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, 0x90, 1, 0, 0, null, [guarded], sourcePage.SourceOwner));
    }

    private static BattleStage Stage(ResourceService resources, ResourceSourceDescriptor source, int timX)
    {
        AssetHandle<TextureValue> atlas = resources.GetTexture(new(new(new("battle/a0stg001.x"), "texture/0"), source, 1, "classic-battle", 384, 256, () => new(384, 256)));
        Dictionary<(ushort Page, ushort Clut), AssetHandle<TextureValue>> pages = [];
        List<BattleFace> faces = [];
        foreach (ushort page in new ushort[] { 0x90, 0x91 })
        {
            pages.Add((page, 0), resources.GetTexture(new(new(new("battle/a0stg001.x"), $"page/{page & 31}"), source, 1, "native-battle-stage-page", 256, 256, () => new(256, 256))));
            faces.Add(new([0, 1, 2], [new(7, 9), new(11, 13), new(17, 19)], 0, new(128, 128, 128)) { NativeMaterial = new(page, 0, [new(21, 23), new(27, 29), new(31, 33)]) });
        }
        BattleMesh mesh = new([new(Vector3.Zero, 0), new(Vector3.UnitX, 0), new(Vector3.UnitY, 0)], faces);
        return new(1, [mesh], new ResourceTextureList([atlas]))
        {
            NativePages = pages,
            NativeSource = new(0, [], [], new("fixture:stage", Tim(timX).ToImmutableArray()))
        };
    }

    private static byte[] Tim(int x)
    {
        const int paletteLength = 524;
        const int imageOffset = 8 + paletteLength;
        byte[] bytes = new byte[imageOffset + 12 + 192 * 256 * 2];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 0x10);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 9);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), paletteLength);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(imageOffset), bytes.Length - imageOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(imageOffset + 4), (ushort)x);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(imageOffset + 6), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(imageOffset + 8), 192);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(imageOffset + 10), 256);
        return bytes;
    }

    private sealed class PageResolver : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (context.Layout != "native-battle-stage-page")
            {
                return null;
            }
            byte page = byte.Parse(context.Key.Subresource[5..], System.Globalization.CultureInfo.InvariantCulture);
            PixelImage image = new(512, 512);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    image[x, y] = new((byte)(x / 2), (byte)(y / 2), page);
                }
            }
            return new(image, "fixture:page", 256, 256) { Animation = new([image, new(512, 512)], 100) };
        }
    }
}
