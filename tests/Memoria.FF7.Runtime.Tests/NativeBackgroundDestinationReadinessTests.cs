using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;

internal static class NativeBackgroundDestinationReadinessTests
{
    private sealed record Fixture(FieldMachine Machine, byte[] Code, FieldVmSnapshot Vm, Ff7FieldProcess Process);
    private static byte[] Motion => [0xA8, 0, 10, 0, 20, 0];

    internal static void Run()
    {
        Fixture loop = Create([.. Motion, 0x24, 30, 0, 0x12, 9]);
        string before = JsonSerializer.Serialize(new { loop.Vm, loop.Process, loop.Code });
        FieldScriptAnalysis analysis = loop.Machine.AnalyzeScriptAtProgramCounter(1, 8);
        Check(analysis.Complete && analysis.IndependentActorMotionLoop, "A closed literal self-NPC movement loop was not classified.");
        Check(!Destination(loop), "A certified background movement loop blocked destination setup.");
        FieldEntitySnapshot[] waiting = loop.Vm.Entities.ToArray();
        waiting[1] = waiting[1] with { ProgramCounter = 14, WaitCounter = 30 };
        Check(!Destination(loop with { Vm = loop.Vm with { Entities = waiting } }), "The same certified loop blocked destination setup during its WAIT frame.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "nav", loop.Vm, loop.Process, loop.Code, []), "The destination-only rule changed general native readiness.");
        FieldEntitySnapshot[] ambient = loop.Vm.Entities.ToArray();
        ambient[1] = ambient[1] with { Priority = 7, SavedProgramCounters = new int[8] };
        FieldVmSnapshot patrol = loop.Vm with { Entities = ambient, PendingRequests = [loop.Vm.PendingRequests![0] with { Priority = 7 }] };
        Check(new Ff7AutoPlayNativeReadiness().ShouldWait(1, "nav", patrol, loop.Process, loop.Code, []), "General frontier readiness no longer allowed an actual finite ambient motion to continue.");
        Check(!new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", patrol, loop.Process, loop.Code, [], (_, _) => analysis), "An unowned ambient motion hid an unavailable explicit destination route.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", loop.Vm, loop.Process, loop.Code, []), "Missing old analysis evidence silently suppressed admitted work.");
        Check(new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", loop.Vm, loop.Process, loop.Code, [], (_, _) => analysis with { Complete = false }), "An incomplete certificate suppressed admitted work.");
        Check(Destination(loop with { Process = loop.Process with { Actors = [loop.Process.Actors.Single() with { PlayerControlled = true }] } }), "A controlled player's native movement was treated as background work.");
        Check(before == JsonSerializer.Serialize(new { loop.Vm, loop.Process, loop.Code }), "Readiness classification changed actual native motion, variables or execution.");

        foreach (byte[] body in new byte[][]
        {
            [.. Motion, 0],
            [.. Motion, 0x80, 0x50, 0, 1, 0x12, 10],
            [.. Motion, 0xA8, 0x60, 0, 0, 0, 0, 0x12, 12],
            [.. Motion, 0x33, 1, 0x12, 8],
            [.. Motion, 0x01, 0, 1, 0x12, 9],
            [.. Motion, 0x73, 0x06, 0, 0, 0x12, 10],
            [.. Motion, 0x30, 1, 0, 1, 0x12, 10],
            [.. Motion, 0x6D, 0, 0, 0x12, 9],
            [.. Motion, 0xFF]
        })
        {
            Fixture invalid = Create(body);
            Check(!invalid.Machine.AnalyzeScriptAtProgramCounter(1, 8).IndependentActorMotionLoop, "A finite, reading, writing, global, child, input or unsupported body qualified as independent background motion.");
            Check(Destination(invalid), "An unproved native continuation lost destination readiness.");
        }
        CheckSynchronousLink(loop);
        Console.WriteLine("Independent background actor loop classification and destination-only readiness exclusions passed.");
    }

    private static void CheckSynchronousLink(Fixture loop)
    {
        byte[] script = loop.Code.ToArray();
        script[2] = 0x03;
        script[3] = 1;
        script[4] = 0xC1;
        FieldEntitySnapshot[] entities = loop.Vm.Entities.Select(entity => entity with { SyncStates = entity.SyncStates.ToArray(), SyncOwners = entity.SyncOwners.ToArray() }).ToArray();
        entities[1].SyncStates[6] = FieldSyncState.Waiting;
        entities[1].SyncOwners[6] = 0;
        Check(new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", loop.Vm with { Entities = entities }, loop.Process, script, [], (entity, pc) => loop.Machine.AnalyzeScriptAtProgramCounter(entity, pc)), "A genuine synchronous child was suppressed merely because its body independently loops.");
    }

    private static Fixture Create(byte[] body)
    {
        byte[] code = new byte[8 + body.Length];
        body.CopyTo(code, 8);
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][1] = 8;
        FieldProgram program = new(code, entries);
        FieldMachine machine = new(program);
        machine.Initialize();
        FieldVmSnapshot vm = machine.Capture();
        vm.Entities[0] = vm.Entities[0] with { ProgramCounter = 2 };
        vm.Entities[1] = vm.Entities[1] with { ProgramCounter = 8, Priority = 6, ModelBound = true };
        vm.Entities[1].SavedProgramCounters[7] = 2;
        FieldHostRequest request = new(1, 1, 6, 8, new FieldActorMove(10, 20));
        vm = vm with { PendingRequests = [request], NextRequestId = 2 };
        machine = FieldMachine.Restore(program, vm);
        Ff7FieldProcess process = new() { Actors = [new() { Slot = 0, Entity = 1, Model = 0 }], Motions = [new(1, 1, 10, 20, true, true)] };
        return new(machine, code, vm, process);
    }

    private static bool Destination(Fixture fixture)
    {
        return new Ff7AutoPlayNativeReadiness().ShouldWaitForDestinationSetup(1, "nav", fixture.Vm, fixture.Process, fixture.Code, [], (entity, pc) => fixture.Machine.AnalyzeScriptAtProgramCounter(entity, pc));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
