using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommonPacketTransactionTests
{
    [Theory]
    [InlineData("off_B6D070", 0, 114688)]
    [InlineData("off_B6D070", 1, 114688)]
    [InlineData("off_B6D094", 0, 147456)]
    [InlineData("off_B6D094", 1, 147456)]
    public void Ordered_masks_crossing_physical_slices_freeze_to_the_identical_immutable_representation(string bank, byte buffer, int capacity)
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { PacketBank = bank, PacketBuffer = buffer };
        string original = JsonSerializer.Serialize(source);
        NativeBattlePrimitiveProcess expected = source;
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess actual = transaction.View;
        Random random = new(37);
        for (int index = 0; index < 80; index++)
        {
            int offset = buffer == 0 && index % 3 == 0 ? capacity - 8 : random.Next(128);
            byte[] bytes = new byte[16];
            random.NextBytes(bytes);
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, bytes.Length).Select(_ => (NativeBattleGfPacketByteState)random.Next(3)).ToImmutableArray();
            expected = expected.ApplyCommonWrite(offset, bytes.ToImmutableArray(), mask);
            actual = actual.ApplyCommonWrite(offset, bytes.ToImmutableArray(), mask);
        }
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(transaction.Freeze(actual)));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Null(transaction.Freeze(actual).CommonTransaction);
    }

    [Fact]
    public void Read_through_and_failed_callback_rollback_preserve_completed_stores_and_previous_snapshots()
    {
        NativeBattlePrimitiveProcess source = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400().ApplyCommonWrite(0, [1, 2, 3, 4], Known(4));
        string original = JsonSerializer.Serialize(source);
        using NativeBattleCommonPacketTransaction transaction = new(source);
        NativeBattlePrimitiveProcess actual = transaction.View;
        transaction.BeginCheckpoint();
        actual = actual.ApplyCommonWrite(1, [7], Known(1));
        transaction.CommitCheckpoint();
        NativeBattlePrimitiveProcess completed = transaction.Freeze(actual);
        string retained = JsonSerializer.Serialize(completed);
        transaction.BeginCheckpoint();
        actual = actual.ApplyCommonWrite(0, [9], Known(1));
        Assert.Equal(new byte[] { 9, 7, 3, 4 }, actual.ReadCommonPacket(0, 4));
        actual = actual.MarkCommonUnknown(2, 1);
        Assert.Throws<NotSupportedException>(() => actual.ReadCommonPacket(0, 4));
        transaction.RollbackCheckpoint();
        Assert.Equal(new byte[] { 1, 7, 3, 4 }, actual.ReadCommonPacket(0, 4));
        Assert.Equal(retained, JsonSerializer.Serialize(transaction.Freeze(actual)));
        Assert.Equal(retained, JsonSerializer.Serialize(completed));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        transaction.Dispose();
        Assert.Throws<ObjectDisposedException>(() => actual.ReadCommonPacket(0, 1));
    }

    private static ImmutableArray<NativeBattleGfPacketByteState> Known(int count) => Enumerable.Repeat(NativeBattleGfPacketByteState.Known, count).ToImmutableArray();
}
