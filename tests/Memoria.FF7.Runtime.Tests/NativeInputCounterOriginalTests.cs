using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInputCounterOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_COUNTER_INPUT_SAVE") ?? throw new InvalidOperationException("Select an external native counter-input continuation.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        int anchors = 0;
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => $"counter-input-anchor-{++anchors}");
        session.NativeInteractionScriptRequested += admission =>
        {
            if (admission.Entity is 7 or 8 && admission.Accepted && admission.Started)
            {
                Console.WriteLine($"Native console admission t{session.Machine.Tick}: {admission}");
            }
        };
        Queue<string> recent = new();
        bool inspectFirst = Environment.GetEnvironmentVariable("MEMORIA_FF7_COUNTER_INPUT_INSPECT_FIRST") == "1";
        Ff7AutoplayInput Choose(Ff7AutoplayDriver driver)
        {
            Ff7AutoplayInput selected = policy.ChooseInput(driver);
            if (inspectFirst && policy.State.Active is { Entity: 7, Accepted: true } active && selected.Held != 0)
            {
                FieldScriptInputDemand[] current = session.Machine.ReadCurrentInputDemands().Where(demand => demand.Entity == 4 && demand.Priority == 6 && demand.AwaitingInput).ToArray();
                if (current.Length != 0)
                {
                    FieldVmSnapshot vm = session.Machine.Capture();
                    FieldScriptInputDemand? progress = session.Machine.ReadCurrentCounterProgressInput(4, 6);
                    Ff7Dialogue? dialogue = session.Host.Process.Dialogues.FirstOrDefault(item => !item.Completed);
                    bool? hud = dialogue is null ? null : Ff7AutoPlayScriptedInput.IsOwnedCounterDialogue(vm, session.Host.Process, active, session.FieldGeneration, 4, 6, dialogue);
                    Console.WriteLine(JsonSerializer.Serialize(new { CounterPc = vm.Entities[4].ProgramCounter.ToString("X4"), vm.Entities[4].Priority, Cycle = vm.CycleCursor is not null, session.HasPendingTick, Hud = hud, Dialogue = dialogue is null ? null : new { dialogue.RequestId, dialogue.Window, dialogue.Text, dialogue.AwaitingInput, dialogue.Presentation }, ParsedProgress = progress, ObservedMatch = progress is not null && current.Any(demand => demand.Identity == progress.Identity && demand.AwaitingInput), Demands = current, Selected = selected }));
                    throw new InvalidOperationException("The first owned counter input was inspected before its physical press.");
                }
            }
            if (policy.State.Active is { Accepted: true })
            {
                return selected;
            }
            Ff7Actor? actor = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
            object? Read(string name) => typeof(Ff7AutoplayDriver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(driver);
            string routeDistances = "";
            if (actor is not null && Read("waypointDestination") is int destination && destination >= 0)
            {
                var routes = Ff7AutoplayDriver.ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, actor.Triangle, destination);
                routeDistances = $"d6/7={routes.Distance[6]}/{routes.Distance[7]}";
            }
            recent.Enqueue($"t{session.Machine.Tick}:({actor?.X4096},{actor?.Y4096},{actor?.Triangle}):{JsonSerializer.Serialize(selected)}:route{policy.State.NativeRoute?.Cursor}/{policy.State.NativeRoute?.Steps.Length}:target={Read("finalTarget")}/tri{Read("waypointDestination")}:waypoint={Read("committedWaypoint")}:rejected={Read("navigationLineRejected")}:{routeDistances}");
            while (recent.Count > 12)
            {
                recent.Dequeue();
            }
            return selected;
        }
        Ff7AutoPlayController controller = new(session, Choose, chooseDialogueSelection: policy.ChooseDialogueSelection);
        void SaveProgress()
        {
            if (Environment.GetEnvironmentVariable("MEMORIA_FF7_COUNTER_INPUT_DIAGNOSTIC") is not string diagnostic)
            {
                return;
            }
            using MemoryStream encoded = new();
            CombinedSessionCodec.Write(encoded, new() { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture() });
            if (encoded.Length > 2 * 1024 * 1024)
            {
                throw new InvalidOperationException("The bounded native continuation exceeds its selected diagnostic allocation.");
            }
            using FileStream output = new(diagnostic, FileMode.CreateNew, FileAccess.Write);
            encoded.Position = 0;
            encoded.CopyTo(output);
            Console.WriteLine($"Retained native continuation: {encoded.Length} bytes, map={session.MapId}, tick={session.Machine.Tick}.");
        }
        void Heartbeat(string phase)
        {
            if (controller.Steps % 100 != 0)
            {
                return;
            }
            Ff7Dialogue? dialogue = session.Host.Process.Dialogues.FirstOrDefault(item => !item.Completed);
            Console.WriteLine($"{phase}: callbacks={controller.Steps}, tick={session.Machine.Tick}, dialogue={dialogue?.RequestId}/{dialogue?.Window}/{dialogue?.Text}, phase={dialogue?.Presentation?.NativeState}, token={dialogue?.TokenPosition}, status={policy.Status}.");
        }
        controller.Start();
        bool consoleContinuation = policy.State.SourceGoal is { Entity: 7, Script: 2, AcceptedEntry: not null };
        bool IntroReturned() => (session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x86) & 0x10) != 0 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && policy.State.Active is null;
        bool bound = consoleContinuation || IntroReturned() || policy.State.SourceGoal is { Entity: 8, Script: 4, AcceptedEntry: not null };
        FieldVmSnapshot bindingVm = session.Machine.Capture();
        for (int attempt = 0; !bound && attempt < 128 && controller.Error is null; attempt++)
        {
            try
            {
                policy.SetSourceGoal("line:1:entity:8:script:4");
                bound = true;
                bindingVm = session.Machine.Capture();
                break;
            }
            catch (InvalidOperationException error) when (error.Message == "The current native event must return before changing its planning destination.")
            {
                controller.Advance(1);
            }
        }
        if (!bound)
        {
            throw new InvalidOperationException("The native console did not become ready for its source goal.", controller.Failure);
        }
        Stopwatch prerequisite = Stopwatch.StartNew();
        while (!consoleContinuation && controller.Error is null && !controller.Paused && !IntroReturned() && controller.Steps < 3000 && prerequisite.Elapsed.TotalSeconds < 90)
        {
            controller.Advance(1);
            Heartbeat("Native introduction");
        }
        if (!consoleContinuation && (controller.Error is not null || !IntroReturned() || policy.State.SourceGoal is not { Completed: true }))
        {
            Ff7AutoPlayObservation currentObservation = new Ff7AutoPlayDiscovery(session).Observe();
            Ff7AutoPlayTarget? currentTarget = currentObservation.Events.SingleOrDefault(target => target.Id == policy.State.SourceGoal?.EventId);
            FieldScriptEntryDependencies dependencies = session.Machine.AnalyzeEntryDependencies(8, 4);
            FieldVmSnapshot currentVm = session.Machine.Capture();
            Console.WriteLine(JsonSerializer.Serialize(new { currentObservation.CanAcceptInput, currentObservation.NavigationKey, currentObservation.Generation, Target = currentTarget is null ? null : new { currentTarget.ConditionFingerprint, currentTarget.AnalysisComplete, currentTarget.EntryInputContract }, dependencies.Reads, dependencies.HostReads, PersistentChanges = currentVm.PersistentVariables.Select((value, index) => new { index, before = bindingVm.PersistentVariables[index], value }).Where(change => change.before != change.value).Take(20), MapChanges = currentVm.MapVariables.Select((value, index) => new { index, before = bindingVm.MapVariables[index], value }).Where(change => change.before != change.value).Take(20) }));
            Console.WriteLine(JsonSerializer.Serialize(new { Goal = policy.State.SourceGoal, Active = policy.State.Active is not { } current ? null : new { current.EventId, current.Entity, current.Script, current.Accepted, current.Returned, current.CompletionRecorded, current.ExecutionProvenance }, P186 = session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x86) }));
            SaveProgress();
            throw new InvalidOperationException($"The actual protected console introduction did not return: steps={controller.Steps}, status={policy.Status}, error={controller.Error}.", controller.Failure);
        }
        if (!consoleContinuation)
        {
            Console.WriteLine($"Native protected introduction returned: callbacks={controller.Steps}, seconds={prerequisite.Elapsed.TotalSeconds:F3}.");
            controller.Start();
            policy.SetSourceGoal("line:0:entity:7:script:2");
        }
        List<ushort> presses = [];
        bool timerStarted = false;
        session.Host.NativeOperationExecuted += (request, reply) => timerStarted |= request.Command is FieldTimerCommand;
        session.Machine.InstructionExecuted += (entity, priority, pc, opcode) =>
        {
            if (entity == 4 && opcode == 0x95)
            {
                presses.Add(session.Machine.Input.RawPressed);
            }
        };
        Stopwatch timer = Stopwatch.StartNew();
        int boundSteps = int.TryParse(Environment.GetEnvironmentVariable("MEMORIA_FF7_COUNTER_INPUT_STEPS"), out int maximum) ? maximum : 5000;
        while (controller.Error is null && !controller.Paused && controller.Steps < boundSteps && timer.Elapsed.TotalSeconds < 90)
        {
            controller.Advance(1);
            Heartbeat("Native console");
            if (policy.State.PendingRestore is not null)
            {
                throw new InvalidOperationException("The native counter continuation requested a choice replay.");
            }
        }
        byte counter = session.Machine.Variables.ReadByte(FieldBank.MapByte, 9);
        byte award = session.Machine.Variables.ReadByte(FieldBank.Persistent1Byte, 0x86);
        bool materiaAward = (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0x42) & 0x80) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x52) & 0x80) != 0 && (session.Machine.Variables.ReadByte(FieldBank.Persistent3Byte, 0x61) & 1) != 0;
        Ff7Actor? player = session.Host.Process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        Console.WriteLine($"Original counter input: steps={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, counter={counter}, presses={string.Join(',', presses.Select(value => value.ToString("X4")))}, award={award:X2}, source={policy.State.SourceGoal?.Completed}/{policy.State.SourceGoal?.AcceptedEntry}, active={policy.State.Active?.Accepted}/{policy.State.Active?.LinePhase}, pose={player?.X4096},{player?.Y4096},{player?.Triangle}, pending={session.HasPendingTick}, locks={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, status={policy.Status}, error={controller.Error}.");
        if (controller.Error is not null || counter != 4 || (award & 0x80) == 0 || !materiaAward || timerStarted || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || policy.State.SourceGoal is not { Completed: true } || !controller.Paused || !bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            Console.WriteLine(string.Join(Environment.NewLine, recent));
            SaveProgress();
            throw new InvalidOperationException("The native finite counter sequence did not earn its actual award and return its owned source.", controller.Failure);
        }
    }
}

