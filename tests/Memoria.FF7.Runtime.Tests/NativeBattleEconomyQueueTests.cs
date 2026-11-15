using System.Text.Json;
using Memoria.FF7.Runtime;

internal static class NativeBattleEconomyQueueTests
{
    public static void Run()
    {
        PhysicalQueueWrapAndUnreadOverwrite();
        RegistrationAndAcquisitionHaveSeparateBoundaries();
        ReturnCapacityAndAcquisitionWrap();
        ColdCopyAndMutationIsolation();
        MalformedSavedStateIsRejected();
        Console.WriteLine("PASS native saved battle economy queues");
    }

    private static void PhysicalQueueWrapAndUnreadOverwrite()
    {
        Ff7BattleEconomyState state = Create();
        for (ushort argument = 0; argument < 127; argument++)
        {
            state = Ff7BattleEconomy.Enqueue(state, 0, new(0x0B, 4, argument));
        }
        Check(state.Queues[0].Read == 0 && state.Queues[0].Write == 127, "The 128-entry command queue acquired an artificial full guard.");
        state = Ff7BattleEconomy.Enqueue(state, 0, new(0x0B, 4, 127));
        Check(state.Queues[0].Read == state.Queues[0].Write && state.Queues[0].Slots.All(command => command is not null), "Native full-ring wrap did not make unread work cursor-empty.");
        state = Ff7BattleEconomy.Dequeue(state, 0, out Ff7BattleEconomyCommand? absent);
        Check(absent is null, "A wrapped queue inferred occupancy from stale physical entries.");
        state = Ff7BattleEconomy.Enqueue(state, 0, new(0x03, 9, 500));
        state = Ff7BattleEconomy.Dequeue(state, 0, out Ff7BattleEconomyCommand? latest);
        Check(latest == new Ff7BattleEconomyCommand(0x03, 9, 500) && state.Queues[0].Read == 1 && state.Queues[0].Write == 1, "Command overwrite or dequeue lost native source/WORD argument.");
        Check(state.Queues[0].Slots[0] == latest && state.Queues[0].Slots[1]?.Argument == 1 && state.Queues[1].Slots.All(command => command is null), "Dequeue cleared stale command storage or mutated a separate queue.");
        state.Queues[2] = state.Queues[2] with { Read = 127, Write = 127 };
        state = Ff7BattleEconomy.Enqueue(state, 2, new(0x14, 8, ushort.MaxValue));
        Check(state.Queues[2].Write == 0, "The command tail did not wrap at 128.");
        state = Ff7BattleEconomy.Dequeue(state, 2, out latest);
        Check(state.Queues[2].Read == 0 && latest?.Argument == ushort.MaxValue, "The command head or generic unsigned argument was narrowed.");
    }

    private static void RegistrationAndAcquisitionHaveSeparateBoundaries()
    {
        Ff7BattleEconomyState state = Create();
        state.RegisteredReturns[1] = 8;
        state.RegisteredReturns[5] = 7;
        Ff7BattleEconomyState registered = Ff7BattleEconomy.RegisterReturn(state, 9);
        Check(state.RegisteredReturns[0] is null && registered.RegisteredReturns[0] == 9, "Return registration did not select the first physical hole independently.");
        registered = Ff7BattleEconomy.RegisterReturn(registered, 9);
        registered = Ff7BattleEconomy.TakeAcquisition(registered, out ushort? premature);
        Check(premature is null && registered.RegisteredReturns[2] == 9, "Registration merged duplicate items or acquired them before a flush.");
        Ff7BattleEconomyState flushed = Ff7BattleEconomy.FlushReturns(registered);
        Check(flushed.RegisteredReturns.All(item => item is null) && registered.RegisteredReturns[0] == 9 && flushed.AcquisitionWrite == 4, "Flush failed to retire the registered records without mutating its input.");
        foreach (ushort expected in new ushort[] { 9, 8, 9, 7 })
        {
            flushed = Ff7BattleEconomy.TakeAcquisition(flushed, out ushort? item);
            Check(item == expected, "Registered returns were reordered or merged before physical acquisition.");
        }
        Check(flushed.Acquisition[0] == 9 && flushed.Acquisition[3] == 7, "Acquisition consumption erased stale physical entries.");
        flushed = Ff7BattleEconomy.TakeAcquisition(flushed, out premature);
        Check(premature is null && flushed.Inventory.SequenceEqual(state.Inventory), "Empty acquisition fabricated inventory publication.");
    }

    private static void ReturnCapacityAndAcquisitionWrap()
    {
        Ff7BattleEconomyState state = Create();
        for (ushort item = 0; item < 16; item++)
        {
            state = Ff7BattleEconomy.RegisterReturn(state, item);
        }
        state = Ff7BattleEconomy.RegisterReturn(state, 77);
        Check(state.RegisteredReturns.Select(item => item!.Value).SequenceEqual(Enumerable.Range(0, 16).Select(item => (ushort)item)), "A full return directory overwrote or grew its sixteen records.");
        state = Ff7BattleEconomy.FlushReturns(state);
        Check(state.AcquisitionRead == 0 && state.AcquisitionWrite == 0 && state.Acquisition.All(item => item is not null), "The sixteen-entry acquisition ring gained a full guard.");
        state = Ff7BattleEconomy.TakeAcquisition(state, out ushort? absent);
        Check(absent is null, "Full acquisition wrap was incorrectly treated as queued work.");
        state = Ff7BattleEconomy.FlushReturns(Ff7BattleEconomy.RegisterReturn(state, 42));
        state = Ff7BattleEconomy.TakeAcquisition(state, out ushort? latest);
        Check(latest == 42 && state.AcquisitionRead == 1 && state.AcquisitionWrite == 1 && state.Acquisition[15] == 15, "A wrapped acquisition failed to overwrite its physical tail or preserve stale slots.");
        state = state with { AcquisitionRead = 15, AcquisitionWrite = 15 };
        state = Ff7BattleEconomy.FlushReturns(Ff7BattleEconomy.RegisterReturn(state, 55));
        state = Ff7BattleEconomy.TakeAcquisition(state, out latest);
        Check(latest == 55 && state.AcquisitionRead == 0 && state.AcquisitionWrite == 0, "The acquisition cursors did not wrap at sixteen.");
    }

    private static void ColdCopyAndMutationIsolation()
    {
        Ff7BattleInventorySlot[] inventory = Inventory();
        Ff7BattleEconomyState state = Ff7BattleEconomy.Create(inventory);
        inventory[0] = new(0, 2, 0);
        Check(state.Inventory[0].ItemId == ushort.MaxValue, "Creation retained the caller's mutable working inventory array.");
        state.Inventory[0] = new(3, 4, 0);
        state = Ff7BattleEconomy.Enqueue(state, 1, new(2, 4, 3));
        state = Ff7BattleEconomy.RegisterReturn(state, 3) with { TerminalPasses = 16 };
        string json = JsonSerializer.Serialize(state);
        Ff7BattleEconomyState cold = JsonSerializer.Deserialize<Ff7BattleEconomyState>(json)!;
        Ff7BattleEconomy.Validate(cold);
        Check(JsonSerializer.Serialize(Ff7BattleEconomy.FlushReturns(state)) == JsonSerializer.Serialize(Ff7BattleEconomy.FlushReturns(cold)), "JSON restoration changed registered-return continuation or saved cursors.");
        Ff7BattleEconomyState dequeued = Ff7BattleEconomy.Dequeue(cold, 1, out Ff7BattleEconomyCommand? command);
        Check(command == new Ff7BattleEconomyCommand(2, 4, 3), "JSON restoration lost a pending command.");
        Ff7BattleEconomyState copy = Ff7BattleEconomy.Copy(state);
        copy.Inventory[0] = new(4, 1, 0);
        copy.Queues[1].Slots[0] = null;
        copy.Queues[0] = copy.Queues[0] with { Read = 7, Write = 7 };
        copy.RegisteredReturns[0] = 99;
        copy.Acquisition[0] = 88;
        dequeued.Inventory[0] = new(5, 1, 0);
        Check(JsonSerializer.Serialize(state) == json && JsonSerializer.Serialize(cold) == json, "Economy operations retained mutable arrays from another saved state.");
    }

    private static void MalformedSavedStateIsRejected()
    {
        Ff7BattleEconomyState state = Create();
        Reject(state with { Queues = [] });
        Reject(state with { RegisteredReturns = new ushort?[15] });
        Reject(state with { Acquisition = new ushort?[17] });
        Reject(state with { AcquisitionRead = 16 });
        Reject(state with { AcquisitionWrite = 16 });
        Reject(state with { AcquisitionWrite = 1 });
        Reject(state with { TerminalPasses = 17 });
        Ff7BattleEconomyState badQueue = Ff7BattleEconomy.Copy(state);
        badQueue.Queues[0] = new(new Ff7BattleEconomyCommand?[128], 0, 1);
        Reject(badQueue);
        badQueue.Queues[0] = new(new Ff7BattleEconomyCommand?[128], 128, 0);
        Reject(badQueue);
        badQueue.Queues[0] = new(new Ff7BattleEconomyCommand?[127], 0, 0);
        Reject(badQueue);
        badQueue.Queues[0] = new(new Ff7BattleEconomyCommand?[128], 0, 0);
        badQueue.Queues[0].Slots[127] = new(255, 10, 0);
        Reject(badQueue);
        Ff7BattleEconomyState badItem = Ff7BattleEconomy.Copy(state);
        badItem.RegisteredReturns[0] = ushort.MaxValue;
        Reject(badItem);
        badItem = Ff7BattleEconomy.Copy(state);
        badItem.Acquisition[15] = ushort.MaxValue;
        Reject(badItem);
        Ff7BattleEconomyState badInventory = Ff7BattleEconomy.Copy(state);
        badInventory.Inventory[0] = new(ushort.MaxValue, 1, 0x0B);
        Reject(badInventory);
        Ff7BattleEconomy.Validate(state);
    }

    private static Ff7BattleEconomyState Create()
    {
        return Ff7BattleEconomy.Create(Inventory());
    }

    private static Ff7BattleInventorySlot[] Inventory()
    {
        return Enumerable.Range(0, 320).Select(_ => new Ff7BattleInventorySlot(ushort.MaxValue, 0, 0x0B)).ToArray();
    }

    private static void Reject(Ff7BattleEconomyState state)
    {
        try
        {
            Ff7BattleEconomy.Validate(state);
        }
        catch (InvalidDataException)
        {
            return;
        }
        throw new InvalidOperationException("Malformed battle economy state was accepted.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
