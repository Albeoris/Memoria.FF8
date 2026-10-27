using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshCallbackOwnershipTests
{
    [Fact]
    public void Same_owner_callback_cannot_bypass_completion_byte_guard_and_rejected_writes_roll_back()
    {
        NativeBattleModulePoolTask task = new(1, 0xBEEF, null, 0x596B30, Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray());
        task.WriteWord(12, 1).WriteWord(14, 0xA500);
        byte[] before = task.ReadRawPayloadSpan().ToArray();
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current.WriteWord(14, 1)));
        Assert.Equal(before, task.ReadRawPayloadSpan().ToArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current.WriteWord(16, 1)));
        Assert.Equal(before, task.ReadRawPayloadSpan().ToArray());
        Assert.Throws<InvalidOperationException>(() => NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) =>
        {
            current.WriteByte(14, 1);
            throw new InvalidOperationException("callback interrupted");
        }));
        Assert.Equal(before, task.ReadRawPayloadSpan().ToArray());
        NativeBattleGilgameshCommonStep accepted = NativeBattleGilgameshCommonTasks.Step596B30(task, 0, 0, (_, _, current) => current.WriteByte(14, 1));
        Assert.Same(task, accepted.UpdatedTask);
        Assert.Equal(2, accepted.Result);
        Assert.Equal((ushort)2, task.ReadWord(12));
        Assert.Equal((ushort)0xA501, task.ReadWord(14));
    }
}
