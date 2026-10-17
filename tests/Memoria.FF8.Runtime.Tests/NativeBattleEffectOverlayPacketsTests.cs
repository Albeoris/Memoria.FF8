using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectOverlayPacketsTests
{
    [Fact]
    public void Original_task_material_and_centering_produce_raw_ft4_at_the_actual_table_head_with_no_depth_precision()
    {
        List<ushort> depths = [];
        NativeBattleGfGeometryBatch batch = NativeBattleEffectOverlayPackets.Emit8D6790(Task(), Projection(), depths.Add);
        Assert.Equal(40, batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
        Assert.Equal(new uint[] { 0x09000000, 0x2F000000, 0x005C008C, 0x3D142000, 0x005C00B4, 0x00B92028, 0x006C008C, 0x00003000, 0x006C00B4, 0x00003028 }, primitive.Words.ToArray());
        Assert.Equal(0, primitive.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, primitive.VertexPayload4.ToArray());
        Assert.Equal(new ushort[] { 1000 }, depths);
        Assert.Equal(33, attempt.WriteMask.Count(state => state == NativeBattleGfPacketByteState.Known));
        Assert.Equal(new[] { 4, 5, 6, 30, 31, 38, 39 }, attempt.WriteMask.Select((state, index) => (state, index)).Where(item => item.state == NativeBattleGfPacketByteState.Untouched).Select(item => item.index).ToArray());
        Assert.Equal(1u, ((primitive.Words[5] >> 16) >> 5) & 3);
    }

    [Theory]
    [InlineData(-5, 259, 250, 252, 162, -151, 158, 108, 245, 255)]
    [InlineData(259, -5, 250, 252, 31, 113, 289, 108, 253, 247)]
    public void Geometry_uses_signed_words_but_uv_addition_uses_only_wrapping_low_bytes(short width, short height, byte u, byte v, short left, short top, short right, short bottom, byte rightU, byte bottomV)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x4C, unchecked((ushort)width)).WriteWord(0x4E, unchecked((ushort)height)).WriteByte(0x48, u).WriteByte(0x4A, v);
        NativeBattleGeometryPrimitive primitive = Assert.Single(NativeBattleEffectOverlayPackets.Emit8D6790(task, Projection()).Attempts).Primitive!;
        Assert.Equal(Screen(left, top), primitive.Words[2]);
        Assert.Equal(Screen(right, top), primitive.Words[4]);
        Assert.Equal(Screen(left, bottom), primitive.Words[6]);
        Assert.Equal(Screen(right, bottom), primitive.Words[8]);
        Assert.Equal(u, (byte)primitive.Words[3]);
        Assert.Equal(v, (byte)(primitive.Words[3] >> 8));
        Assert.Equal(rightU, (byte)primitive.Words[5]);
        Assert.Equal(bottomV, (byte)(primitive.Words[7] >> 8));
        Assert.Equal(rightU, (byte)primitive.Words[9]);
        Assert.Equal(bottomV, (byte)(primitive.Words[9] >> 8));
    }

    [Theory]
    [InlineData(65535, 65535, 65535, 65535, 0x8BF, 0xFFFF)]
    [InlineData(63, 512, 4096, 1024, 0x8A0, 0)]
    public void Material_uses_original_signed_word_shifts_masks_and_low_word_palette_row_wrap(ushort textureX, ushort textureY, ushort paletteX, ushort paletteY, ushort expectedPage, ushort expectedClut)
    {
        NativeBattleModulePoolTask task = Task().WriteWord(0x40, textureX).WriteWord(0x42, textureY).WriteWord(0x44, paletteX).WriteWord(0x46, paletteY);
        NativeBattleGeometryPrimitive primitive = Assert.Single(NativeBattleEffectOverlayPackets.Emit8D6790(task, Projection()).Attempts).Primitive!;
        Assert.Equal(expectedPage, (ushort)(primitive.Words[5] >> 16));
        Assert.Equal(expectedClut, (ushort)(primitive.Words[3] >> 16));
        Assert.Equal(1, expectedPage >> 5 & 3);
    }

    [Fact]
    public void Projection_uses_signed_screen_word_sar_and_still_emits_for_zero_depth_without_nclip_or_a_near_rejection()
    {
        NativeBattleCameraProjection projection = Projection() with { View = Projection().View! with { Z = -1 }, OffsetX = -1, OffsetY = -1 };
        List<ushort> depths = [];
        NativeBattleGfGeometryBatch batch = NativeBattleEffectOverlayPackets.Emit8D6790(Task().WriteWord(0x1C, unchecked((ushort)-1)).WriteWord(0x1E, unchecked((ushort)-1)), projection, depths.Add);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(new ushort[] { 0 }, depths);
        //45FAA0 gives screen WORD-24 in each axis; SAR3 is-3, rather than a DWORD shift or truncation toward zero.
        Assert.Equal(Screen(-23, -19), primitive.Words[2]);
        Assert.Equal(Screen(17, -3), primitive.Words[8]);
        Assert.Equal(40, batch.AllocationBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Physical_arena_and_json_preserve_prior_known_or_unknown_rgb_and_padding_without_zero_filling(bool unknown)
    {
        const int cursor = 0x6000;
        NativeBattleGfGeometryBatch batch = NativeBattleEffectOverlayPackets.Emit8D6790(Task(), Projection());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(4);
        NativeBattleEffectArena arena = memory.Arena!.Write(cursor, Enumerable.Repeat((byte)0xA5, 40).ToArray());
        foreach (int offset in new[] { 4, 5, 6, 30, 31, 38, 39 })
        {
            if (unknown)
            {
                arena = arena.MarkUnknown(cursor + offset, 1);
            }
        }
        memory = memory with { Arena = arena };
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, cursor, NativeBattleLogicalOrdering.Clear(), batch, flags20: 1);
        Assert.Equal(cursor + 40, written.Cursor);
        NativeBattleLogicalProxy proxy = Assert.Single(written.Ordering.Proxies);
        Assert.Equal((ushort)1, proxy.Flags20);
        Assert.Equal(new[] { 0, 0, 0, 0 }, proxy.VertexPayload4.ToArray());
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(written.Memory))!;
        NativeBattleEffectArena actualArena = restored.Arena!;
        Assert.Equal(3, actualArena.Read(cursor, 3).Length);
        Assert.Equal(new byte[] { 9 }, actualArena.Read(cursor + 3, 1));
        Assert.Equal(new byte[] { 0x2F }, actualArena.Read(cursor + 7, 1));
        foreach (int offset in new[] { 4, 5, 6, 30, 31, 38, 39 })
        {
            if (unknown)
            {
                Assert.Throws<NotSupportedException>(() => actualArena.Read(cursor + offset, 1));
            }
            else
            {
                Assert.Equal(new byte[] { 0xA5 }, actualArena.Read(cursor + offset, 1));
            }
        }
        Assert.Equal(new byte[] { 0xA5 }, memory.Arena!.Read(cursor, 1));
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGfGeometryBatch>(JsonSerializer.Serialize(batch))));
    }

    [Fact]
    public void Missing_camera_or_wrong_physical_task_shape_is_not_replaced_with_a_default()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectOverlayPackets.Emit8D6790(Task(), Projection() with { View = null }));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOverlayPackets.Emit8D6790(Task() with { Callback = 0x8D6710 }, Projection()));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectOverlayPackets.Emit8D6790(Task() with { Payload = new byte[64].ToImmutableArray() }, Projection()));
    }

    private static NativeBattleModulePoolTask Task()
    {
        return new NativeBattleModulePoolTask(1, 0, null, 0x8D6790, new byte[68].ToImmutableArray()).WriteWord(0x40, 0x240).WriteWord(0x42, 0x120).WriteWord(0x44, 0x140).WriteWord(0x46, 0xF4).WriteWord(0x48, 0).WriteWord(0x4A, 0x20).WriteWord(0x4C, 0x28).WriteWord(0x4E, 0x10);
    }

    private static NativeBattleCameraProjection Projection()
    {
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 1000, 4096);
        return new(camera, 216, 160, 108);
    }

    private static uint Screen(short x, short y)
    {
        return (ushort)x | (uint)(ushort)y << 16;
    }
}
