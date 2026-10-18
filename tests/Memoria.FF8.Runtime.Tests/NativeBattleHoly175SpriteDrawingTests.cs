using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175SpriteDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 512, 160, 112);

    [Fact]
    public void Orbital_row_draws_actual_terminal_frame_and_returns_resource_next_offset_without_mutating_retained_rows()
    {
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess();
        string retained = JsonSerializer.Serialize(state);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x5D3180, new byte[24].ToImmutableArray()).WriteWord(0x10, 100).WriteWord(0x12, 5000).WriteWord(0x14, 10);
        NativeBattleHoly175Sprite31Row row = new(4, 1, 4096, 64, 20, 1024);
        NativeBattleHoly175DrawingResult result = NativeBattleHoly175Drawing.DrawSprite31(task, row, state, Resources(), View, Projection, 0);
        Assert.Same(state, result.State);
        Assert.Equal(retained, JsonSerializer.Serialize(result.State));
        Assert.False(result.Common);
        Assert.Null(result.Geometry);
        Assert.Equal((short?)-7, result.NextOffset);
        NativeBattleAllocatedPrimitive primitive = Assert.Single(result.Sprites!.Primitives);
        Assert.Equal(40, result.Sprites.AllocationBytes);
        Assert.Equal(0x2C808080u, primitive.Primitive.Words[1]);
        NativeBattleHoly175DrawingResult changedAnchorY = NativeBattleHoly175Drawing.DrawSprite31(task.WriteWord(0x12, unchecked((ushort)-5000)), row, state, Resources(), View, Projection, 0);
        Assert.Equal(JsonSerializer.Serialize(result.Sprites), JsonSerializer.Serialize(changedAnchorY.Sprites));
        NativeBattleHoly175DrawingResult rolled = NativeBattleHoly175Drawing.DrawSprite31(task, row, state, Resources(), View, Projection, 512);
        Assert.False(primitive.Primitive.Words.SequenceEqual(Assert.Single(rolled.Sprites!.Primitives).Primitive.Words));
        Assert.Throws<InvalidDataException>(() => NativeBattleHoly175Drawing.DrawSprite31(task, row with { Frame = 2 }, state, Resources(), View, Projection, 0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 21)]
    [InlineData(6, 126)]
    [InlineData(7, 128)]
    [InlineData(18, 128)]
    public void Screen_rows_use_common_bank_inset_UV_and_signed_age_brightness_without_current_view(int age, int brightness)
    {
        NativeBattleHoly175State state = NativeBattleHoly175State.FromColdProcess();
        NativeBattleHoly175Sprite4Draw row = new(3, 0, 256, new(30, -20, 0), (short)age);
        NativeBattleHoly175DrawingResult result = NativeBattleHoly175Drawing.DrawSprite4(row, state, Resources(), Projection, 0);
        Assert.True(result.Common);
        Assert.Null(result.Geometry);
        Assert.Null(result.NextOffset);
        Assert.Same(state, result.State);
        NativeBattleAllocatedPrimitive primitive = Assert.Single(result.Sprites!.Primitives);
        Assert.Equal(4121, primitive.Primitive.Bucket);
        Assert.Equal(0x2C000000u | (uint)brightness * 0x10101u, primitive.Primitive.Words[1]);
        Assert.Equal(13u, primitive.Primitive.Words[5] & 255u);
        Assert.Equal(24u, primitive.Primitive.Words[7] >> 8 & 255u);
        NativeBattleCameraProjection unrelatedView = Projection with { View = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 9999, 8888, -7777, 4096) };
        NativeBattleHoly175DrawingResult independent = NativeBattleHoly175Drawing.DrawSprite4(row, state, Resources(), unrelatedView, 0);
        Assert.Equal(JsonSerializer.Serialize(result.Sprites), JsonSerializer.Serialize(independent.Sprites));
        NativeBattleHoly175DrawingResult rolled = NativeBattleHoly175Drawing.DrawSprite4(row, state, Resources(), Projection, 512);
        Assert.False(primitive.Primitive.Words.SequenceEqual(Assert.Single(rolled.Sprites!.Primitives).Primitive.Words));
        NativeBattleHoly175State restored = JsonSerializer.Deserialize<NativeBattleHoly175State>(JsonSerializer.Serialize(state))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleHoly175Drawing.DrawSprite4(row, restored, Resources(), Projection, 0)));
    }

    private static NativeBattleHoly175Resources Resources() => new(new("animation", []), new("texture", []), [Sprite(0xD524C4), Sprite(0xD52374)], [], new("sound", [0, 0, 0, 0]));

    private static NativeBattleHoly175SpriteResource Sprite(uint address)
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 40);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14), -7);
        foreach (int offset in new[] { 16, 40 })
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 4;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 5;
            bytes[offset + 7] = 0x2C;
            bytes[offset + 8] = 10;
            bytes[offset + 9] = 20;
        }
        return new(address, bytes.ToImmutableArray());
    }
}
