using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97SpriteTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(View, 216, 160, 108);

    [Fact]
    public void Four_hidden_callbacks_two_frame_hold_and_terminal_suppression_preserve_actual_task_fields()
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x730C10, new byte[0x70 - 12].ToImmutableArray());
        List<ushort> drawn = [];
        List<byte> phases = [];
        for (int callback = 1; callback <= 27; callback++)
        {
            phases.Add(task.ReadByte(0x29));
            NativeBattleModulePoolTask restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
            NativeBattleChoco97SpriteStep step = Advance(task);
            Assert.Equal(NativeBattleTestSnapshots.Json(step), NativeBattleTestSnapshots.Json(Advance(restored)));
            Assert.Equal((ushort)callback, step.Task.ReadWord(0x24));
            if (callback <= 4 || callback == 27)
            {
                Assert.Null(step.Packets);
            }
            else
            {
                Assert.NotNull(step.Packets);
                drawn.Add(step.Task.ReadWord(0x50));
                Assert.NotEmpty(step.Packets.Primitives);
            }
            Assert.Equal(callback == 27, step.ReadyToComplete);
            task = step.Task;
        }
        Assert.Equal(Enumerable.Range(0, 10).Select(value => (ushort)value).Concat(new ushort[] { 8, 9 }).Concat(Enumerable.Range(10, 10).Select(value => (ushort)value)).ToArray(), drawn.ToArray());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, phases.Distinct().ToArray());
        Assert.Equal((ushort)10, task.ReadWord(0x1C));
        Assert.Equal(unchecked((ushort)(20 - 0x300)), task.ReadWord(0x1E));
        Assert.Equal(unchecked((ushort)(30 - 0x280)), task.ReadWord(0x20));
        Assert.Equal((ushort)40, task.ReadWord(0x22));
        Assert.Equal((ushort)19, task.ReadWord(0x50));
        Assert.Equal((byte)5, task.ReadByte(0x29));
        Assert.Equal((byte)5, task.ReadByte(0x26));
        NativeBattleModulePoolTask beforeHold = NativeBattleStorageSnapshots.Capture(task);
        NativeBattleChoco97SpriteStep held = Advance(task.WriteByte(0x28, 1));
        Assert.False(held.ReadyToComplete);
        Assert.Null(held.Packets);
        NativeBattleChoco97SpriteStep onlyCompleteFlag = Advance(NativeBattleStorageSnapshots.Restore(beforeHold).WriteByte(0x26, 1));
        Assert.True(onlyCompleteFlag.ReadyToComplete);
        Assert.NotNull(onlyCompleteFlag.Packets);
    }

    [Fact]
    public void Sprite_draw_uses_camera_roll_and_exact_retained_pointer_and_signed_depth()
    {
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x730C10, new byte[0x70 - 12].ToImmutableArray()).WriteDword(0x4C, 0x152B5C8).WriteWord(0x54, 0xFF00);
        NativeBattleModulePacketBatch normal = NativeBattleChoco97Sprite.Draw730C90(task, Resource(), View, Projection, 0)!;
        NativeBattleModulePacketBatch rolled = NativeBattleChoco97Sprite.Draw730C90(task, Resource(), View, Projection, 512)!;
        Assert.Equal(Assert.Single(normal.Primitives).Primitive.Words[2], Assert.Single(rolled.Primitives).Primitive.Words[2]);
        Assert.NotEqual(Assert.Single(normal.Primitives).Primitive.Words[4], Assert.Single(rolled.Primitives).Primitive.Words[4]);
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Sprite.Draw730C90(task.WriteDword(0x4C, 0), Resource(), View, Projection, 0));
        Assert.Null(NativeBattleChoco97Sprite.Draw730C90(task.WriteDword(0x4C, 0).WriteByte(0x26, 4), Resource(), View, Projection, 0));
    }

    [Fact]
    public void Captured_sprite_preserves_native_packet_spacing_material_depth_and_task_bytes()
    {
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x730C10, new byte[0x70 - 12].ToImmutableArray()).WriteDword(0x4C, 0x152B5C8).WriteWord(0x54, 0xFF00);
        NativeBattleModulePacketBatch plain = NativeBattleChoco97Sprite.Draw730C90(task, Resource(), View, Projection, 512)!;
        NativeBattleModulePacketBatch captured = NativeBattleChoco97Sprite.Draw730C90(task, Resource(), View, Projection, 512, new(97, 17, 1, 2, 3))!;
        Assert.Equal(plain.AllocationBytes, captured.AllocationBytes);
        NativeBattleAllocatedPrimitive before = Assert.Single(plain.Primitives);
        NativeBattleAllocatedPrimitive after = Assert.Single(captured.Primitives);
        Assert.Equal(before.ByteOffset, after.ByteOffset);
        Assert.Equal(before.Flags20, after.Flags20);
        Assert.Equal(before.Primitive.Words.ToArray(), after.Primitive.Words.ToArray());
        Assert.Equal(before.Primitive.VertexPayload4.ToArray(), after.Primitive.VertexPayload4.ToArray());
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(after.Primitive.PilotGeometry);
        Assert.Equal(Resource().Address, visual.Identity.ResourceGeneration);
        Assert.Equal(4, visual.CornerCount);
        Assert.NotNull(visual.Sprite);
        Assert.Equal((ushort)0xFF00, task.ReadWord(0x54));
    }

    private static NativeBattleChoco97SpriteStep Advance(NativeBattleModulePoolTask task) => NativeBattleChoco97Sprite.Advance730C10(task, Resource(), [10, 0, 20, 0, 30, 0, 40, 0], View, Projection, 0);

    private static NativeBattleChoco97SpriteResources Resource()
    {
        byte[] bytes = new byte[0x368];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 20);
        int cursor = 0x34;
        for (int frame = 0; frame < 20; frame++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)cursor);
            int quads = frame < 3 ? 1 : 2;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), (uint)quads);
            for (int quad = 0; quad < quads; quad++)
            {
                int offset = cursor + 4 + quad * 20;
                bytes[offset] = 16;
                bytes[offset + 1] = 128;
                bytes[offset + 2] = 8;
                bytes[offset + 3] = 0x2C;
            }
            cursor += 4 + quads * 20;
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 0xFFFF);
        return new(0x152B5C8, bytes.ToImmutableArray());
    }
}
