namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleEconomyCommand(byte Command, byte Source, ushort Argument);
public sealed record Ff7BattleEconomyQueue(Ff7BattleEconomyCommand?[] Slots, byte Read, byte Write);
public sealed record Ff7BattleEconomyState(Ff7BattleInventorySlot[] Inventory, Ff7BattleEconomyQueue[] Queues, ushort?[] RegisteredReturns, ushort?[] Acquisition, byte AcquisitionRead = 0, byte AcquisitionWrite = 0)
{
    public byte TerminalPasses { get; init; }
}

/// <summary>Saved physical economy queues; callers own execution, timing and acquisition policy.</summary>
public static class Ff7BattleEconomy
{
    public static Ff7BattleEconomyState Create(IReadOnlyList<Ff7BattleInventorySlot> inventory)
    {
        Ff7NativeBattleInventory.Validate(inventory);
        return new(inventory.ToArray(), Enumerable.Range(0, 3).Select(_ => new Ff7BattleEconomyQueue(new Ff7BattleEconomyCommand?[128], 0, 0)).ToArray(), new ushort?[16], new ushort?[16]);
    }

    public static Ff7BattleEconomyState Copy(Ff7BattleEconomyState state)
    {
        Validate(state);
        return state with
        {
            Inventory = state.Inventory.ToArray(),
            Queues = state.Queues.Select(queue => queue with { Slots = queue.Slots.ToArray() }).ToArray(),
            RegisteredReturns = state.RegisteredReturns.ToArray(),
            Acquisition = state.Acquisition.ToArray()
        };
    }

    public static void Validate(Ff7BattleEconomyState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Inventory is null || state.Queues is null || state.Queues.Length != 3 || state.RegisteredReturns is null || state.RegisteredReturns.Length != 16 || state.Acquisition is null || state.Acquisition.Length != 16 || state.AcquisitionRead >= 16 || state.AcquisitionWrite >= 16 || state.TerminalPasses > 16)
        {
            throw new InvalidDataException("The saved battle economy has invalid physical storage or cursors.");
        }
        Ff7NativeBattleInventory.Validate(state.Inventory);
        foreach (Ff7BattleEconomyQueue queue in state.Queues)
        {
            if (queue is null || queue.Slots is null || queue.Slots.Length != 128 || queue.Read >= 128 || queue.Write >= 128 || queue.Slots.Any(command => command is not null && command.Source >= 10))
            {
                throw new InvalidDataException("A saved battle economy queue has invalid physical storage, cursors or actor sources.");
            }
            for (int index = queue.Read; index != queue.Write; index = (index + 1) & 127)
            {
                if (queue.Slots[index] is null)
                {
                    throw new InvalidDataException("A pending battle economy command has no physical queue entry.");
                }
            }
        }
        if (state.RegisteredReturns.Any(item => item == ushort.MaxValue) || state.Acquisition.Any(item => item == ushort.MaxValue))
        {
            throw new InvalidDataException("A saved battle return stores the empty-item sentinel as an item.");
        }
        for (int index = state.AcquisitionRead; index != state.AcquisitionWrite; index = (index + 1) & 15)
        {
            if (state.Acquisition[index] is null)
            {
                throw new InvalidDataException("A pending battle acquisition has no physical ring entry.");
            }
        }
    }

    public static Ff7BattleEconomyState Enqueue(Ff7BattleEconomyState state, int queue, Ff7BattleEconomyCommand command)
    {
        ValidateQueueIndex(queue);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Source >= 10)
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }
        Ff7BattleEconomyState result = Copy(state);
        Ff7BattleEconomyQueue selected = result.Queues[queue];
        selected.Slots[selected.Write] = command;
        result.Queues[queue] = selected with { Write = (byte)((selected.Write + 1) & 127) };
        return result;
    }

    public static Ff7BattleEconomyState Dequeue(Ff7BattleEconomyState state, int queue, out Ff7BattleEconomyCommand? command)
    {
        ValidateQueueIndex(queue);
        Ff7BattleEconomyState result = Copy(state);
        Ff7BattleEconomyQueue selected = result.Queues[queue];
        command = null;
        if (selected.Read != selected.Write)
        {
            command = selected.Slots[selected.Read];
            result.Queues[queue] = selected with { Read = (byte)((selected.Read + 1) & 127) };
        }
        return result;
    }

    public static Ff7BattleEconomyState RegisterReturn(Ff7BattleEconomyState state, ushort item)
    {
        if (item == ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(item));
        }
        Ff7BattleEconomyState result = Copy(state);
        int empty = Array.FindIndex(result.RegisteredReturns, entry => entry is null);
        if (empty >= 0)
        {
            result.RegisteredReturns[empty] = item;
        }
        return result;
    }

    public static Ff7BattleEconomyState FlushReturns(Ff7BattleEconomyState state)
    {
        Ff7BattleEconomyState result = Copy(state);
        byte write = result.AcquisitionWrite;
        for (int index = 0; index < result.RegisteredReturns.Length; index++)
        {
            if (result.RegisteredReturns[index] is ushort item)
            {
                result.Acquisition[write] = item;
                write = (byte)((write + 1) & 15);
                result.RegisteredReturns[index] = null;
            }
        }
        return result with { AcquisitionWrite = write };
    }

    public static Ff7BattleEconomyState TakeAcquisition(Ff7BattleEconomyState state, out ushort? item)
    {
        Ff7BattleEconomyState result = Copy(state);
        item = null;
        if (result.AcquisitionRead != result.AcquisitionWrite)
        {
            item = result.Acquisition[result.AcquisitionRead];
            result = result with { AcquisitionRead = (byte)((result.AcquisitionRead + 1) & 15) };
        }
        return result;
    }

    private static void ValidateQueueIndex(int queue)
    {
        if ((uint)queue >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(queue));
        }
    }
}
