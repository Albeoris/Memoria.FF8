using System.Collections.Immutable;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTextureReplacementTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void OriginalModelWeaponAndStageMaterialsFollowSharedReplacementPublication(int scale)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(directory), "Configure optional original game resources.");
        using IResourceSource source = GameDataSourceFactory.Open(directory!);
        ResourceService resources = new(source);
        using BattleAssets assets = new(directory!, resources);
        BattleModel model = assets.LoadPlayer(0);
        BattleModel weapon = assets.LoadWeapon(0, 0, model);
        BattleStage stage = assets.LoadStage(0);
        NativeBattleModelTextureLoad bodyLoad = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 0);
        NativeBattleModelTextureLoad weaponLoad = NativeBattleModelTextures.Prepare507400(weapon.NativeWeaponTexturePack!, bodyLoad.AllocatedSlots);
        NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), bodyLoad), weaponLoad).EnqueueTim505E30(stage.NativeSource!.Texture).State.Drain505D20().State;
        Assert.Empty(NativeBattleTextureReplacements.Bind(model, bodyLoad.Binding));
        Assert.Empty(NativeBattleTextureReplacements.Bind(weapon, weaponLoad.Binding, true));
        Assert.Empty(NativeBattleTextureReplacements.Bind(stage));
        resources.Prepare(new SolidResolver(scale)).Commit();
        IReadOnlyList<NativeBattleTextureReplacement>[] groups = [NativeBattleTextureReplacements.Bind(model, bodyLoad.Binding), NativeBattleTextureReplacements.Bind(weapon, weaponLoad.Binding, true), NativeBattleTextureReplacements.Bind(stage)];
        foreach (IReadOnlyList<NativeBattleTextureReplacement> replacements in groups)
        {
            Assert.NotEmpty(replacements);
            foreach (NativeBattleTextureReplacement replacement in replacements)
            {
                PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, replacement.Page, replacement.Clut, replacement.U, replacement.V, replacement.Width, replacement.Height);
                PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, replacement.Page, replacement.Clut, replacement.U, replacement.V, null, replacements);
                Assert.Equal(scale * replacement.Width, selected.Width);
                Assert.Equal(scale * replacement.Height, selected.Height);
                Assert.Equal(new Rgba32(23, 45, 67, 127), selected[0, 0]);
            }
        }
        resources.Prepare(null).Commit();
        Assert.Empty(NativeBattleTextureReplacements.Bind(model, bodyLoad.Binding));
        Assert.Empty(NativeBattleTextureReplacements.Bind(weapon, weaponLoad.Binding, true));
        Assert.Empty(NativeBattleTextureReplacements.Bind(stage));
    }

    [Fact]
    public void MixedPixelDensitiesKeepNativePlacementAndAlphaWithinNativeWindow()
    {
        const ushort page = 0x80;
        PixelImage first = new(8, 8, Enumerable.Repeat(new Rgba32(11, 22, 33, 0), 64).ToArray());
        PixelImage second = new(12, 12, Enumerable.Repeat(new Rgba32(44, 55, 66, 127), 144).ToArray());
        second[1, 0] = new(77, 88, 99, 255);
        NativeBattleTextureReplacement a = new(page, 0, 0, 0, 4, 4, first, 2, new byte[16].ToImmutableArray(), new byte[512].ToImmutableArray());
        NativeBattleTextureReplacement b = new(page, 0, 4, 0, 4, 4, second, 2, new byte[16].ToImmutableArray(), new byte[512].ToImmutableArray());
        PixelImage original = new(8, 4);
        PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, NativeBattleEffectTextureState.FromColdProcess(), page, 0, 0, 0, null, [a, b]);
        Assert.Equal(24, selected.Width);
        Assert.Equal(12, selected.Height);
        Assert.Equal(first[0, 0], selected[0, 0]);
        Assert.Equal(second[0, 0], selected[12, 0]);
        Assert.Equal(second[1, 0], selected[13, 0]);
    }

    [Theory]
    [InlineData(0, 16, 8)]
    [InlineData(0x100, 0, 32)]
    public void NativeFourBitAndDirectMaterialsRetainTheirOriginalWordOwnership(ushort page, int paletteColors, int wordBytes)
    {
        PixelImage image = new(8, 8, Enumerable.Repeat(new Rgba32(10, 20, 30, 127), 64).ToArray());
        NativeBattleTextureReplacement replacement = new(page, 0, 0, 0, 4, 4, image, wordBytes / 8, new byte[wordBytes].ToImmutableArray(), new byte[paletteColors * 2].ToImmutableArray());
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        PixelImage original = new(4, 4);
        PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, page, 0, 0, 0, null, [replacement]);
        Assert.Equal(image.Pixels, selected.Pixels);
        NativeBattleEffectTextureState changed = state.Upload45BD30(0, 0, 1, 1, [1, 0]);
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, changed, page, 0, 0, 0, null, [replacement]));
    }

    [Fact]
    public void FractionalStageAtlasRegionsRetainTheirFullSourceCanvasCoordinates()
    {
        PixelImage image = new(14, 11);
        for (int row = 0; row < image.Height; row++)
        {
            for (int column = 0; column < image.Width; column++)
            {
                image[column, row] = new((byte)column, (byte)row, 91);
            }
        }
        NativeBattleTextureReplacement replacement = new(0x80, 0, 0, 0, 4, 4, image, 2, new byte[16].ToImmutableArray(), new byte[512].ToImmutableArray()) { ImageSourceX = 4, ImageCanvasWidth = 8, ImageCanvasHeight = 4 };
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(new(4, 4), state, 0x80, 0, 0, 0, null, [replacement]);
        Assert.Equal(7, selected.Width);
        Assert.Equal(11, selected.Height);
        for (int row = 0; row < selected.Height; row++)
        {
            for (int column = 0; column < selected.Width; column++)
            {
                Assert.Equal(image[column + 7, row], selected[column, row]);
            }
        }
        PixelImage cropped = NativeBattleEffectTextureReader.ApplyModelReplacements(new(2, 2), state, 0x80, 0, 1, 1, null, [replacement]);
        Assert.Equal(4, cropped.Width);
        Assert.Equal(6, cropped.Height);
        Assert.Equal(image[8, 2], cropped[0, 0]);
        Assert.Equal(image[11, 7], cropped[3, 5]);
    }

    [Theory]
    [InlineData(7, 11)]
    [InlineData(8, 13)]
    [InlineData(2, 2)]
    public void ArbitraryImageDimensionsMapIndependentlyThroughUnchangedLogicalCanvas(int width, int height)
    {
        PixelImage image = new(width, height);
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                image[column, row] = new((byte)column, (byte)row, 91, (byte)(column % 2 == 0 ? 127 : 255));
            }
        }
        NativeBattleTextureReplacement replacement = new(0x80, 0, 0, 0, 4, 4, image, 2, new byte[16].ToImmutableArray(), new byte[512].ToImmutableArray());
        PixelImage original = new(4, 4);
        NativeBattleEffectTextureState state = NativeBattleEffectTextureState.FromColdProcess();
        PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, 0x80, 0, 0, 0, null, [replacement]);
        Assert.Equal(Math.Max(4, width), selected.Width);
        Assert.Equal(Math.Max(4, height), selected.Height);
        for (int row = 0; row < selected.Height; row++)
        {
            for (int column = 0; column < selected.Width; column++)
            {
                Assert.Equal(image[column * width / selected.Width, row * height / selected.Height], selected[column, row]);
            }
        }
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, state.Upload45BD30(0, 0, 1, 1, [1, 0]), 0x80, 0, 0, 0, null, [replacement]));
        Assert.Same(original, NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, 0x80, 0, 0, 0, null, []));
    }

    private sealed class SolidResolver(int scale) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            if (context.Layout != "classic-battle")
            {
                return null;
            }
            int width = context.LogicalWidth * scale;
            int height = context.LogicalHeight * scale;
            return new(new(width, height, Enumerable.Repeat(new Rgba32(23, 45, 67, 127), width * height).ToArray()), "fixture:material", context.LogicalWidth, context.LogicalHeight);
        }
    }
}
