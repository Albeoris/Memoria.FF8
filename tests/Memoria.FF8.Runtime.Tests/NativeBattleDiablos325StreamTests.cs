using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325StreamTests
{
    [Theory]
    [InlineData(162)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void Actual_leading_DWORD_overwrites_both_stream_globals_and_acknowledges_task_without_reading_tail(int number)
    {
        var fixture = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(fixture.Frame, fixture.Packets, fixture.Models);
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, number);
        frame = frame with { Memory = frame.Memory with { SummonStream = null, Arena = frame.Memory.Arena!.Write(0x1685C, bytes).MarkUnknown(0x16860, 32) } };
        NativeBattleModulePoolTask task = new(1, 0, null, 0x6545F0, new byte[0x994 - 12].ToImmutableArray());
        task = task.WriteByte(0xF, 9);
        string original = JsonSerializer.Serialize(frame);
        NativeBattleDiablos325StreamStep result = NativeBattleDiablos325Stream.Initialize655488(frame, task);
        Assert.Equal(new NativeBattleSummonStreamState(unchecked(number + 690000), NativeBattleEffectNodeMemory.ArenaAddress + 0x1685Cu), result.Frame.Memory.SummonStream);
        Assert.Equal((byte)1, result.Task.ReadByte(0xF));
        Assert.Equal((byte)9, task.ReadByte(0xF));
        Assert.Equal(original, JsonSerializer.Serialize(frame));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(0x16860, 1));
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.MarkUnknown(0x1685C, 1) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleDiablos325Stream.Initialize655488(frame, task));
    }
}
