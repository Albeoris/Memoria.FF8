using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEyePlaneReplacementTests
{
    private static readonly Rgba32 OpenColor = new(91, 32, 17, 255);
    private static readonly Rgba32 ClosedColor = new(14, 73, 29, 255);

    [Fact]
    public void SelectedOriginalBodyCatalogMapsClosedPlanesThroughItsActualNativeCopyTargets()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? catalog = Environment.GetEnvironmentVariable("MEMORIA_FF8_BATTLE_EYE_MODEL_CATALOG");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(catalog), "Select original resources and an optional body model JSON array with MEMORIA_FF8_GAME_DIR and MEMORIA_FF8_BATTLE_EYE_MODEL_CATALOG.");
        string[] names = JsonSerializer.Deserialize<string[]>(File.ReadAllText(catalog!)) ?? throw new InvalidDataException("The body model catalog must be a JSON string array.");
        Assert.NotEmpty(names);
        using IResourceSource source = GameDataSourceFactory.Open(directory!);
        ResourceService resources = new(source);
        using BattleAssets assets = new(directory!, resources);
        foreach (string name in names)
        {
            Assert.Matches(@"\Ad[0-9a-f]c[0-9]{3}\.dat\z", name);
            BattleModel model = assets.LoadModel(name);
            NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 0);
            NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
            NativeBattleTextureCopy open = NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 0) ?? throw new InvalidDataException($"Body {name} has no native open copy.");
            NativeBattleTextureCopy closed = NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 1) ?? throw new InvalidDataException($"Body {name} has no native closed copy.");
            resources.Prepare(new Planes(false)).Commit();
            IReadOnlyList<NativeBattleTextureReplacement> baseline = NativeBattleTextureReplacements.Bind(model, load.Binding);
            Assert.NotEmpty(baseline);
            resources.Prepare(new Planes()).Commit();
            IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
            Assert.True(replacements.Count > baseline.Count, $"Body {name} has no proved closed-plane copy candidate.");
            state = Copy(state, closed);
            int changed = 0;
            foreach (NativeBattleTextureReplacement material in baseline)
            {
                PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, material.Page, material.Clut, material.U, material.V, material.Width, material.Height);
                PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, material.Page, material.Clut, material.U, material.V, null, replacements);
                int closedPixels = 0;
                for (int y = 0; y < selected.Height; y++)
                {
                    for (int x = 0; x < selected.Width; x++)
                    {
                        if (selected[x, y] != ClosedColor)
                        {
                            continue;
                        }
                        changed++;
                        closedPixels++;
                    }
                }
                Assert.True(closedPixels == 0 || closedPixels == selected.Width * selected.Height, $"Body {name} selected a partial HD plane.");
            }
            Assert.True(changed > 0, $"Body {name} never samples its proved closed-plane destination.");
            state = Copy(state, open);
            foreach (NativeBattleTextureReplacement material in baseline)
            {
                PixelImage original = NativeBattleEffectTextureReader.DecodePcRegion(state, material.Page, material.Clut, material.U, material.V, material.Width, material.Height);
                PixelImage selected = NativeBattleEffectTextureReader.ApplyModelReplacements(original, state, material.Page, material.Clut, material.U, material.V, null, replacements);
                Assert.All(selected.Pixels, color => Assert.Equal(OpenColor, color));
            }
        }

        static NativeBattleEffectTextureState Copy(NativeBattleEffectTextureState state, NativeBattleTextureCopy copy) => state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20().State;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ActualCopiedWordsSelectTheWholeClosedPlaneOnEitherMaterial(int target)
    {
        using InMemoryResourceSource source = new(new("synthetic", GameDataEdition.Classic, []));
        ResourceService resources = new(source);
        AssetHandle<TextureValue>[] handles = Enumerable.Range(0, 2).Select(index => resources.GetTexture(new(new(new("battle/d0c000.dat"), $"texture/{index}"), source.Descriptor, 1, "classic-battle", 8, 8, () => new(8, 8)))).ToArray();
        BattleModel model = Model(new ResourceTextureList(handles), target);
        NativeBattleModelTextureLoad load = NativeBattleModelTextures.Prepare507400(model.NativeBodyTexturePack!, 15);
        NativeBattleEffectTextureState state = NativeBattleModelTextures.Enqueue507400(NativeBattleEffectTextureState.FromColdProcess(), load).Drain505D20().State;
        resources.Prepare(new Planes()).Commit();
        IReadOnlyList<NativeBattleTextureReplacement> replacements = NativeBattleTextureReplacements.Bind(model, load.Binding);
        Assert.Equal(3, replacements.Count);
        PixelImage closedPlane = replacements.Single(replacement => replacement.Image.Pixels.Contains(ClosedColor)).Image;
        Assert.Same(closedPlane, NativeBattleTextureReplacements.Bind(model, load.Binding).Single(replacement => replacement.Image.Pixels.Contains(ClosedColor)).Image);
        NativeBattleModelTextureBinding independentBinding = new(load.Binding.PaletteRows);
        Assert.Same(closedPlane, NativeBattleTextureReplacements.Bind(model, independentBinding).Single(replacement => replacement.Image.Pixels.Contains(ClosedColor)).Image);
        BattleModel independentModel = model with { Name = "d1c001.dat" };
        Assert.Same(closedPlane, NativeBattleTextureReplacements.Bind(independentModel, load.Binding).Single(replacement => replacement.Image.Pixels.Contains(ClosedColor)).Image);
        foreach (int descriptorDimension in new[] { 5, 6 })
        {
            byte[] motion = model.NativeTextureAnimations!.Value.ToArray();
            motion[descriptorDimension] = 0;
            BattleModel emptyCopy = model with { NativeTextureAnimations = motion.ToImmutableArray() };
            Assert.Equal(2, NativeBattleTextureReplacements.Bind(emptyCopy, load.Binding).Count);
        }
        NativeBattleTextureReplacement baseline = replacements.First(replacement => replacement.Clut >> 6 == 228 + target);
        Assert.All(Sample(state).Pixels, color => Assert.Equal(OpenColor, color));
        NativeBattleTextureCopy copy = NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 1)!;
        state = state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20().State;
        PixelImage selected = Sample(state);
        Assert.Equal((16, 16), (selected.Width, selected.Height));
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(ClosedColor, selected[x, y]);
            }
        }
        int pageX = (baseline.Page & 15) * 64;
        int pageY = (baseline.Page & 16) * 16;
        NativeBattleEffectTextureState unrelated = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(state)).Upload45BD30((short)pageX, (short)(pageY + baseline.V), 1, 1, [9, 9]);
        PixelImage native = Decode(unrelated);
        Assert.Same(native, NativeBattleEffectTextureReader.ApplyModelReplacements(native, unrelated, baseline.Page, baseline.Clut, baseline.U, baseline.V, null, replacements));
        int paletteX = (baseline.Clut & 63) * 16;
        int paletteY = baseline.Clut >> 6;
        NativeBattleEffectTextureState changedPalette = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(state)).Upload45BD30((short)paletteX, (short)paletteY, 1, 1, [9, 9]);
        native = Decode(changedPalette);
        Assert.Same(native, NativeBattleEffectTextureReader.ApplyModelReplacements(native, changedPalette, baseline.Page, baseline.Clut, baseline.U, baseline.V, null, replacements));
        copy = NativeBattleTextureMotion.Plan50C780(model.NativeTextureAnimations, load.AllocatedSlots, 0, 0)!;
        state = state.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY).Drain505D20().State;
        Assert.All(Sample(state).Pixels, color => Assert.Equal(OpenColor, color));
        Rgba32 changedClosedColor = new(113, 47, 81, 255);
        resources.Prepare(new Planes(closedColor: changedClosedColor)).Commit();
        PixelImage republished = NativeBattleTextureReplacements.Bind(model, load.Binding).Single(replacement => replacement.Image.Pixels.Contains(changedClosedColor)).Image;
        Assert.NotSame(closedPlane, republished);
        Assert.Equal(changedClosedColor, republished[4, 2]);
        Assert.Same(republished, NativeBattleTextureReplacements.Bind(model, load.Binding).Single(replacement => replacement.Image.Pixels.Contains(changedClosedColor)).Image);
        resources.Prepare(null).Commit();
        Assert.Empty(NativeBattleTextureReplacements.Bind(model, load.Binding));

        PixelImage Decode(NativeBattleEffectTextureState current) => NativeBattleEffectTextureReader.DecodePcRegion(current, baseline.Page, baseline.Clut, baseline.U, baseline.V, 8, 8);
        PixelImage Sample(NativeBattleEffectTextureState current) => NativeBattleEffectTextureReader.ApplyModelReplacements(Decode(current), current, baseline.Page, baseline.Clut, baseline.U, baseline.V, null, replacements);
    }

    private static BattleModel Model(ResourceTextureList textures, int target)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);
        writer.Write(2);
        writer.Write(12);
        writer.Write(620);
        for (int index = 0; index < 2; index++)
        {
            writer.Write(16);
            writer.Write(9);
            writer.Write(524);
            writer.Write((ushort)0);
            writer.Write((ushort)(224 + index));
            writer.Write((ushort)256);
            writer.Write((ushort)1);
            for (int color = 0; color < 256; color++)
            {
                writer.Write((ushort)(color == 1 ? 31 : color == 2 ? 992 : 0));
            }
            writer.Write(76);
            writer.Write((ushort)640);
            writer.Write((ushort)(128 * index));
            writer.Write((ushort)4);
            writer.Write((ushort)8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    writer.Write((ushort)(x == 2 && y == 4 ? 0x0202 : 0x0101));
                }
            }
        }
        BattleMesh[] meshes = Enumerable.Range(0, 2).Select(index => new BattleMesh([], [new([], [Vector2.Zero], index, default) { NativeMaterial = new(0x8A, (ushort)((224 + index) << 6), [Vector2.Zero]) }])).ToArray();
        return new("d0c000.dat", [], meshes, textures, [], 1)
        {
            NativeBodyTexturePack = new("synthetic:body", stream.ToArray().ToImmutableArray()),
            NativeTextureAnimations = new byte[] { 2, 0, (byte)target, 1, 1, 1, 1, 1, 0, 0, 1, 4, 2, 4 }.ToImmutableArray()
        };
    }

    private sealed class Planes(bool includeClosed = true, Rgba32? closedColor = null) : ITextureReplacementResolver
    {
        public TextureReplacement? Resolve(TextureBuildContext context)
        {
            int width = context.LogicalWidth * 2;
            int height = context.LogicalHeight * 2;
            PixelImage open = new(width, height, Enumerable.Repeat(OpenColor, width * height).ToArray());
            PixelImage closed = new(width, height, Enumerable.Repeat(closedColor ?? ClosedColor, width * height).ToArray());
            return new(open, "synthetic:planes", context.LogicalWidth, context.LogicalHeight, Variants: includeClosed ? new Dictionary<string, PixelImage> { ["eyes-closed"] = closed } : null);
        }
    }
}
