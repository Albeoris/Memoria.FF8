using System.Diagnostics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeSynchronousTriangleSetupTests
{
    internal static void Run()
    {
        byte[] script = [0x03, 1, 0xC0, 0, 0x6D, 1, 0, 1, 0x6D, 2, 0, 0, 0];
        int[] saved = new int[8];
        saved[7] = 3;
        FieldEntitySnapshot parent = new(0, 6, 0, saved, new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray());
        FieldSyncState[] sync = new FieldSyncState[8];
        sync[6] = FieldSyncState.Waiting;
        int[] owners = Enumerable.Repeat(-1, 8).ToArray();
        owners[6] = 0;
        FieldEntitySnapshot child = new(4, 6, 0, saved, sync, owners);
        FieldVmSnapshot vm = new(1, FieldMachine.SemanticProfile, "triangle-setup", 1, true, new byte[1280], new byte[256], [parent, child]);
        Ff7FieldProcess process = new() { BlockedTriangles = [0] };
        string before = JsonSerializer.Serialize(new { vm, process });
        bool Wait(FieldVmSnapshot state, byte[]? code = null, int entry = 4) => new Ff7AutoPlayNativeReadiness().ShouldWaitForSynchronousTriangleSetup(1, "source", state, process, code ?? script, (_, _) => entry);
        Check(Wait(vm), "A finite admitted blocking child was not scheduled.");
        Check(Wait(vm with { Entities = [parent, child with { ProgramCounter = 12 }] }), "The authenticated child terminal RET was stranded.");
        Check(!Wait(vm with { Entities = [parent with { Priority = 7 }, child] }), "An ambient parent authenticated setup.");
        Check(!Wait(vm with { Entities = [parent, child with { Priority = 7 }] }), "An ambient child authenticated setup.");
        Check(!Wait(vm with { Entities = [parent, child with { SyncOwners = Enumerable.Repeat(-1, 8).ToArray() }] }), "An absent synchronous owner authenticated setup.");
        Check(!Wait(vm, [0x03, 0, 0xC0, 0, 0x6D, 1, 0, 1, 0x6D, 2, 0, 0, 0]), "A different parent target authenticated setup.");
        Check(!Wait(vm, [0x03, 1, 0xA0, 0, 0x6D, 1, 0, 1, 0x6D, 2, 0, 0, 0]), "A different parent priority authenticated setup.");
        Check(!Wait(vm, [0x03, 1, 0xC0, 0, 0x6D, 1, 0, 1, 0xF8, 2, 0, 0, 0]), "A future movie entered the pure triangle closure.");
        Check(!Wait(vm, entry: 8), "A current PC outside the requested body authenticated setup.");
        Check(!Wait(vm, entry: 12), "An empty RET body authenticated triangle setup.");
        Ff7AutoPlayNativeReadiness frozen = new();
        bool waiting = true;
        for (int i = 0; i <= Ff7AutoPlayNativeReadiness.RepeatedObservationLimit; i++)
        {
            waiting = frozen.ShouldWaitForSynchronousTriangleSetup(1, "source", vm with { Tick = i }, process, script, (_, _) => 4);
        }
        Check(!waiting, "Frozen setup retained readiness indefinitely.");
        Check(before == JsonSerializer.Serialize(new { vm, process }), "Setup readiness changed native source facts.");
        Console.WriteLine("PASS exact synchronous pure triangle body, blocking/release, terminal RET and bounded source purity");
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_TRIANGLE_SETUP_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(stream).Ff7!);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice boundary."));
        List<FieldScriptDispatch> dispatches = [];
        session.Machine.ScriptDispatched += dispatches.Add;
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
        controller.Start();
        Stopwatch elapsed = Stopwatch.StartNew();
        bool returned = false;
        for (int i = 0; controller.Running && i < 256 && elapsed.Elapsed < TimeSpan.FromSeconds(10); i++)
        {
            controller.Advance(1);
            FieldVmSnapshot vm = session.Machine.Capture();
            Console.WriteLine($"SETUP t{vm.Tick} e0={vm.Entities[0].Priority}/{vm.Entities[0].ProgramCounter} e2={vm.Entities[2].Priority}/{vm.Entities[2].ProgramCounter} physical={controller.PlanningProgress.PhysicalNodes} detached={controller.PlanningProgress.DetachedNativeCallbacks}");
            Check(controller.PlanningProgress.PhysicalNodes == 0 && controller.PlanningProgress.DetachedNativeCallbacks == 0, "Admitted native setup started navigation graph work.");
            if (vm.Entities[0].Priority == 7 && vm.Entities[2].Priority == 7)
            {
                returned = true;
                break;
            }
        }
        Check(controller.Error is null && returned, $"Native setup did not return: {controller.Error}");
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Ff7Line line = session.Host.Process.Lines.Single(line => line.Entity == 16);
        var contacts = Ff7AutoplayDriver.DiscoverLineContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, line);
        Check(contacts.Count > 0, "Completed setup did not expose the native LINE floor contact.");
        Console.WriteLine("SETUP dispatches " + JsonSerializer.Serialize(dispatches.Where(dispatch => dispatch.SourceEntity == 0)));
        Console.WriteLine($"SETUP reachable contact triangles {string.Join(',', contacts.Select(contact => contact.Triangle).Distinct())}; mask={Convert.ToHexString(session.Host.Process.BlockedTriangles)}");
        Check(bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The standalone source changed.");
        Console.WriteLine($"PASS original native setup return callbacks={controller.Steps}, tick={session.Machine.Tick}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
