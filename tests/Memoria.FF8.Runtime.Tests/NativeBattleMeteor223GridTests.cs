using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeteor223GridTests
{
    private const uint Arena = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;

    [Theory]
    [InlineData(64, 4)]
    [InlineData(0, 0)]
    public void Cylindrical_grid_preserves_ordered_future_stores_and_native_cursor_when_depth_rejects(int depth, int linked)
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        NativeBattleGfGeometryBatch? packets = null;
        int projected = 0;
        NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame current) => NativeBattleMeteor223Grid.Draw(current, new((_, _) => Arena + 0x4000, (prepared, _) => prepared, (prepared, _) => prepared.WriteTemporaryDword(0xF0, Arena + 0x5000).WriteTemporaryDword(0xF4, 0).WriteTemporaryDword(0xF8, 0xE2000000), prepared =>
        {
            projected++;
            uint screen = prepared.ReadTemporaryWord(0xC0) | ((uint)prepared.ReadTemporaryWord(0xC2) << 16);
            return new(prepared, screen, depth, 0);
        }, (prepared, start, batch) =>
        {
            Assert.Equal(Arena + 0x8000, start);
            packets = batch;
            return prepared;
        }, (_, _) => throw new InvalidOperationException()));
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame));
        NativeBattleWaterExecutionFrame result = Draw(frame);
        Assert.Equal(9, projected);
        Assert.Equal(4 * 0x44, packets!.AllocationBytes);
        Assert.True(packets.NativeUnlinkedAllocation);
        Assert.Equal(linked, packets.Attempts.Count(attempt => attempt.Primitive is not null));
        Assert.Contains(packets.Attempts, attempt => attempt.ByteOffsetRelative >= packets.AllocationBytes && attempt.Primitive is null);
        Assert.Equal(Arena + 0x8000 + 4 * 0x44, WaterSchedulerFixture.ReadCoreDword(result, Manager + 0x7C));
        Assert.Equal(uint.MaxValue, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xC4));
        Assert.Equal(0x27971E4u, WaterSchedulerFixture.ReadSlotDword(result, 0, 0xBC));
        string packetJson = NativeBattleTestSnapshots.Json(packets);
        Assert.Equal(NativeBattleTestSnapshots.Json(result), NativeBattleTestSnapshots.Json(Draw(restored)));
        Assert.Equal(packetJson, NativeBattleTestSnapshots.Json(packets));
    }

    [Fact]
    public void Unwritten_native_stack_parameter_is_an_explicit_boundary()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.Write(0x300C, new byte[] { 0, 0 }) };
        Assert.Throws<NotSupportedException>(() => NativeBattleMeteor223Grid.Draw(frame, new((_, _) => Arena + 0x4000, (prepared, _) => prepared, (prepared, _) => prepared.WriteTemporaryDword(0xF0, Arena + 0x5000).WriteTemporaryDword(0xF4, 0).WriteTemporaryDword(0xF8, 0), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException())));
    }

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27973B8, frame.TemporaryFrame.Address + 0x100);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, Arena + 0x6000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, Arena + 0x8000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, Arena + 0x3000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x112233);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 64);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 128);
        byte[] parameters = new byte[14];
        short[] values = [2, 1, 2, -1, 0, 0, 1];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(parameters.AsSpan(2 * index), values[index]);
        }
        return frame with { Arena = frame.Arena.Write(0x3000, parameters).Write(0x5004, BitConverter.GetBytes((short)4)).Write(0x5006, BitConverter.GetBytes((short)4)) };
    }
}
