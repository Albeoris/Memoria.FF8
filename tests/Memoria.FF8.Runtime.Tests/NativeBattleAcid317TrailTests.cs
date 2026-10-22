using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAcid317TrailTests
{
    [Fact]
    public void Two_points_emit_native_partial_GT4_with_signed_width_depth_and_no_sidecar()
    {
        NativeBattleAcid317Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, 2048, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, 1024, 0);
        string registers = JsonSerializer.Serialize(new { frame.Memory.GeometryRegisters, frame.Memory.ScriptedColorRegisters });
        var actual = NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources());
        Assert.Equal(52, actual.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.Equal(145, packet.Primitive!.Bucket);
        Assert.Empty(packet.Primitive.VertexPayload4);
        Assert.Equal(0x0C000000u, Dword(packet.Data, 0));
        Assert.Equal(0x3E808080u, Dword(packet.Data, 4));
        Assert.Equal(0x00808080u, Dword(packet.Data, 16));
        Assert.Equal(0x00404040u, Dword(packet.Data, 28));
        Assert.Equal(0x00404040u, Dword(packet.Data, 40));
        Assert.Equal(0x00C70064u, Dword(packet.Data, 8));
        Assert.Equal(0x00C90064u, Dword(packet.Data, 20));
        Assert.Equal(0x00C80074u, Dword(packet.Data, 32));
        Assert.Equal(0x00C80074u, Dword(packet.Data, 44));
        Assert.Equal(0x3D14E048u, Dword(packet.Data, 12));
        Assert.Equal(0x00BAE078u, Dword(packet.Data, 24));
        Assert.Equal((ushort)0xE048, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(36)));
        Assert.Equal((ushort)0xE078, BinaryPrimitives.ReadUInt16LittleEndian(packet.Data.AsSpan().Slice(48)));
        for (int index = 0; index < 52; index++)
        {
            Assert.Equal(index is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, packet.WriteMask[index]);
        }
        Assert.Equal(registers, JsonSerializer.Serialize(new { actual.Frame.Memory.GeometryRegisters, actual.Frame.Memory.ScriptedColorRegisters }));
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Equal(frame.State, actual.Frame.State);
    }

    [Fact]
    public void Interior_miter_uses_previous_direction_and_arithmetic_floor_then_emits_in_chain_order()
    {
        NativeBattleAcid317Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, 2048, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, 2048, Address(2));
        frame = Point(frame, 2, 116, 216, 512, 32, 2048, 0);
        var actual = NativeBattleAcid317Trail.Draw(frame, Address(0), 3, Resources());
        Assert.Equal(104, actual.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, 52 }, actual.Batch.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        int middle = PointsOffset + PointStride;
        Assert.Equal((short)118, Word(actual.Frame.Memory.Arena!, middle + 0x10));
        Assert.Equal((short)199, Word(actual.Frame.Memory.Arena!, middle + 0x12));
        Assert.Equal((short)114, Word(actual.Frame.Memory.Arena!, middle + 0x18));
        Assert.Equal((short)201, Word(actual.Frame.Memory.Arena!, middle + 0x1A));
        Assert.Equal(Dword(actual.Batch.Attempts[0].Data, 32), Dword(actual.Batch.Attempts[1].Data, 8));
        Assert.Equal(Dword(actual.Batch.Attempts[0].Data, 44), Dword(actual.Batch.Attempts[1].Data, 20));
        Assert.Equal(Address(2), BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Memory.Arena!.Read(middle + 0x24, 4)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Nonpositive_depth_skips_packet_and_never_reads_intensity_or_unknown_W(short depth)
    {
        NativeBattleAcid317Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, depth, 32, null, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, null, 0);
        var actual = NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources());
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(PointsOffset + 0x1C, 4));
    }

    [Fact]
    public void Zero_distance_marks_first_depth_invalid_without_fabricating_preceding_slope()
    {
        NativeBattleAcid317Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, null, Address(1));
        frame = Point(frame, 1, 100, 200, 512, 32, null, 0);
        byte[] table = new byte[128 * 128];
        var actual = NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources(table));
        Assert.Equal((short)-1, Word(actual.Frame.Memory.Arena!, PointsOffset + 0x14));
        Assert.Empty(actual.Batch.Attempts);
        frame = Point(frame, 1, 116, 200, 512, 32, 1024, Address(2));
        frame = Point(frame, 2, 132, 200, 512, 32, 1024, 0);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(PointsOffset + 0x14, BitConverter.GetBytes((short)-1)) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleAcid317Trail.Draw(frame, Address(0), 3, Resources()));
    }

    [Fact]
    public void Negative_next_depth_avoids_distance_table_and_record_color_reads()
    {
        NativeBattleAcid317Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, null, Address(1));
        frame = Point(frame, 1, 116, 200, -1, 32, null, 0);
        var actual = NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources([]));
        Assert.Empty(actual.Batch.Attempts);
        Assert.Equal((short)100, Word(actual.Frame.Memory.Arena!, PointsOffset + 0x10));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(PointsOffset + 0x18, 4));
    }

    [Fact]
    public void Count_pointer_and_lazy_knownness_guards_fail_at_the_reached_owner()
    {
        NativeBattleAcid317Frame frame = Frame();
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317Trail.Draw(frame, Address(0), 1, Resources()));
        Assert.Throws<InvalidDataException>(() => NativeBattleAcid317Trail.Draw(frame, Address(0) + 1, 2, Resources()));
        Assert.Throws<NotSupportedException>(() => NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources()));
        frame = Point(frame, 0, 100, 200, 512, 32, 2048, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, 2048, 0);
        Assert.Throws<NotSupportedException>(() => NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources([])));
    }

    [Fact]
    public void JSON_restore_preserves_live_chain_unknown_fields_and_packet_result()
    {
        NativeBattleAcid317Frame frame = Frame();
        frame = Point(frame, 0, 100, 200, 512, 32, 2048, Address(1));
        frame = Point(frame, 1, 116, 200, 512, 32, 1024, 0);
        NativeBattleAcid317Frame restored = JsonSerializer.Deserialize<NativeBattleAcid317Frame>(JsonSerializer.Serialize(frame))!;
        var expected = NativeBattleAcid317Trail.Draw(frame, Address(0), 2, Resources());
        var actual = NativeBattleAcid317Trail.Draw(restored, Address(0), 2, Resources());
        Assert.Equal(JsonSerializer.Serialize(expected.Frame), JsonSerializer.Serialize(actual.Frame));
        Assert.Equal(JsonSerializer.Serialize(expected.Batch), JsonSerializer.Serialize(actual.Batch));
    }

    private static uint Address(int index) => ArenaAddress + (uint)(PointsOffset + index * PointStride);
    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleAcid317Resources Resources(byte[]? table = null) => new(new("unused", []), new(0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new("unused", []), (table ?? Enumerable.Repeat((byte)16, 128 * 128).ToArray()).ToImmutableArray(), [], 0);

    private static NativeBattleAcid317Frame Frame()
    {
        NativeBattleOriginalEffectState effect = new(317, 317, new(317, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x24708) };
        return new(memory, effect, new(1), NativeBattleAcid317State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
    }

    private static NativeBattleAcid317Frame Point(NativeBattleAcid317Frame frame, int index, short x, short y, short depth, short width, short? intensity, uint next)
    {
        int offset = PointsOffset + index * PointStride;
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(offset + 6, BitConverter.GetBytes(width)).Write(offset + 0x10, BitConverter.GetBytes(x)).Write(offset + 0x12, BitConverter.GetBytes(y)).Write(offset + 0x14, BitConverter.GetBytes(depth)).Write(offset + 0x24, BitConverter.GetBytes(next));
        if (intensity is { } value)
        {
            arena = arena.Write(offset + 0x20, BitConverter.GetBytes(value));
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }
}
