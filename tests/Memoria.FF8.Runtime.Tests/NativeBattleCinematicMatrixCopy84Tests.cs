using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicMatrixCopy84Tests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Pc = 0x80001000;
    private const uint Matrices = 0x27979E8;

    [Fact]
    public void Zero_displacements_allocate_each_actual_node_row_and_propagate_unknown_padding_without_a_semantic_read()
    {
        NativeBattleWaterExecutionFrame frame = Fixture(0, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x12, 7);
        frame = WaterSchedulerFixture.SlotWord(frame, 1, 0x12, 8);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797206, 8);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797208, 7);
        frame = frame with { Core = frame.Core.Write(Matrices + 32, Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()).MarkUnknown(Matrices + 50, 2).MarkUnknown(Matrices + 64, 32) };
        NativeBattleWaterExecutionFrame result = Run(frame, Base + 0x2100);
        var source = frame.Core.ReadSnapshot(Matrices + 32, 32);
        var actual = result.Core.ReadSnapshot(Matrices + 64, 32);
        Assert.Equal(source.Bytes, actual.Bytes);
        Assert.Equal(source.Known, actual.Known);
        Assert.Equal(Base + 0x2000, WaterSchedulerFixture.ReadCoreDword(result, 0x27973E8));
        Assert.Equal(Pc + 8, WaterSchedulerFixture.ReadCoreDword(result, 0x2797450));
        Assert.Same(frame.TemporaryFrame, result.TemporaryFrame);
        Assert.Same(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(32, 36)]
    [InlineData(-32, -28)]
    public void Signed_literal_byte_offsets_copy_four_native_eight_byte_groups_with_forward_overlap_and_known_masks(short sourceOffset, short destinationOffset)
    {
        NativeBattleWaterExecutionFrame frame = Fixture(sourceOffset, destinationOffset);
        int source = 0x2000 + sourceOffset;
        int destination = 0x2000 + destinationOffset;
        frame = frame with { Arena = frame.Arena.Write(source, Enumerable.Range(0, 36).Select(value => (byte)value).ToArray()).MarkUnknown(source + 6, 2) };
        NativeBattleWaterExecutionFrame result = Run(frame, Base + 0x2000);
        var actual = result.Arena.ReadSnapshot(destination, 32);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 4, 5, 6, 7, 12, 13, 14, 15, 12, 13, 14, 15, 20, 21, 22, 23, 20, 21, 22, 23, 28, 29, 30, 31 }, actual.Data);
        Assert.Equal(new[] { 6, 7, 10, 11 }, Enumerable.Range(0, 32).Where(index => !actual.Known[index]));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Run(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!, Base + 0x2000)));
    }

    private static NativeBattleWaterExecutionFrame Run(NativeBattleWaterExecutionFrame frame, uint selected) => NativeBattleCinematicMatrixCopy84.Execute(frame, new(0x1000, 8, Pc), (_, selector) =>
    {
        Assert.Equal((short)-1, selector);
        return selected;
    });

    private static NativeBattleWaterExecutionFrame Fixture(short source, short destination)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreDword(WaterSchedulerFixture.Create(), 0x2797450, Pc);
        byte[] program = [0x84, 0xF0, 0xFF, 0xFF, 0, 0, 0, 0];
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(4), source);
        BinaryPrimitives.WriteInt16LittleEndian(program.AsSpan(6), destination);
        return frame with { Arena = frame.Arena.Write(0x1000, program) };
    }
}
