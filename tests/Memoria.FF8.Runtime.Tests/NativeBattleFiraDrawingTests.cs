using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFiraDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Spark_uses_reverse_half_rate_frame_explicit_palette_and_signed_billboard_depth()
    {
        NativeBattleFiraResources resources = Resources();
        NativeBattleModulePoolTask task = Task(0x61D4C0, 10).WriteWord(32, 4096);
        NativeBattleModuleDraw draw = NativeBattleFiraDrawing.Sprite(task, resources, Sprite(5, 10));
        Assert.Equal((ushort)4, draw.Phase);
        Assert.Equal((ushort)0x10, draw.SpriteFlags);
        Assert.Equal((byte)4, draw.SpritePaletteOffsets[0]);
        Assert.Equal(-512, draw.DepthOffset);
        NativeBattleFiraDrawResult emitted = NativeBattleFiraDrawing.Draw(task, resources, Sprite(5, 10), Projection, View, 0, Colors);
        Assert.Null(emitted.Mesh);
        NativeBattleAllocatedPrimitive primitive = Assert.Single(emitted.Sprites!.Primitives);
        Assert.Equal(0x100u, primitive.Primitive.Words[3] >> 16);
        Assert.Equal(40, emitted.Sprites.AllocationBytes);
        NativeBattleModuleDraw negative = NativeBattleFiraDrawing.Sprite(task.WriteWord(32, unchecked((ushort)-9)), resources, Sprite(5, 10));
        Assert.Equal(2, negative.DepthOffset);
    }

    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(5, 4, 105)]
    [InlineData(6, 0, 128)]
    [InlineData(13, 0, 128)]
    [InlineData(14, 4, 128)]
    [InlineData(21, 4, 16)]
    public void Ring_changes_RGB_only_in_the_two_native_intervals(int counter, int flags, int intensity)
    {
        NativeBattleModulePoolTask task = Task(0x61D6B0, counter).WriteWord(28, 4096);
        NativeBattleModuleDraw draw = NativeBattleFiraDrawing.Sprite(task, Resources(), Sprite(5, 10));
        Assert.Equal((ushort)flags, draw.SpriteFlags);
        Assert.Equal((uint)intensity * 0x10101u, draw.SpriteColor);
        Assert.Equal(0, draw.DepthOffset);
        Assert.Equal(9, draw.SpriteWorldRotation.Length);
        Assert.Equal((ushort)0, draw.Phase);
    }

    [Fact]
    public void Ring_packet_uses_wrapped_RGB_and_smoke_uses_its_full_frame_counter()
    {
        NativeBattleFiraDrawResult ring = NativeBattleFiraDrawing.Draw(Task(0x61D6B0, 21).WriteWord(28, 4096), Resources(), Sprite(5, 10), Projection, View, 0, Colors);
        Assert.Equal(0x2C101010u, Assert.Single(ring.Sprites!.Primitives).Primitive.Words[1]);
        NativeBattleModuleDraw smoke = NativeBattleFiraDrawing.Sprite(Task(0x61D5D0, 15).WriteWord(28, 2560), Resources(), Sprite(5, 10));
        Assert.Equal((ushort)15, smoke.Phase);
        Assert.Equal((ushort)0, smoke.SpriteFlags);
        Assert.Equal(-320, smoke.DepthOffset);
        Assert.Empty(smoke.SpriteWorldRotation);
    }

    [Theory]
    [InlineData(0, 0xF0, 4096)]
    [InlineData(3, 0xF0, 1024)]
    [InlineData(4, 0x30, -1)]
    [InlineData(7, 0x30, -1)]
    [InlineData(8, 0xF0, 0)]
    [InlineData(17, 0xF0, 3681)]
    public void Column_preserves_opacity_boundaries_and_nonuniform_world_scale(int counter, int flags, int opacity)
    {
        NativeBattleModulePoolTask task = Task(0x61D7F0, counter).WriteWord(28, 4096).WriteWord(32, 2048);
        NativeBattleFiraMeshDraw draw = NativeBattleFiraDrawing.Mesh(task, Resources(), View);
        Assert.Equal((uint)flags, draw.Context.Flags);
        Assert.Equal(opacity < 0 ? null : (int?)opacity, draw.Context.Fade);
        Assert.Equal(new short[] { 4096, 0, 0, 0, 2048, 0, 0, 0, 4096 }, draw.Matrix.Rotation.ToArray());
        Assert.Equal((byte)3, draw.Resource.Id);
        Assert.Equal(17, draw.Context.TableBaseBucket);
        Assert.Equal((byte)2, draw.Context.DepthShift);
    }

    [Theory]
    [InlineData(13, 0x30, -1)]
    [InlineData(14, 0xF0, 0)]
    [InlineData(21, 0xF0, 3584)]
    public void Flare_enters_black_depth_cue_at_fourteen(int counter, int flags, int opacity)
    {
        NativeBattleFiraMeshDraw draw = NativeBattleFiraDrawing.Mesh(Task(0x61D9A0, counter), Resources(), View);
        Assert.Equal((uint)flags, draw.Context.Flags);
        Assert.Equal(opacity < 0 ? null : (int?)opacity, draw.Context.Fade);
        Assert.Equal(opacity < 0 ? null : (uint?)0, draw.Context.FarColor);
        Assert.Equal((byte)2, draw.Resource.Id);
    }

    [Fact]
    public void Tracer_uses_the_original_packed_UV_operand_and_current_sixteen_frame_range()
    {
        NativeBattleFiraResources resources = Resources();
        NativeBattleModulePoolTask task = Task(0x61DC20, 8).WriteWord(28, 4096);
        NativeBattleFiraMeshDraw draw = NativeBattleFiraDrawing.Mesh(task, resources, View);
        Assert.Equal(0x10F3u, draw.Context.Flags);
        Assert.Equal(resources.UvOffsets[8], draw.Context.UvOffset);
        Assert.Equal(0, draw.Context.Fade);
        Assert.Equal((byte)1, draw.Resource.Id);
        Assert.Throws<InvalidDataException>(() => NativeBattleFiraDrawing.Mesh(task.WriteWord(12, 16), resources, View));
        Assert.Throws<InvalidDataException>(() => NativeBattleFiraDrawing.Mesh(task, resources with { UvOffsets = [] }, View));
        Assert.Equal(0x1033u, NativeBattleFiraDrawing.Mesh(task.WriteWord(12, 7), resources, View).Context.Flags);
    }

    [Fact]
    public void Tracer_emits_real_mesh_packets_and_emitter_has_no_direct_submission()
    {
        NativeBattleFiraDrawResult mesh = NativeBattleFiraDrawing.Draw(Task(0x61DC20, 0).WriteWord(28, 4096), Resources(), Sprite(5, 10), Projection, View, 0, Colors);
        Assert.Null(mesh.Sprites);
        Assert.NotEmpty(mesh.Mesh!.Attempts);
        Assert.Contains(mesh.Mesh.Attempts, attempt => attempt.Primitive is not null);
        NativeBattleFiraDrawResult emitter = NativeBattleFiraDrawing.Draw(Task(0x61DAF0, 0), Resources(), Sprite(5, 10), Projection, View, 0, Colors);
        Assert.Null(emitter.Sprites);
        Assert.Null(emitter.Mesh);
        Assert.Throws<NotSupportedException>(() => NativeBattleFiraDrawing.Draw(Task(0x61CEC0, 0), Resources(), Sprite(5, 10), Projection, View, 0, Colors));
    }

    private static NativeBattleModulePoolTask Task(uint callback, int counter)
    {
        return new NativeBattleModulePoolTask(1, 0, null, callback, new byte[24].ToImmutableArray()).WriteWord(12, unchecked((ushort)counter));
    }

    private static NativeBattleFiraResources Resources()
    {
        return new(new("animation", []), Sprite(1, 1), Sprite(2, 16), Mesh(1), Mesh(2), Mesh(3), 1, Enumerable.Range(0, 16).Select(index => (uint)(index * 64)).ToImmutableArray());
    }

    private static NativeBattleModuleResource Sprite(byte id, int frames)
    {
        int start = 12 + frames * 2;
        byte[] bytes = new byte[start + 24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)frames));
        for (int frame = 0; frame <= frames; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), checked((ushort)start));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start), 1);
        bytes[start + 4] = 1;
        bytes[start + 5] = 128;
        bytes[start + 6] = 1;
        bytes[start + 7] = 0x2C;
        return new(id, id, 0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleStaticMeshResource Mesh(byte id)
    {
        byte[] bytes = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        Put(8, -64, -64);
        Put(16, 64, -64);
        Put(24, -64, 64);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), 0x20808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 4);
        return new(id, id, bytes.ToImmutableArray());

        void Put(int offset, short x, short y)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
        }
    }
}
