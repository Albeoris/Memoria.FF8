using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterDrawDispatcherTests
{
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x800, 8, 0x2000);

    [Fact]
    public void Scroll_publishes_actual_counter_and_full_ring_rectangle_before_each_enqueue()
    {
        var source = ScrollSource();
        NativeBattleWaterExecutionFrame initial = WaterSchedulerFixture.Create();
        initial = WaterSchedulerFixture.SlotDword(initial, 0, 0xB8, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        initial = WaterSchedulerFixture.SlotWord(initial, 0, 0x96, 1);
        initial = initial with { Arena = initial.Arena.Write(0x5000, new byte[] { 0, 0, 0, 2, 0, 1 }), Core = initial.Core.Write(0x27970AB, new byte[] { 0xFF }) };
        List<int> indices = [];
        NativeBattleWaterDrawServices services = UnusedServices() with
        {
            ReadCurrentFiles = _ => source.Files,
            EnqueueScroll = (current, write) =>
            {
                Assert.Equal(write.NextCounter, current.Core.Read(0x27970AB, 1)[0]);
                Assert.Equal(write.RectangleBytes, current.Core.Read(0x27974D4 + (uint)(write.RingIndex * 8), 8).ToImmutableArray());
                indices.Add(write.RingIndex);
                return current.WriteTemporaryByte(0x10 + indices.Count, (byte)indices.Count);
            }
        };
        NativeBattleWaterDrawInputs input = new(new(source.Files, source.Profile), 0x155, 0, 0, 0);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterDrawDispatcher.Draw(initial, 0x15, Bank, 0, input, services);
        Assert.Equal(new[] { 15, 0 }, indices);
        Assert.Equal((byte)1, result.Core.Read(0x27970AB, 1)[0]);
        Assert.Equal((byte)1, result.ReadTemporaryByte(0x11));
        Assert.Equal((byte)2, result.ReadTemporaryByte(0x12));
        Assert.Equal(new NativeCrtRandomState(1), result.Random);
        Assert.Equal((short)3, BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(0x279754C + 6, 2)));
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(result.Core.Read(0x27974D4 + 6, 2)));
        indices.Clear();
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(initial))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleWaterDrawDispatcher.Draw(restored, 0x15, Bank, 0, input, services)));
    }

    [Fact]
    public void Rectangle_terminal_does_not_resolve_projection_packet_cursor_or_unrelated_owners()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD0, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xE4, NativeBattleEffectNodeMemory.ArenaAddress + 0x5000);
        frame = frame with { Arena = frame.Arena.Write(0x5000, new byte[] { 0, 0, 0, 0, 0, 4, 1, 0 }), Core = frame.Core.Write(0x2797554, new byte[] { 0, 0xFF }) };
        frame = frame.WriteTemporaryDword(0, 9);
        NativeBattleWaterExecutionFrame result = NativeBattleWaterDrawDispatcher.Draw(frame, 5, Bank, 0, null!, UnusedServices());
        Assert.Equal((byte)0, result.Arena.Read(0x201C, 1)[0]);
        Assert.Equal((byte)0xFF, result.Core.Read(0x2797554, 1)[0]);
        Assert.Equal(8u, result.ReadTemporaryDword(0));
        Assert.Equal(frame.Random, result.Random);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(0xFF)]
    public void Unsupported_draw_kinds_fail_before_consuming_any_owner(byte kind)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterDrawDispatcher.Draw(null!, kind, null!, 0, null!, null!));
    }

    private static NativeBattleWaterDrawServices UnusedServices() => new(
        _ => throw new InvalidOperationException("Unexpected projection read."),
        (_, _) => throw new InvalidOperationException("Unexpected focal publication."),
        (_, _) => throw new InvalidOperationException("Unexpected shape extent read."),
        _ => throw new InvalidOperationException("Unexpected depth read."),
        (_, _) => throw new InvalidOperationException("Unexpected depth publication."),
        (_, _, _) => throw new InvalidOperationException("Unexpected packet submission."),
        _ => throw new InvalidOperationException("Unexpected file read."),
        (_, _) => throw new InvalidOperationException("Unexpected scroll enqueue."),
        _ => throw new InvalidOperationException("Unexpected actor read."),
        (_, _) => throw new InvalidOperationException("Unexpected actor publication."),
        new((_, _) => throw new InvalidOperationException("Unexpected local pose read."), (_, _, _) => throw new InvalidOperationException("Unexpected bone publication.")),
        (_, _, _) => throw new InvalidOperationException("Unexpected metadata read."),
        _ => throw new InvalidOperationException("Unexpected ordering table read."));

    private static (NativeBattleWater222Profile Profile, ImmutableArray<NativeBattleEffectHeapAllocation> Files) ScrollSource()
    {
        byte[] tables = new byte[0x70];
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(4), 2);
        BinaryPrimitives.WriteInt16LittleEndian(tables.AsSpan(6), 4);
        tables[80] = 0x80;
        byte[] file = new byte[0x110];
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x14), 0x40);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(0x40), 0xC0);
        return (new(new("tables", tables.ToImmutableArray()), new("initialization", new byte[0x1C].ToImmutableArray()), new("owners", new byte[0x14].ToImmutableArray())), [new("current0", file.ToImmutableArray()), new("current1", [])]);
    }
}
