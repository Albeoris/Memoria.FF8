using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScenePaletteInterningTests
{
    [Theory]
    [InlineData(0, 16)]
    [InlineData(1, 256)]
    public void Equal_ordered_palette_words_from_distinct_CLUTs_share_sample_identity(int depth, int length)
    {
        NativeBattleSoftwarePrimitiveExecution execution = Execution();
        NativeBattleSceneCaptureSample first = Sample(execution, depth, 0);
        NativeBattleSceneCaptureSample second = Sample(execution, depth, 64);
        Assert.Equal(length, first.Palette.Length);
        Assert.Equal(Palette().AsSpan(0, length).ToArray(), first.Palette.ToArray());
        Assert.True(first.Palette.Equals(second.Palette));
        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(1, 256)]
    public void Mutation_epochs_reread_words_preserve_old_samples_and_reuse_only_equal_content(int depth, int length)
    {
        NativeBattleSoftwarePrimitiveExecution execution = Execution();
        NativeBattleSceneCaptureSample first = Sample(execution, depth, 0);
        ImmutableArray<ushort> original = first.Palette;
        NativeBattleEffectTextureState firstOwner = execution.Textures;
        Fill(execution, 0, 100);
        NativeBattleSceneCaptureSample outside = Sample(execution, depth, 0);
        Assert.NotSame(firstOwner, execution.Textures);
        Assert.True(original.Equals(outside.Palette));
        NativeBattleEffectTextureState outsideOwner = execution.Textures;
        Fill(execution, 3, 0);
        NativeBattleSceneCaptureSample changed = Sample(execution, depth, 0);
        Assert.NotSame(outsideOwner, execution.Textures);
        Assert.False(original.Equals(changed.Palette));
        Assert.Equal((ushort)0x7FFF, changed.Palette[3]);
        Assert.Equal(Palette()[3], first.Palette[3]);
        for (int index = 0; index < length; index++)
        {
            Assert.Equal(index == 3 ? (ushort)0x7FFF : original[index], changed.Palette[index]);
        }
        NativeBattleSceneCaptureSample untouchedClut = Sample(execution, depth, 64);
        Assert.True(original.Equals(untouchedClut.Palette));
        Assert.True(original.AsSpan().SequenceEqual(Palette().AsSpan(0, length)));
    }

    [Fact]
    public void Indexed_depths_do_not_merge_a_short_palette_with_its_equal_long_prefix()
    {
        NativeBattleSoftwarePrimitiveExecution execution = Execution();
        ImmutableArray<ushort> fourBit = Sample(execution, 0, 0).Palette;
        ImmutableArray<ushort> eightBit = Sample(execution, 1, 0).Palette;
        Assert.Equal(16, fourBit.Length);
        Assert.Equal(256, eightBit.Length);
        Assert.True(fourBit.AsSpan().SequenceEqual(eightBit.AsSpan(0, 16)));
        Assert.False(fourBit.Equals(eightBit));
    }

    [Fact]
    public void Canonical_palette_identity_is_private_to_one_execution()
    {
        ImmutableArray<ushort> first = Sample(Execution(), 0, 0).Palette;
        ImmutableArray<ushort> second = Sample(Execution(), 0, 0).Palette;
        Assert.True(first.AsSpan().SequenceEqual(second.AsSpan()));
        Assert.False(first.Equals(second));
    }

    private static NativeBattleSoftwarePrimitiveExecution Execution()
    {
        ushort[] palette = Palette();
        byte[] encoded = new byte[palette.Length * 2];
        for (int index = 0; index < palette.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(index * 2), palette[index]);
        }
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, palette.Length, 1, encoded).Upload45BD30(0, 1, palette.Length, 1, encoded);
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer().Initialize500400() with { ViewportInputs = null };
        uint[] handlers = new uint[256];
        handlers[0x02] = 0x466EF0;
        return new(state, textures, new(handlers.ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
    }

    private static NativeBattleSceneCaptureSample Sample(NativeBattleSoftwarePrimitiveExecution execution, int depth, ushort clut)
    {
        ushort page = checked((ushort)(0x59 | depth << 7));
        execution.Cache = execution.Cache!.Preprocess462F90(page, clut).Prepare464BD0().Cache;
        NativeBattlePcTextureWindowResolve selected = execution.Cache.Resolve465CE0(page, 0, 0);
        execution.Cache = selected.Cache;
        NativeBattleSceneCapture capture = new(BattleSceneVisibleArea.Native, NativeBattleSceneCaptureKind.ScreenSample) { X = 576, Y = 256 };
        NativeBattleLogicalCommand command = new(new("sample", 0, 0), 1, 0x26, [0x26808080, 0x00020002, (uint)clut << 16, 0x00020008, (uint)page << 16, 0x00080002, 0], 1, [], null) { SceneCapture = capture };
        NativeBattleLogicalResolvedCommand actual = execution.RetainTexturePixels(new(command, new(page) { TextureWindowBits = 0, BlendMode = 2, TextureSelection = selected.Selection }, 1));
        return Assert.IsType<NativeBattleSceneCaptureSample>(actual.DrawMode.SceneCaptureSample);
    }

    private static void Fill(NativeBattleSoftwarePrimitiveExecution execution, ushort x, ushort y)
    {
        NativeBattleLogicalCommand fill = new(new("fill", 0, 0), 1, 0x02, [0x02FFFFFF, x | (uint)y << 16, 1u | 1u << 16], 1, [], null);
        execution.ExecuteSoftware(fill);
    }

    private static ushort[] Palette()
    {
        return Enumerable.Range(0, 256).Select(index => checked((ushort)(1 + index * 17))).ToArray();
    }
}
