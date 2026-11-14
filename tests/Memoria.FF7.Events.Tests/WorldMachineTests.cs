using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

internal static class WorldMachineTests
{
    public static void Run()
    {
        AliasesAndArithmetic();
        SchedulerAndColdRestore();
        MeshPreemption();
        AttachAndReplaceModel();
        DeleteModelAndPool();
        VehicleOperationOperands();
        NativeReservedWorldOpcode();
        ChocoboPathWaitContracts();
        CorruptionAndUnsupported();
        Console.WriteLine("PASS world VM bank aliases, signed operands, native scheduler, cold snapshot and corruption");
    }

    private static void AliasesAndArithmetic()
    {
        ushort[] code = [0, 0x11B, 0, 0x110, 255, 0xE0, 0x100, 0x11A, 0, 0x303, 0x11C, 0, 0x110, 1, 0x41, 0x303, 0x114, 32, 0x110, 1, 0xE0, 0x100, 0x203];
        byte[] persistent = new byte[1280];
        persistent[0] = 254;
        persistent[1] = 255;
        Host host = new();
        WorldMachine machine = new(new(new([new(0, 1)], code)), persistent, host);
        machine.StartSystem(0);
        machine.Tick();
        Check(host.Requests.Count == 2 && host.Requests[0].Command.Arguments[0] == 255 && host.Requests[1].Command.Arguments[0] == -3, "Local banks must alias and signed short arithmetic must retain native order.");
        Check(persistent[4] == 1 && machine.CopyPersistent()[4] == 1, "World bank zero must alias the caller's persistent variable storage.");
    }

    private static void SchedulerAndColdRestore()
    {
        ushort[] code = [0, 0x110, 3, 0x300, 0x203, 0x110, 2, 0x305, 0x306, 0x203, 0x110, 17, 0x303, 0x203];
        PcWorldEvents events = new([new(0, 1), new(0x4300, 5), new(0x4302, 10)], code);
        Host originalHost = new();
        WorldMachine original = new(new(events), new byte[1280], originalHost);
        original.StartSystem(0);
        original.Tick();
        Check(originalHost.Requests.Count == 2 && originalHost.Requests[0].ExecutingModel == -1 && originalHost.Requests[0].ActiveModel == 3 && originalHost.Requests[1].ExecutingModel == 3, "Allocated models must run after the system in native prepend order.");
        WorldSnapshot snapshot = original.Capture();
        Host restoredHost = new();
        WorldMachine restored = new(new(events), original.CopyPersistent(), restoredHost);
        restored.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Check(JsonSerializer.Serialize(snapshot) == JsonSerializer.Serialize(restored.Capture()), "Cold restore must preserve PCs, stack, counters and request sequence without execution.");
        originalHost.Requests.Clear();
        for (int i = 0; i < 3; i++)
        {
            original.Tick();
            restored.Tick();
        }
        Check(JsonSerializer.Serialize(original.Capture()) == JsonSerializer.Serialize(restored.Capture()) && JsonSerializer.Serialize(originalHost.Requests) == JsonSerializer.Serialize(restoredHost.Requests), "Cold continuation must execute identical native scheduler operations.");
        Check(restoredHost.Requests.Count(value => value.Command.Operation == WorldOperation.Move) == 1 && restoredHost.Requests.Any(value => value.Command.Operation == WorldOperation.Speed && value.Command.Arguments[0] == 17), "Move counters must yield once per native tick before on_tick starts.");
    }

    private static void CorruptionAndUnsupported()
    {
        WorldMachine machine = new(new(new([new(0, 1)], new ushort[] { 0, 0x203 })), new byte[1280], new Host());
        WorldSnapshot snapshot = machine.Capture();
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { ProgramIdentity = "different" }));
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { Local = new byte[63] }));
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { Stack = new WorldStackValue[5] }));
        Throws<InvalidDataException>(() => machine.Restore(snapshot with { System = snapshot.System with { ProgramCounter = 2 } }));
        WorldMachine unsupported = new(new(new([new(0, 1)], new ushort[] { 0, 0x356 })), new byte[1280], new Host());
        unsupported.StartSystem(0);
        WorldSnapshot beforeFailure = unsupported.Capture();
        Throws<NotSupportedException>(unsupported.Tick);
        Throws<InvalidOperationException>(() => unsupported.Capture());
        Throws<InvalidOperationException>(unsupported.Tick);
        unsupported.Restore(beforeFailure);
        Check(JsonSerializer.Serialize(unsupported.Capture()) == JsonSerializer.Serialize(beforeFailure), "A failed tick may continue only from its previous completed native snapshot.");
        WorldMachine underflow = new(new(new([new(0, 1)], new ushort[] { 0, 0x303 })), new byte[1280], new Host());
        underflow.StartSystem(0);
        Throws<InvalidDataException>(underflow.Tick);
    }

    private static void MeshPreemption()
    {
        ushort[] code = [0, 0x110, 2, 0x305, 0x306, 0x203, 0x110, 33, 0x303, 0x203];
        PcWorldEvents events = new([new(0, 1), new(0x86E4, 6)], code);
        Host host = new();
        WorldMachine machine = new(new(events), new byte[1280], host);
        machine.StartSystem(0);
        machine.Tick();
        machine.StartMesh(2, 3, 4);
        WorldSnapshot suspended = machine.Capture();
        Check(suspended.System.Priority == 3 && suspended.System.Calls is [{ ProgramCounter: 4, MoveCounter: 1, Priority: 0 }], "Native mesh dispatch must preempt with priority3 and suspend the exact move counter/PC.");
        host.Requests.Clear();
        machine.Tick();
        Check(host.Requests.Count == 2 && host.Requests[0].Command.Operation == WorldOperation.Speed && host.Requests[1].Command.Operation == WorldOperation.Move && machine.Capture().System.ProgramCounter == 0, "Nested RET must restore and continue the original script in the same native pass.");
    }

    private static void AttachAndReplaceModel()
    {
        ushort[] code = [0, 0x110, 0, 0x300, 0x203, 0x110, 3, 0x343, 0x110, 0x105, 0x346, 0x110, 7, 0x303, 0x203, 0x110, 99, 0x303, 0x203, 0x110, 11, 0x303, 0x203];
        PcWorldEvents events = new([new(0, 1), new(0x4000, 5), new(0x4300, 15), new(0x4502, 19)], code);
        Host host = new();
        WorldMachine machine = new(new(events), new byte[1280], host);
        machine.StartSystem(0);
        machine.Tick();
        Check(host.Requests.Count == 4, "Native attachment allocates without invoking the replacement model's on_load or a second create command.");
        Check(host.Requests[1] is { ExecutingModel: 0, ActiveModel: 3, Command.Operation: WorldOperation.AttachRider } && host.Requests[1].Command.Arguments.SequenceEqual(new[] { 0, 3 }), "Opcode343 detaches the old active model and selects its attachment transport while retaining the executing script owner.");
        Check(host.Requests[2] is { ExecutingModel: 0, ActiveModel: 3, Command.Operation: WorldOperation.ChangeModelIdentity } && host.Requests[2].Command.Arguments.SequenceEqual(new[] { 5 }), "Opcode346 writes the byte model identity on the currently active object.");
        Check(host.Requests[3] is { ExecutingModel: 0, ActiveModel: 5, Command.Operation: WorldOperation.Speed } && host.Requests[3].Command.Arguments[0] == 7, "Instructions after identity replacement continue in the same callback against the renamed object.");
        WorldSnapshot snapshot = machine.Capture();
        Check(snapshot.Models is [{ Model: 5, ProgramCounter: 0, Detached: false }, { Model: 0, ProgramCounter: 0, Detached: true }], "Allocation prepends a transport without starting on_load and preserves the detached rider node.");
        Host coldHost = new();
        WorldMachine cold = new(new(events), machine.CopyPersistent(), coldHost);
        cold.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(snapshot))!);
        host.Requests.Clear();
        machine.Tick();
        cold.Tick();
        Check(JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()) && JsonSerializer.Serialize(host.Requests) == JsonSerializer.Serialize(coldHost.Requests), "Detached and renamed script contexts cold-continue identically without replaying attachment.");
        Check(host.Requests.Count == 0, "An allocated attachment has no script PC and must not invent a periodic script callback.");

        ushort[] renameCode = [0, 0x110, 0, 0x300, 0x203, 0x110, 5, 0x346, 0x203, 0x110, 11, 0x303, 0x203];
        Host renameHost = new();
        WorldMachine renamed = new(new(new([new(0, 1), new(0x4000, 5), new(0x4502, 9)], renameCode)), new byte[1280], renameHost);
        renamed.StartSystem(0);
        renamed.Tick();
        renameHost.Requests.Clear();
        renamed.Tick();
        Check(renameHost.Requests is [{ ExecutingModel: 5, ActiveModel: 5, Command.Operation: WorldOperation.Speed }] && renameHost.Requests[0].Command.Arguments[0] == 11, "Identity replacement retains its own script context and selects the renamed native on_tick header after RET.");

        WorldEntitySnapshot detached = snapshot.Models.Single(value => value.Detached) with { ProgramCounter = 5, MoveCounter = 7, Priority = 2, ScriptDisabled = true, CalledModel = 3, Calls = [new(5, 3, 1)] };
        cold.Restore(snapshot with { Models = [snapshot.Models[0], detached] });
        cold.RestoreTransportRider(0);
        Check(cold.Capture().Models[0] is { Model: 0, Detached: false, ProgramCounter: 0, MoveCounter: 0, Priority: 0, ScriptDisabled: false, CalledModel: 0, Calls.Length: 0 }, "Native dismount reinitializes the rider script node and prepends it without replaying on_load or suspended scripts.");

        ushort[] existingCode = [0, 0x110, 0, 0x300, 0x110, 3, 0x300, 0x110, 0, 0x330, 0x110, 3, 0x343, 0x203];
        Host existingHost = new();
        WorldMachine existing = new(new(new([new(0, 1)], existingCode)), new byte[1280], existingHost);
        existing.StartSystem(0);
        existing.Tick();
        Check(existingHost.Requests.Count == 3 && existingHost.Requests[2].Command.Operation == WorldOperation.AttachRider && existing.Capture().Models.Length == 2 && existing.IsModelAttached(3) && !existing.IsModelAttached(0), "Attachment to a resident transport must reuse its script node and detach only the prior active rider.");
    }

    private static void DeleteModelAndPool()
    {
        ushort[] code = [0, 0x110, 1, 0x300, 0x110, 2, 0x300, 0x203, 0x110, 11, 0x303, 0x203, 0x31A, 0x110, 1, 0x330, 0x110, 7, 0x303, 0x203];
        PcWorldEvents events = new([new(0, 1), new(0x4100, 8), new(0x4200, 12)], code);
        Host host = new();
        WorldMachine machine = new(new(events), new byte[1280], host);
        machine.StartSystem(0);
        machine.Tick();
        WorldSnapshot deleted = machine.Capture();
        Check(deleted.Models is [{ Model: 1, ProgramCounter: 8 }] && deleted.FreeModels is [{ Model: 2, ProgramCounter: 0 }], "Deleting the executing model must unlink it into the native free pool without ticking the next active node twice.");
        Check(host.Requests is [_, _, { Command.Operation: WorldOperation.DeleteActiveModel, ExecutingModel: 2, ActiveModel: 2 }, { Command.Operation: WorldOperation.Speed, ExecutingModel: 2, ActiveModel: 1 }] && host.Requests[^1].Command.Arguments[0] == 7, "Native deletion must consume no operand and allow the current callback to continue after explicitly selecting another active model.");
        Host coldHost = new();
        WorldMachine cold = new(new(events), machine.CopyPersistent(), coldHost);
        cold.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(deleted))!);
        host.Requests.Clear();
        machine.Tick();
        cold.Tick();
        Check(host.Requests is [{ ActiveModel: 1, Command.Operation: WorldOperation.Speed }] && host.Requests[0].Command.Arguments[0] == 11 && JsonSerializer.Serialize(host.Requests) == JsonSerializer.Serialize(coldHost.Requests) && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "The remaining active model must run once on the next callback, with exact cold free-pool continuation and no deleted on_tick restart.");

        ushort[] otherCode = [0, 0x110, 1, 0x300, 0x110, 2, 0x300, 0x203, 0x110, 11, 0x303, 0x203, 0x110, 1, 0x330, 0x31A, 0x110, 2, 0x330, 0x110, 5, 0x303, 0x203, 0x31A, 0x203];
        PcWorldEvents otherEvents = new([new(0, 1), new(0x4100, 8), new(0x4200, 12), new(0x4202, 23)], otherCode);
        Host otherHost = new();
        WorldMachine other = new(new(otherEvents), new byte[1280], otherHost);
        other.StartSystem(0);
        other.Tick();
        WorldSnapshot otherDeleted = other.Capture();
        Check(otherDeleted.Models is [{ Model: 2, ProgramCounter: 23 }] && otherDeleted.FreeModels is [{ Model: 1, ProgramCounter: 8 }], "Deleting another selected model must retain the executing node and the freed node's original native memory.");
        Host otherColdHost = new();
        WorldMachine otherCold = new(new(otherEvents), other.CopyPersistent(), otherColdHost);
        otherCold.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(otherDeleted))!);
        otherHost.Requests.Clear();
        other.Tick();
        otherCold.Tick();
        Check(other.Capture().Models.Length == 0 && other.Capture().FreeModels is [{ Model: 2, ProgramCounter: 0 }, { Model: 1, ProgramCounter: 0 }] && otherHost.Requests is [{ Command.Operation: WorldOperation.DeleteActiveModel }, { Command.Operation: WorldOperation.Speed, ExecutingModel: 1 }] && JsonSerializer.Serialize(other.Capture()) == JsonSerializer.Serialize(otherCold.Capture()) && JsonSerializer.Serialize(otherHost.Requests) == JsonSerializer.Serialize(otherColdHost.Requests), "Native iteration must follow the deleted current node's free-list pointer, including its retained script memory, after cold restore.");

        ushort[] reuseCode = [0, 0x110, 2, 0x300, 0x203, 0x31A, 0x110, 3, 0x300, 0x110, 7, 0x303, 0x203, 0x110, 99, 0x303, 0x203];
        Host reuseHost = new();
        WorldMachine reuse = new(new(new([new(0, 1), new(0x4200, 5), new(0x4300, 13)], reuseCode)), new byte[1280], reuseHost);
        reuse.StartSystem(0);
        reuse.Tick();
        Check(reuse.Capture().Models is [{ Model: 3, ProgramCounter: 0 }] && reuse.Capture().FreeModels is null && reuseHost.Requests[^1] is { ExecutingModel: 3, ActiveModel: 3, Command.Operation: WorldOperation.Speed } && reuseHost.Requests[^1].Command.Arguments[0] == 99 && !reuseHost.Requests.Any(request => request.Command.Operation == WorldOperation.Speed && request.Command.Arguments[0] == 7), "Immediate allocation must reuse the native freed object, reset its PC, and execute the new on_load instead of the old script tail.");

        List<ushort> poolCode = [0];
        for (ushort model = 0; model < 16; model++)
        {
            poolCode.AddRange([0x110, model, 0x300]);
        }
        poolCode.Add(0x203);
        ushort refill = checked((ushort)poolCode.Count);
        poolCode.AddRange([0x110, 0, 0x330, 0x31A, 0x110, 16, 0x300, 0x203]);
        PcWorldEvents poolEvents = new([new(0, 1), new(2, refill)], poolCode);
        WorldMachine pool = new(new(poolEvents), new byte[1280], new Host());
        pool.StartSystem(0);
        pool.Tick();
        WorldMachine poolCold = new(new(poolEvents), pool.CopyPersistent(), new Host());
        poolCold.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(pool.Capture()))!);
        pool.Tick();
        poolCold.Tick();
        Check(pool.Capture().Models.Length == 16 && !pool.Capture().Models.Any(model => model.Model == 0) && pool.Capture().Models.Any(model => model.Model == 16) && JsonSerializer.Serialize(pool.Capture()) == JsonSerializer.Serialize(poolCold.Capture()), "Deleting and replacing a node at the native sixteen-object limit must release its exact pool slot after cold restore.");
    }

    private static void VehicleOperationOperands()
    {
        ushort[] code = [0, 0x331, 0x335, 0x33C, 0x110, 7, 0x337, 0x110, 9, 0x303, 0x203];
        Host host = new();
        WorldMachine machine = new(new(new([new(0, 1)], code)), new byte[1280], host);
        machine.StartSystem(0);
        machine.Tick();
        Check(host.Requests.Select(request => request.Command.Operation).SequenceEqual(new[] { WorldOperation.StartLeaveVehicle, WorldOperation.RestoreAttachedRider, WorldOperation.Surface, WorldOperation.EnableTinyBroncoFlight, WorldOperation.Speed }), "Native331,335,33C and337 complete without a script yield and retain dispatch order.");
        Check(host.Requests.Take(3).All(request => request.Command.Arguments.Length == 0) && host.Requests[3].Command.Arguments.SequenceEqual(new[] { 7 }) && host.Requests[4].Command.Arguments.SequenceEqual(new[] { 9 }), "Native331,335 and33C take no stack operands;337 pops exactly its Tiny Bronco flight flag.");
    }

    private static void NativeReservedWorldOpcode()
    {
        ushort[] code = [0, 0x110, 123, 0x31B, 0x303, 0x203];
        Host host = new();
        WorldMachine machine = new(new(new([new(0, 1)], code)), new byte[1280], host);
        machine.StartSystem(0);
        machine.Tick();
        Check(host.Requests is [{ Command.Operation: WorldOperation.Speed }] && host.Requests[0].Command.Arguments.SequenceEqual(new[] { 123 }) && machine.Capture().Stack.Length == 0, "Recognized native31B is a same-callback no-op that consumes no operands and publishes no host command.");
    }

    private static void ChocoboPathWaitContracts()
    {
        ushort[] code = [0, 0x110, 4, 0x300, 0x110, 0, 0x300, 0x33F, 0x110, 3, 0x340, 0x110, 17, 0x303, 0x341, 0x110, 7, 0x303, 0x342, 0x110, 9, 0x303, 0x203];
        PcWorldEvents events = new([new(0, 1)], code);
        bool ready = false;
        bool running = true;
        WorldHostReply Reply(WorldHostRequest request) => request.Command.Operation switch
        {
            WorldOperation.RunChocoboPath => new(true, ActiveModel: 4),
            WorldOperation.WaitChocoboPath => new(ready),
            WorldOperation.WaitChocoboRuns => new(!running),
            _ => new(true)
        };
        Host host = new() { Reply = Reply };
        WorldMachine machine = new(new(events), new byte[1280], host);
        machine.StartSystem(0);
        machine.Tick();
        Check(machine.Capture().System.ProgramCounter == 14 && machine.Capture().Stack.Length == 0, "341 rewinds exactly its own instruction while preserving the completed340 operand pop.");
        Check(host.Requests.Single(request => request.Command.Operation == WorldOperation.BeginChocoboPath).Command.Arguments.Length == 0 && host.Requests.Single(request => request.Command.Operation == WorldOperation.RunChocoboPath).Command.Arguments.SequenceEqual(new[] { 3 }), "33F consumes no operands and340 pops exactly one path action.");
        Check(host.Requests.Single(request => request.Command.Operation == WorldOperation.Speed) is { ActiveModel: 4 }, "A native host path action can select its caught bird for following same-callback instructions.");
        WorldSnapshot snapshot = machine.Capture();
        Host coldHost = new() { Reply = Reply };
        WorldMachine cold = new(new(events), machine.CopyPersistent(), coldHost);
        cold.Restore(JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(snapshot))!);
        host.Requests.Clear();
        ready = true;
        machine.Tick();
        cold.Tick();
        Check(machine.Capture().System.ProgramCounter == 18 && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()) && JsonSerializer.Serialize(host.Requests) == JsonSerializer.Serialize(coldHost.Requests), "Cold path readiness completes341, continues the same callback and yields only on active runner342.");
        running = false;
        machine.Tick();
        cold.Tick();
        Check(machine.Capture().System.ProgramCounter == 0 && JsonSerializer.Serialize(machine.Capture()) == JsonSerializer.Serialize(cold.Capture()), "342 completes at the native runner-zero boundary without replaying initialization or action340.");

        Host unloadHost = new() { Reply = _ => new(false) };
        WorldMachine unload = new(new(new([new(0, 1)], new ushort[] { 0, 0x341, 0x342, 0x110, 99, 0x303, 0x203 })), new byte[1280], unloadHost);
        unload.StartSystem(0);
        unload.Tick();
        unloadHost.Requests.Clear();
        unloadHost.Reply = request => request.Command.Operation == WorldOperation.Speed ? new(true) : throw new InvalidOperationException("Native unload must bypass Chocobo waits without querying the host.");
        unload.CompleteUnload();
        Check(unload.Capture() is { Unloading: true, System.ProgramCounter: 0 } && unloadHost.Requests is [{ Command.Operation: WorldOperation.Speed }], "Both Chocobo wait polarities complete unconditionally during original module unload.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class Host : IWorldHost
    {
        public List<WorldHostRequest> Requests { get; } = [];
        public Func<WorldHostRequest, WorldHostReply>? Reply { get; set; }

        public WorldHostReply Execute(WorldHostRequest request)
        {
            Requests.Add(request);
            return Reply?.Invoke(request) ?? new(true);
        }

        public int ReadSpecial(int selector, int activeModel)
        {
            throw new NotSupportedException($"Unexpected special selector {selector}.");
        }
    }
}
