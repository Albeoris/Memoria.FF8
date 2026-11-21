using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeActorRouteTests
{
    internal static void Run()
    {
        Ff7AutoPlayTarget target = new("actor", 0, 1, Ff7AutoPlayTargetKind.ActorTalk, "condition", false, default);
        Check(Ff7AutoplayDriver.SupportsNativeRouteTarget(target), "Actor talk was excluded from native graph eligibility.");
        Check(!Ff7AutoplayDriver.SupportsNativeRouteTarget(target with { Kind = Ff7AutoPlayTargetKind.ActorContact }), "An unsupported contact acquired talk admission semantics.");
        CheckFacingSelection();
        CheckSelectedDialogue(target);
        CheckClockWitness();
        Console.WriteLine("PASS native actor facing/selection, exact admission ownership, dialogue/menu terminal fences and cold authentication.");
    }

    internal static void RunSourceRelease()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTOR_SOURCE_RELEASE_SAVE");
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            Console.WriteLine("SKIP native actor source release: configure original resources and an arrived actor revalidation fixture.");
            return;
        }
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Ff7AutoPlaySourceRevalidationTask task = saved.AutoPlay?.NativeRevalidation is { ArrivalAcknowledged: true } arrived ? arrived : throw new InvalidDataException("The actor release fixture requires an acknowledged source task.");
        var installation = Memoria.FF7.Resources.Ff7Installation.Discover(root);
        using Ff7Session source = Ff7Session.Restore(installation, saved);
        string before = JsonSerializer.Serialize(source.Capture());
        using Ff7Session session = Ff7Session.Restore(installation, saved);
        Ff7AutoPlayTarget target = new Ff7AutoPlayDiscovery(session).Observe().Events.Single(item => item.Kind == Ff7AutoPlayTargetKind.ActorTalk && item.Id == task.EventId && item.ConditionFingerprint == task.Condition);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("No checkpoint may be written."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        bool accepted = false;
        bool released = false;
        session.NativeActorTalkRequested += admission => accepted |= Ff7AutoplayDriver.IsSelectedNativeActorAdmission(target, admission);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, readPlanningProgress: () => policy.WorkProgress);
        controller.Start();
        long first = session.Host.Process.Tick;
        var wall = System.Diagnostics.Stopwatch.StartNew();
        while (wall.Elapsed < TimeSpan.FromSeconds(15) && controller.Running && session.Battle is null && session.FieldGeneration == saved.FieldGeneration && session.Host.Process.Tick - first < 96 && !accepted)
        {
            controller.Advance(1);
            released |= policy.State.NativeRoute is { SourceReleased: true } proof && proof.EventId == target.Id;
        }
        Check(controller.Error is null, "Public actor source-release continuation failed: " + controller.Error);
        Check(released && accepted && policy.State.Active is { Accepted: true } active && active.EventId == target.Id && active.Entity == target.Entity && active.Script == target.Script && policy.State.NativeRevalidation is null, "Actor source-release proof did not preserve its exact owner through actual native admission and source-task completion.");
        Check(before == JsonSerializer.Serialize(source.Capture()) && bytes.SequenceEqual(File.ReadAllBytes(path)), "Actor source-release regression changed the retained source session/file.");
        Console.WriteLine("PASS public native actor source release, exact typed actor admission/task completion and source immutability.");
    }

    private static void CheckFacingSelection()
    {
        short[] vectors = new short[512];
        for (int i = 0; i < 256; i++)
        {
            vectors[i * 2] = (short)Math.Round(Math.Sin(i * Math.PI / 128) * 4096);
            vectors[i * 2 + 1] = (short)Math.Round(Math.Cos(i * Math.PI / 128) * 4096);
        }
        byte[] angles = Enumerable.Range(0, 129).Select(i => (byte)Math.Round(Math.Asin(i / 128.0) * 128 / Math.PI)).ToArray();
        Ff7NativeMovement movement = new([new PcWalkmeshTriangle(0, new(-1000, -1000, 0), new(0, 1000, 0), new(1000, -1000, 0), -1, -1, -1)], vectors, angles);
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, CollisionRadius = 30, Direction = movement.Direction(100, 0) };
        Ff7Actor target = new() { Slot = 2, Entity = 2, Model = 1, X4096 = 100 * 4096, TalkEnabled = true, TalkRadius = 120 };
        Ff7Actor wrong = target with { Slot = 1, Entity = 1 };
        string before = JsonSerializer.Serialize(new[] { player, target, wrong });
        Check(Ff7Session.SelectNativeTalkTarget(player, [target], movement)?.Entity == target.Entity, "A native-ready actor was not selected.");
        Check(Ff7Session.SelectNativeTalkTarget(player with { Direction = (byte)(player.Direction + 128) }, [target], movement) is null, "Talk bypassed the native facing window.");
        Check(Ff7Session.SelectNativeTalkTarget(player, [target, wrong], movement)?.Entity == wrong.Entity, "Exact native selection ignored the earlier competing NPC.");
        Check(Ff7Session.SelectNativeTalkTarget(player, [target with { Z4096 = 256 * 4096 }], movement) is null, "Talk ignored native height admission.");
        Check(before == JsonSerializer.Serialize(new[] { player, target, wrong }), "Talk preview changed source actors.");
    }

    private static void CheckSelectedDialogue(Ff7AutoPlayTarget target)
    {
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldProgram program = new([0, 0x40, 0, 0, 0], [entries]);
        Ff7FieldHost host = new(NativeDialogueTests.Field(), (_, _) => 1);
        FieldMachine machine = new(program, host: host);
        machine.Initialize();
        Ff7AutoplayDriver.NativeGraphOwnership owner = new(null, target);
        FieldExternalScriptAdmission admission = machine.RequestExternalScript(0, 1);
        Check(admission is { Accepted: true, Started: true } && Ff7AutoplayDriver.IsSelectedNativeActorAdmission(target, admission), "Actual selected actor entry was not authenticated.");
        Check(!Ff7AutoplayDriver.IsSelectedNativeActorAdmission(target, admission with { Entity = 1 }) && !Ff7AutoplayDriver.IsSelectedNativeActorAdmission(target, admission with { Script = 2 }) && !Ff7AutoplayDriver.IsSelectedNativeActorAdmission(target, admission with { Accepted = false }), "Wrong/rejected NPC entry borrowed the selected goal.");
        owner.Requested(admission.Entity, admission.Script, admission.Accepted, admission.Started);
        machine.ScriptDispatched += owner.Dispatched;
        machine.ScriptReturned += owner.Returned;
        using Ff7NativeRouteClockWitness clock = new(machine);
        machine.AdvanceTick();
        FieldVmSnapshot vm = machine.Capture();
        Ff7FieldProcess process = host.Capture();
        Check(clock.Independent && process.Dialogues is [{ Completed: false }], "Native selected entry did not reach its real dialogue request.");
        Check(Ff7AutoplayDriver.HasNativeTalkTerminalContract(process, 0, vm.PendingRequests!, owner), "Selected native-owned dialogue was rejected as a terminal handoff.");
        Check(Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process, 0, vm.PendingRequests!, owner), "A known navigation prerequisite could not hand off its actual owned plain message.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process, 0, [], owner), "A prerequisite message without its actual pending request was accepted.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process, 0, vm.PendingRequests!.Select(request => request with { Entity = 1 }).ToArray(), owner), "An unrelated message acquired prerequisite ownership.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process with { Dialogues = process.Dialogues.Select(dialogue => dialogue with { FirstChoice = 1, LastChoice = 2 }).ToArray() }, 0, vm.PendingRequests!, owner), "A choice was silently accepted as a plain prerequisite message.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process with { GlobalCommand = Ff7GlobalCommand.Movie }, 0, vm.PendingRequests!, owner), "A global host operation entered the prerequisite message boundary.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process with { Movie = new(1, true, 0, false, 1) }, 0, vm.PendingRequests!, owner), "A playing movie entered the prerequisite message boundary.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process, 1, vm.PendingRequests!, owner), "A prerequisite waived the actual countdown contract.");
        Check(!Ff7AutoplayDriver.HasNativeTalkTerminalContract(process, 1, vm.PendingRequests!, owner), "Selected dialogue waived the countdown clock contract.");
        Check(!Ff7AutoplayDriver.HasNativeTalkTerminalContract(process, 0, vm.PendingRequests!.Select(request => request with { Entity = 1 }).ToArray(), owner), "Unrelated dialogue owner acquired terminal permission.");
        Check(!Ff7AutoplayDriver.HasNativeTalkTerminalContract(process, 0, [], owner), "A dialogue without its actual pending request acquired terminal permission.");
        Ff7AutoplayDriver.NativeGraphOwnership empty = new(null, target);
        empty.Requested(0, 1, true, false);
        Check(!Ff7AutoplayDriver.HasNativeTalkTerminalContract(process, 0, vm.PendingRequests!, empty) && !empty.HasDebt, "No-op admission invented a live dialogue/ladder owner.");
        Ff7SessionSave source = new() { ResourceProfile = "authored-actor", Language = "en", FieldName = "authored-actor", FieldGeneration = 1, Progress = new() { InitialKernelData = [1], Characters = [], Party = [255, 255, 255], Inventory = [], Materia = [] }, Vm = vm, Process = process };
        Ff7SessionSave cold = JsonSerializer.Deserialize<Ff7SessionSave>(JsonSerializer.Serialize(source))!;
        Ff7AutoplayDriver.NativeGraphOwnership coldOwner = new(JsonSerializer.Deserialize<Ff7NativeGraphDebt>(JsonSerializer.Serialize(owner.Capture())), null);
        Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(source) == Ff7AutoplayDriver.ReadNativeRouteFingerprint(cold) && Ff7AutoplayDriver.HasNativeTalkTerminalContract(cold.Process, 0, cold.Vm.PendingRequests!, coldOwner), "Cold terminal state lost exact source/owner authentication.");
        Check(Ff7AutoplayDriver.ReadNativeRouteFingerprint(source with { Process = process with { PlayerLocked = true } }) != Ff7AutoplayDriver.ReadNativeRouteFingerprint(cold), "Terminal native divergence did not change the authenticated After state.");
        FieldHostRequest menu = vm.PendingRequests!.Single() with { Command = new FieldMenuCommand(0, 0) };
        Check(Ff7AutoplayDriver.HasNativeTalkTerminalContract(process with { Dialogues = [], Menu = new(menu.Id, 0, 0) }, 0, [menu], owner), "Owned native menu could not terminate talk admission.");
        Check(!Ff7AutoplayDriver.HasNativeNavigationPrerequisiteBoundary(process with { Dialogues = [], Menu = new(menu.Id, 0, 0) }, 0, [menu], owner), "A prerequisite prefix silently accepted an owned menu.");
        Check(!Ff7AutoplayDriver.HasNativeTalkTerminalContract(process with { Dialogues = [], Menu = new(menu.Id, 0, 0) }, 0, vm.PendingRequests!, owner), "Dialogue request masqueraded as a menu request.");
    }

    private static void CheckClockWitness()
    {
        ushort[] entries = new ushort[32];
        entries[1] = 1;
        FieldMachine machine = new(new FieldProgram([0, 0x80, 0x11, 24, 16, 0], [entries]));
        machine.Initialize();
        machine.RequestExternalScript(0, 1);
        using Ff7NativeRouteClockWitness clock = new(machine);
        machine.AdvanceTick();
        Check(!clock.Independent, "Executed native clock access was waived by talk terminal support.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
