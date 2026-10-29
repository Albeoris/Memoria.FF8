using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleActorTextureTests
{
    [Theory]
    [InlineData(0, 10, 0, 224)]
    [InlineData(1, 10, 128, 225)]
    [InlineData(4, 12, 0, 228)]
    [InlineData(11, 15, 128, 235)]
    [InlineData(12, 10, 128, 236)]
    [InlineData(14, 12, 128, 238)]
    public void Native507550_preserves_material_bits_and_relocates_page_palette_and_every_v(int slot, int page, int v, int row)
    {
        NativeBattleModelTextureBinding binding = new(ImmutableArray.CreateRange(new byte?[16]).SetItem(1, (byte)slot));
        NativeBattleFaceMaterial source = new(0x1D7, (ushort)(225 << 6 | 3), [new(11, 7), new(19, 135), new(99, 127)]);
        NativeBattleFaceMaterial relocated = NativeBattleFaceMaterialBinding.Apply507550(source, binding);
        Assert.Equal((ushort)(0x1C0 | page), relocated.TexturePage);
        Assert.Equal((ushort)(row << 6 | 3), relocated.Clut);
        Assert.Equal(new Vector2[] { new(11, v | 7), new(19, v | 7), new(99, v | 127) }, relocated.Uv);
        Assert.Equal(new Vector2(19, 135), source.Uv[1]);
    }

    [Fact]
    public void Native_actor_reads_canonical_vram_and_observes_queued_texture_copy_without_reloading_the_model()
    {
        BattleVertex[] vertices = [new(new(-1, 0, 0), 0), new(new(1, 0, 0), 0), new(new(-1, 2, 0), 0)];
        BattleFace face = new([0, 1, 2], [Vector2.Zero, Vector2.Zero, Vector2.Zero], 0, new(128, 128, 128)) { NativeMaterial = new(0x100, 224 << 6, [new(16, 16), new(16, 16), new(16, 16)]) };
        BattleModel model = new("native-actor", [new(-1, 0)], [new(vertices, [face])], [new PixelImage(1, 1, [new(0, 255, 0)])], [new BattleAnimation([new(Vector3.Zero, [Vector3.Zero], [Vector3.One])])], 1);
        byte[] red = new byte[2];
        byte[] blue = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(red, 0x801F);
        BinaryPrimitives.WriteUInt16LittleEndian(blue, 0xFC00);
        NativeBattleEffectArena bank = NativeBattleEffectArena.Clear().Write(2 * (16 * 1024 + 656), red).Write(2 * (16 * 1024 + 700), blue);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess() with { VramRegions = NativeBattleStorageSnapshots.Capture(bank).Regions };
        NativeBattleModelTextureBinding binding = new(ImmutableArray.CreateRange(new byte?[16]).SetItem(0, 0));
        BattleActorDraw actor = new(model, Vector3.Zero) { NativeTextureBinding = binding, NativeTextures = textures };
        BattleRenderer renderer = new(64, 64);
        BattleCamera camera = new(new(0, 1, -10), new(0, 1, 0));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new BattleStage(0, [], []), [actor with { NativeTextures = null }], camera));
        Assert.Throws<NotSupportedException>(() => renderer.Render(new BattleStage(0, [], []), [actor with { NativeTextureBinding = null }], camera));
        byte[] first = renderer.Render(new BattleStage(0, [], []), [actor], camera).ToArray();
        Assert.Contains(Enumerable.Range(0, 64 * 64), index => first[index * 4] == 255 && first[index * 4 + 2] == 0);
        NativeBattleEffectTextureState copied = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(textures)).EnqueueCopy505EB0(700, 16, 1, 1, 656, 16).Drain505D20().State;
        byte[] second = renderer.Render(new BattleStage(0, [], []), [actor with { NativeTextures = copied }], camera).ToArray();
        Assert.Contains(Enumerable.Range(0, 64 * 64), index => second[index * 4] == 0 && second[index * 4 + 2] == 255);
        Assert.NotEqual(first, second);
        Assert.Equal(first, renderer.Render(new BattleStage(0, [], []), [actor], camera));
    }

    [Fact]
    public void Native_actor_material_does_not_substitute_cached_tim_for_unknown_vram_or_allocation()
    {
        NativeBattleFaceMaterial material = new(0, 224 << 6, [Vector2.Zero]);
        Assert.Throws<NotSupportedException>(() => NativeBattleFaceMaterialBinding.Apply507550(material, new(ImmutableArray.CreateRange(new byte?[16]))));
    }
}
