using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDispel109Tests
{
    [Theory]
    [InlineData(-1, -287)]
    [InlineData(0, 0)]
    [InlineData(7, 2012)]
    [InlineData(8, 2300)]
    [InlineData(59, 2300)]
    [InlineData(60, 2300)]
    [InlineData(67, 958)]
    [InlineData(71, 191)]
    [InlineData(72, 0)]
    public void Flash_uses_signed_age_and_original_truncating_divisions(short age, int expected)
    {
        Assert.Equal(expected, NativeBattleDispel109Controller.Flash(age));
    }

    [Fact]
    public void Fixed_base_initializer_ignores_allocator_cursor_and_root_uses_native_double_buffers()
    {
        NativeBattleDispel109Frame frame = Fixture();
        Assert.Equal(32768, frame.Memory.Cursor);
        Assert.Equal(0, frame.Effect.Dispel109!.BufferOffset);
        Assert.Equal((ushort)20, frame.Effect.Pools[0].Stride);
        frame = NativeBattleDispel109Controller.Advance6CF540(frame, Resources(), Inputs());
        Assert.Equal(69856, frame.Effect.DrawArenaOffset);
        Assert.Single(frame.Effect.Pools);
        frame = NativeBattleDispel109Controller.Advance6CF540(frame, Resources(), Inputs());
        Assert.Equal(4320, frame.Effect.DrawArenaOffset);
        Assert.Equal((ushort)840, frame.Effect.Pools[1].Stride);
        Assert.Equal(3, frame.Effect.Pools[1].Slots.Length);
        Assert.Equal((ushort)28, frame.Effect.Pools[2].Stride);
        Assert.Equal(64, frame.Effect.Pools[2].Slots.Length);
        NativeBattleModulePoolTask target = frame.Effect.Pools[1].Slots[0];
        Assert.Equal(0x1D97494u, target.ReadDword(20));
        Assert.Equal((ushort)1, target.ReadWord(12));
        Assert.Equal((ushort)1536, target.ReadWord(30));
        Assert.Equal(0x12E3600u, target.ReadDword(32));
        Assert.Equal(1u, target.ReadDword(36));
    }

    [Fact]
    public void Complete_lifecycle_draws_new_sparks_same_tick_contacts_at_67_and_releases_at_72()
    {
        List<NativeBattleDispel109Operation> operations = [];
        List<(ushort Age, ushort Life, uint Resource)> sprites = [];
        NativeBattleDispel109Inputs inputs = Inputs() with
        {
            Execute = (current, operation) =>
            {
                operations.Add(operation);
                return current;
            },
            DrawSprite = (current, task) =>
            {
                sprites.Add((task.ReadWord(12), task.ReadWord(14), task.ReadDword(24)));
                return current;
            }
        };
        NativeBattleDispel109Frame frame = Fixture();
        for (int tick = 0; tick < 74; tick++)
        {
            frame = NativeBattleDispel109Controller.Advance6CF540(frame, Resources(), inputs);
            if (tick == 36)
            {
                NativeBattleDispel109Frame independent = JsonSerializer.Deserialize<NativeBattleDispel109Frame>(JsonSerializer.Serialize(frame))!;
                Assert.Equal(JsonSerializer.Serialize(NativeBattleDispel109Controller.Advance6CF540(frame, Resources(), Inputs())), JsonSerializer.Serialize(NativeBattleDispel109Controller.Advance6CF540(independent, Resources(), Inputs())));
            }
            Assert.Equal(tick == 73, frame.Effect.ReturnedDescriptor == 0);
        }
        Assert.Single(operations, operation => operation.Kind == NativeBattleDispel109OperationKind.Sound);
        Assert.Single(operations, operation => operation.Kind == NativeBattleDispel109OperationKind.Preset);
        Assert.Equal(0, Assert.Single(operations, operation => operation.Kind == NativeBattleDispel109OperationKind.Contact).Value);
        Assert.Equal(8, sprites.Count(sprite => sprite is { Age: 0, Life: 16, Resource: 0x12E3440 }));
        Assert.Equal(3, sprites.Count(sprite => sprite is { Age: 0, Life: 7, Resource: 0x12E337C }));
        Assert.Equal(8 * 16 + 3 * 7, sprites.Count);
        Assert.All(frame.Effect.Pools, pool => Assert.Null(pool.Head));
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 40; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(0, operations[^1].Value);
    }

    [Fact]
    public void Target_random_sparks_use_five_draws_signed_high_word_and_exact_anchor_offsets()
    {
        NativeBattleDispel109Frame frame = Fixture();
        frame = NativeBattleDispel109Controller.Advance6CF540(frame, Resources(), Inputs());
        frame = NativeBattleDispel109Controller.Advance6CF540(frame, Resources(), Inputs());
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[0].WriteWord(12, 26).WriteWord(30, 0x8000);
        NativeBattleDispel109LeafStep result = NativeBattleDispel109Controller.Advance6CF700(frame, task, Resources(), Inputs());
        NativeCrtRandomState expected = new NativeCrtRandomState(1).Draw(out ushort a).Draw(out ushort b).Draw(out ushort c).Draw(out ushort d).Draw(out ushort e);
        NativeBattleModulePoolTask spark = result.Frame.Effect.Pools[2].Slots[0];
        Assert.Equal(unchecked((ushort)(100 + (unchecked((short)((uint)unchecked(-32768 * (a + b - 32768)) >> 16)) >> 2))), spark.ReadWord(16));
        Assert.Equal(unchecked((ushort)(200 - (c >> 6) + 256)), spark.ReadWord(18));
        Assert.Equal(unchecked((ushort)(300 + (unchecked((short)((uint)unchecked(-32768 * (d + e - 32768)) >> 16)) >> 2))), spark.ReadWord(20));
        Assert.Equal(expected, result.Frame.Random);
        Assert.Equal((ushort)27, result.Task.ReadWord(12));
    }

    [Theory]
    [InlineData(0x7FFF, 0x8000, 2)]
    [InlineData(0xFFFF, 0, 2)]
    [InlineData(15, 16, 2)]
    public void Sprite_completion_compares_incremented_signed_WORDs(ushort age, ushort life, int expected)
    {
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6CFC50, new byte[16].ToImmutableArray());
        task = task.WriteWord(12, age).WriteWord(14, life);
        NativeBattleDispel109LeafStep step = NativeBattleDispel109Controller.Advance6CFC50(Fixture(), task, Inputs());
        Assert.Equal(expected, step.Result);
        Assert.Equal(unchecked((ushort)(age + 1)), step.Task.ReadWord(12));
    }

    [Fact]
    public void Geometry_consumes_the_required_stack_branch_and_wraps_before_camera_rotation()
    {
        byte[] data = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 400);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), -400);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, data.ToImmutableArray());
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);
        NativeBattleActionMatrix unscaled = NativeBattleDispel109Geometry.Compose6CF9A0(node, view, new(32760, 0, 0), 2048, false);
        NativeBattleActionMatrix scaled = NativeBattleDispel109Geometry.Compose6CF9A0(node, view, new(32760, 0, 0), 2048, true);
        Assert.Equal(unchecked((short)(32760 + 400)) + 10, unscaled.X);
        Assert.Equal(unchecked((short)(32760 + 200)) + 10, scaled.X);
        Assert.Equal(4116, unscaled.Y);
        Assert.Equal(3020, scaled.Y);
        Assert.Equal(new short[] { 2048, 0, 0, 0, 3000, 0, 0, 0, 2048 }, scaled.Rotation);
        Assert.True(NativeBattleDispel109Geometry.Visible(node));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), 4096);
        Assert.False(NativeBattleDispel109Geometry.Visible(node with { Data = data.ToImmutableArray() }));
    }

    [Fact]
    public void Morph_writes_XYZ_only_and_keeps_original_padding_unknown()
    {
        byte[] source = new byte[100];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 20);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(20), 28);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(24), 1);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(40), 200);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(42), 400);
        BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(44), 600);
        byte[] nodeBytes = new byte[44];
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(nodeBytes.AsSpan(42), 1);
        NativeBattleGfAnimationNode node = new(0, 0, nodeBytes.ToImmutableArray());
        NativeBattleDispel109Frame frame = Fixture();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(4312, new byte[] { 1, 2, 3, 4, 5, 6, 0xA5, 0x5A }).MarkUnknown(4318, 2) } };
        NativeBattleModulePoolTask target = new(1, 0, null, 0x6CF700, new byte[828].ToImmutableArray());
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096), 216, 160, 108);
        NativeBattleDispel109Draw drawn = NativeBattleDispel109Geometry.Draw6CF9A0(frame, target, node, false, Resources() with { Animation = new(0x12E3600, source.ToImmutableArray()) }, projection, new(0, 0, 1f / 4096, 4503601774854144d));
        Assert.Equal(new byte[] { 100, 0, 200, 0, 44, 1 }, drawn.Frame.Memory.Arena!.Read(4312, 6));
        (byte[] retained, bool[] known) = drawn.Frame.Memory.Arena.ReadSnapshot(4318, 2);
        Assert.Equal(new byte[] { 0xA5, 0x5A }, retained);
        Assert.All(known, value => Assert.False(value));
        Assert.Equal(0x20DFAB8u + 4312, drawn.Context!.SelectedVertices);
        Assert.Null(drawn.Context.FarColor);
        Assert.Equal(0x2030u, drawn.Context.Flags);
        Assert.Empty(drawn.Packets.Attempts);
    }

    private static NativeBattleDispel109Frame Fixture()
    {
        NativeBattleOriginalEffectState effect = new(109, 108, new(109, 0x6CF4A0, 0x6CF4B0), new(0, 1, 8, 0, 0, 0), null);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(108) with { Cursor = 32768 };
        return NativeBattleDispel109Controller.Initialize6CF4D0(new(memory, effect, new(1)));
    }

    private static NativeBattleDispel109Resources Resources()
    {
        byte[] source = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 46);
        return new(new(0x12E3600, source.ToImmutableArray()), new("camera", []), new(0, 0x12E337C, 0, 0, []), new(0, 0x12E3440, 0, 0, []), 0x4941E);
    }

    private static NativeBattleDispel109Inputs Inputs()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 28, 109, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        return new(packets, _ => new(100, 200, 300, 0), _ => 256, (current, _) => current, (current, _, _) => current, (current, _) => current);
    }
}
