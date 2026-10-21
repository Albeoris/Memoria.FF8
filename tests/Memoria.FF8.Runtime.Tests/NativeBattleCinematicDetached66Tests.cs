using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicDetached66Tests
{
    private const int RecordOffset = 0x10000;
    private const uint Record = NativeBattleEffectNodeMemory.ArenaAddress + RecordOffset;
    private const uint Payload = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Initialization_writes_only_native_fields_and_preserves_triangle_angles(bool quad)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { Arena = frame.Arena.MarkUnknown(RecordOffset, 48) };
        ImmutableArray<NativeBattleVector> vertices = [new(-1, 0, 0), new(-3, 0, 0), new(-4, 0, 0)];
        if (quad)
        {
            vertices = vertices.Add(new(-4, 0, 0));
        }
        frame = NativeBattleCinematicDetached66.InitializeRecord(frame, Record, vertices, new(0, 0, 0), quad);
        var snapshot = frame.Arena.ReadSnapshot(RecordOffset, 48);
        int[] expected = Enumerable.Range(0, 12).Concat(Enumerable.Range(0x24, 8)).Concat(quad ? Enumerable.Range(0x18, 6) : []).Order().ToArray();
        Assert.Equal(expected, Enumerable.Range(0, 48).Where(index => snapshot.Known[index]));
        Assert.Equal((short)(quad ? -3 : -2), Word(frame, 0x24));
        Assert.Equal(quad ? -768 : -512, Dword(frame, 0));
        Assert.Equal((short)(quad ? 3 : 2), Word(frame, 0x2A));
    }

    [Fact]
    public void Distance_uses_nested_integer_roots()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleCinematicDetached66.InitializeRecord(WaterSchedulerFixture.Create(), Record, [new(2, 2, 2), new(2, 2, 2), new(2, 2, 2)], new(0, 0, 0), false);
        Assert.Equal((short)2, Word(frame, 0x2A));
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public void Activation_uses_strict_signed_distance_and_nine_ordered_random_draws(int threshold, bool activates)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = Put(frame, 0x2A, BitConverter.GetBytes((short)10));
        frame = Put(frame, 0x2C, [0xFF]);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, (ushort)threshold);
        NativeCrtRandomState expected = frame.Random;
        if (activates)
        {
            for (int index = 0; index < 9; index++)
            {
                expected = expected.Draw(out _);
            }
        }
        NativeBattleWaterExecutionFrame result = NativeBattleCinematicDetached66.ActivateRecord(frame, Record, Payload);
        Assert.Equal(expected, result.Random);
        Assert.Equal(activates ? (byte)3 : (byte)255, result.Arena.Read(RecordOffset + 0x2C, 1)[0]);
        if (activates)
        {
            Assert.Equal((short)-7, Word(result, 0xC));
            Assert.Equal((short)5, Word(result, 0xE));
        }
    }

    [Fact]
    public void Motion_ignores_pause_and_preserves_full_MAC_translation_after_WORD_position_truncation()
    {
        NativeBattleWaterExecutionFrame frame = Fixture().WriteTemporaryByte(0x145, 1);
        frame = Put(frame, 0, BitConverter.GetBytes(unchecked(65537 << 8)));
        frame = Put(frame, 0xC, BitConverter.GetBytes((short)16));
        frame = Put(frame, 0xE, BitConverter.GetBytes((short)16));
        NativeBattleActionMatrix basis = Identity() with { X = 40000 };
        NativeBattleCinematicDetached66Motion result = NativeBattleCinematicDetached66.MoveRecord(frame, Record, basis);
        Assert.Equal((short)32, Word(result.Frame, 0xC));
        Assert.Equal(40003, result.Matrix.X);
        Assert.Equal(0, result.Matrix.Y);
        Assert.Equal(0, result.Matrix.Z);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(255, 2)]
    public void Draw_consumes_captured_cursor_retains_state_and_emits_only_nonzero_lifetimes(byte lifetime, int expectedPackets)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        uint geometry = NativeBattleEffectNodeMemory.ArenaAddress + 0x5000;
        byte[] bytes = new byte[0x100];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x14), 0x80);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x18), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x40), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x42), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x44 + 12), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x44 + 14), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x58), 0xFFFF);
        frame = frame with { Arena = frame.Arena.Write(0x5000, bytes).Write(0x4006, [lifetime]) };
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Record);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279770C, NativeBattleEffectNodeMemory.ArenaAddress + 0x50000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Payload);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 1);
        int appended = 0;
        List<uint> ordering = [];
        NativeBattleCinematicDetached66Services services = new((current, _) => new(current, geometry, bytes.Length), (current, _) => new(current, 0x27979E8, Identity(), Identity()), (current, _, vertices, _) => new(current, vertices.Select(_ => new NativeBattleModuleProjectedVertex(0, 100, 0)).ToImmutableArray(), 25, 0), (current, _, table, packets) =>
        {
            appended++;
            ordering.Add(table);
            Assert.Equal(32, packets.AllocationBytes);
            NativeBattleGfGeometryAttempt attempt = Assert.Single(packets.Attempts);
            Assert.Equal(27, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[0]);
            return current;
        });
        NativeBattleWaterExecutionFrame first = NativeBattleCinematicDetached66.Draw(frame, services);
        Assert.Equal(Record, WaterSchedulerFixture.ReadSlotDword(first, 0, 0xBC));
        Assert.Equal(Record + 16, BinaryPrimitives.ReadUInt32LittleEndian(first.Arena.Read(RecordOffset + 4, 4)));
        Assert.Equal(Record + 64, WaterSchedulerFixture.ReadCoreDword(first, 0x2797704));
        NativeBattleWaterExecutionFrame second = NativeBattleCinematicDetached66.Draw(first, services);
        Assert.Equal(Record + 64, WaterSchedulerFixture.ReadCoreDword(second, 0x2797704));
        Assert.Equal(expectedPackets, appended);
        Assert.All(ordering, value => Assert.Equal(lifetime == 1 ? 0xA5A5A5A9u : 0xA5A5A5BDu, value));
        Assert.Equal(geometry + 0x58, second.ReadTemporaryDword(0xF0));
        Assert.Equal(0x24000000u, second.ReadTemporaryDword(0xF4));
        Assert.Equal(0u, second.ReadTemporaryDword(0xF8));
        Assert.Equal(geometry + 0x80, second.ReadTemporaryDword(0xEC));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        for (int offset = 0x8C; offset <= 0x90; offset += 2)
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, offset, 256);
        }
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 256);
        byte[] payload = new byte[0x34];
        payload[6] = 3;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0xE), -7);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(0x12), 5);
        return frame with { Arena = frame.Arena.Write(0x4000, payload) };
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleWaterExecutionFrame Put(NativeBattleWaterExecutionFrame frame, int offset, byte[] bytes) => frame with { Arena = frame.Arena.Write(RecordOffset + offset, bytes) };
    private static short Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(RecordOffset + offset, 2));
    private static int Dword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt32LittleEndian(frame.Arena.Read(RecordOffset + offset, 4));
}
