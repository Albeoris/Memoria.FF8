using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativeBattleCommandQueueTests
{
    public static void Run()
    {
        Ff7BattleCommandQueueState empty = Ff7BattleCommandQueueState.Create();
        Ff7BattleCommandQueueState staged = Ff7BattleCommandQueue.StageReady(empty, 4, 0xFF);
        staged = Ff7BattleCommandQueue.StageReady(staged, 0, 0x14, 56, 16);
        Check(Ff7BattleCommandQueue.TakeNext(staged).Entry is null, "The ready ring cannot dispatch until its native promotion callback.");
        Ff7BattleCommandQueueState ready = Ff7BattleCommandQueue.PromoteReady(Restore(staged));
        Check(ready is { ReadyRead: 2, ReadyWrite: 2, LastEnqueuedPriority: 5 } && ready.Slots[0]?.Command is { Priority: 6, ActorSlot: 4, CommandId: 0xFF } && ready.Slots[1]?.Command is { Priority: 5, ActorSlot: 0, CommandId: 0x14 }, "Native staging FIFO must put the first ready actor into the lowest physical slot and retain the Limit priority.");

        Ff7BattleQueuedCommand main = Ff7BattleQueuedCommand.FromAi(new(4, 0x20, 272, 0, 1));
        Ff7BattleQueuedCommand counter = Ff7BattleQueuedCommand.FromAi(new(5, 0x20, 273, 1, 1, Callback: 2, Priority: 0));
        Ff7BattleCommandQueueState queue = Ff7BattleCommandQueue.Enqueue(Ff7BattleCommandQueue.Enqueue(ready, main), counter);
        Ff7BattleQueueSelection selection = Ff7BattleCommandQueue.TakeNext(Restore(queue), 1);
        Check(selection.Entry is { PhysicalSlot: 3, Sequence: 0, Command: { Priority: 0, Callback: 2, Argument: 1, AttackId: 273 } }, "The restricted scan must dispatch the counter's class and retain its scene index and source bank.");
        Check(Ff7BattleCommandQueue.TakeNext(selection.State, 1).Entry is null && selection.State.ReadSequences[2] == 0, "A restricted scan advanced a suspended lower priority class.");
        selection = Ff7BattleCommandQueue.TakeNext(selection.State);
        Check(selection.Entry is { PhysicalSlot: 2, Sequence: 0, Command: { Priority: 2, Callback: 1, Argument: 0, AttackId: 272 } }, "The admitted enemy attack must precede player Limit and normal ready events.");
        selection = Ff7BattleCommandQueue.TakeNext(selection.State);
        Check(selection.Entry is { PhysicalSlot: 1, Command: { Priority: 5, CommandId: 0x14 } }, "The Limit class must precede normal ready events.");
        selection = Ff7BattleCommandQueue.TakeNext(selection.State);
        Check(selection.Entry is { PhysicalSlot: 0, Command: { Priority: 6, CommandId: 0xFF } } && Ff7BattleCommandQueue.TakeNext(selection.State).Entry is null, "Consumed queue entries must not dispatch again.");
        Check(queue.Slots.Count(entry => entry is not null) == 4 && queue.ReadSequences.All(sequence => sequence == 0), "The queue must publish a replacement state and leave the saved source unchanged.");

        Ff7BattleCommandQueueState fifo = Ff7BattleCommandQueue.Enqueue(Ff7BattleCommandQueue.Enqueue(empty, main), main with { Argument = 1 });
        fifo = Ff7BattleCommandQueue.TakeNext(fifo).State;
        fifo = Ff7BattleCommandQueue.Enqueue(fifo, main with { Argument = 2 });
        Check(fifo.Slots[0] is { Sequence: 2 } && Ff7BattleCommandQueue.TakeNext(Restore(fifo)).Entry is { PhysicalSlot: 1, Sequence: 1 }, "A reused low physical slot must remain behind an older command of the same class.");

        Ff7BattleCommandQueueState wrap = Ff7BattleCommandQueueState.Create();
        wrap.ReadSequences[2] = 255;
        wrap.WriteSequences[2] = 255;
        wrap = Ff7BattleCommandQueue.Enqueue(Ff7BattleCommandQueue.Enqueue(wrap, main), main);
        selection = Ff7BattleCommandQueue.TakeNext(Restore(wrap));
        Check(selection.Entry is { Sequence: 255 } && Ff7BattleCommandQueue.TakeNext(Restore(selection.State)).Entry is { Sequence: 0 }, "Class cursors must wrap independently through byte 255 across JSON restoration.");

        Ff7BattleCommandQueueState full = empty;
        for (int index = 0; index < Ff7BattleCommandQueue.Capacity; index++)
        {
            full = Ff7BattleCommandQueue.Enqueue(full, main);
        }
        Expect<InvalidOperationException>(() => Ff7BattleCommandQueue.Enqueue(full, main));
        Check(full.WriteSequences[2] == 64, "A saturated queue must preserve the last published write cursor.");
        for (int index = 0; index < 15; index++)
        {
            empty = Ff7BattleCommandQueue.StageReady(empty, 0, 1, (ushort)index, 16);
        }
        Expect<InvalidOperationException>(() => Ff7BattleCommandQueue.StageReady(empty, 0, 1));
        Expect<InvalidOperationException>(() => Ff7BattleCommandQueue.PromoteReady(full with { Ready = empty.Ready, ReadyWrite = empty.ReadyWrite }));
        Check(empty.ReadyRead == 0 && empty.ReadyWrite == 15, "A failed promotion must preserve the staged commands.");

        Check(Ff7BattleCommandQueue.ActorConditionsAfterEnqueue(main, 0x27) == 7 && Ff7BattleCommandQueue.ActorConditionsAfterEnqueue(main with { CommandId = 0x53 }, 7) == 0x27 && Ff7BattleCommandQueue.ActorConditionsAfterEnqueue(counter, 0x27) == 0x27, "Native queue insertion must update Defend only for ordinary classes using the low six command bits.");
    }

    private static Ff7BattleCommandQueueState Restore(Ff7BattleCommandQueueState state) => JsonSerializer.Deserialize<Ff7BattleCommandQueueState>(JsonSerializer.Serialize(state))!;

    private static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception("Invalid FFVII battle command queue operation succeeded.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
