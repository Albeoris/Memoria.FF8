using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePromptInputOriginalTests
{
    internal static void RunDigReward()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_PROMPT_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native excavation reward fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The selected dig reward must not require a choice checkpoint."));
        bool digPressed = false;
        bool wonOutcome = false;
        bool arrived = false;
        bool rewardStarted = false;
        bool rewardReturned = false;
        session.NativeInteractionScriptRequested += admission =>
        {
            if (session.MapId == 617 && admission is { Entity: 13, Script: 1, Priority: 1, Accepted: true, Started: true })
            {
                rewardStarted = true;
                Console.WriteLine($"Native reward talk started at {session.Machine.Capture().Entities[13].ProgramCounter:X4}.");
            }
        };
        session.NativeTickCompleted += observation => rewardReturned |= session.MapId == 617 && observation.ScriptReturns.Any(returned => returned.Entity == 13 && returned.Priority == 1);
        var controller = new Ff7AutoPlayController(session, driver =>
        {
            var result = policy.ChooseInput(driver);
            if (session.MapId == 772 && (result.Held & 0x80) != 0 && (session.Machine.Input.RawHeld & 0x80) == 0)
            {
                int target = session.Machine.Variables.ReadWord(FieldBank.MapWord, 0x10);
                int actual = session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle;
                if (actual != target)
                {
                    throw new InvalidOperationException("The native dig button was selected before its required ground triangle.");
                }
                digPressed = true;
                Console.WriteLine($"Native dig admitted at triangle {actual}, tick {session.Host.Process.Tick}.");
            }
            return result;
        }, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool reward = false;
        for (int polls = 0; polls < 100000 && controller.Steps < 3000 && timer.Elapsed.TotalSeconds < 60 && controller.Error is null && !reward; polls++)
        {
            controller.Advance(1);
            wonOutcome |= session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA) == 1;
            if (!arrived && session.MapId == 617)
            {
                arrived = true;
                Console.WriteLine($"Native excavation arrival: EA={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA)}, callbacks={controller.Steps}, program={session.Machine.ProgramIdentity}, EB={session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEB)}, rewardEntry={session.Field.Scripts.Entities[13].ScriptOffsets[1]:X4}, rewardGuard={Convert.ToHexString(session.Field.Scripts.Data.Span.Slice(0x0AA9, 7))}.");
                var offered = new Ff7AutoPlayDiscovery(session).Observe().Events.SingleOrDefault(target => target.Id == "actor:13:script:1");
                var box = session.Host.Process.Actors.SingleOrDefault(actor => actor.EntityBound && actor.Entity == 13);
                Console.WriteLine($"Native reward offering: box={box?.Entity}/talk:{box?.TalkEnabled}/visible:{box?.Visible}/triangle:{box?.Triangle}, event={offered?.Id}, complete={offered?.AnalysisComplete}, reason={offered?.SupportReason}");
            }
            reward = session.MapId == 617 && (session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) & 8) != 0 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && session.Machine.Capture().Entities[13].Priority == 7;
        }
        Console.WriteLine($"Original dig reward: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, map={session.MapId}, tick={session.Host.Process.Tick}, triangle={session.Host.Process.Actors.Single(actor => actor.PlayerControlled).Triangle}, digPressed={digPressed}, outcomeEA1={wonOutcome}, reward={reward}, rewardStarted={rewardStarted}, rewardReturned={rewardReturned}, active={policy.State.Active?.EventId}, status={policy.State.Status}, error={controller.Error}");
        if (!digPressed || !wonOutcome || !arrived || !reward || !rewardStarted || !rewardReturned || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The selected native excavation did not award its real key item and return usable control.");
        }
    }

    internal static void RunBombOwnedContinuation()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_PROMPT_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native admitted excavation continuation fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The admitted bomb continuation must not create a choice checkpoint."));
        int outcome = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA);
        int harp = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7);
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        bool ready = false;
        bool sawMessage = false;
        for (int polls = 0; polls < 100000 && controller.Steps < 1024 && timer.Elapsed.TotalSeconds < 35 && controller.Error is null && !ready; polls++)
        {
            controller.Advance(1);
            sawMessage |= session.Host.Process.Dialogues.Any(dialogue => dialogue.Text == 31);
            ready = session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C) == 1 && session.Machine.Capture().Entities[2].Priority == 7;
        }
        Console.WriteLine($"Original admitted bomb continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Host.Process.Tick}, phase={session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C)}, message31={sawMessage}, workerPriority={session.Machine.Capture().Entities[2].Priority}, nextInput={new Ff7AutoPlayScriptedInput(session).ChooseInput()}, physical={controller.PlanningProgress.PhysicalNodes}, detached={controller.PlanningProgress.DetachedNativeCallbacks}, error={controller.Error}");
        if (!ready || !sawMessage || new Ff7AutoPlayScriptedInput(session).ChooseInput() is not null || controller.Error is not null || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA) != outcome || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) != harp || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The admitted bomb continuation did not return normally before the next positional input.");
        }
    }

    internal static void RunBombContinuation()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_PROMPT_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native excavation bomb fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The bomb continuation must not create a choice checkpoint."));
        int outcome = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA);
        int harp = session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7);
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        HashSet<int> texts = [];
        bool ready = false;
        for (int polls = 0; polls < 100000 && controller.Steps < 1024 && timer.Elapsed.TotalSeconds < 35 && controller.Error is null && !ready; polls++)
        {
            controller.Advance(1);
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
            {
                texts.Add(dialogue.Text);
            }
            ready = texts.Contains(30);
        }
        Console.WriteLine($"Original bomb continuation: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Host.Process.Tick}, phase={session.Machine.Variables.ReadByte(FieldBank.MapByte, 0x0C)}, texts={string.Join(',', texts.Order())}, workerPriority={session.Machine.Capture().Entities[2].Priority}, nextInput={new Ff7AutoPlayScriptedInput(session).ChooseInput()}, error={controller.Error}");
        Console.WriteLine($"bomb boundary ownerPc={session.Machine.Capture().Entities[2].ProgramCounter:X4}, windows={string.Join(';', session.Host.Process.Windows.Select(window => $"{window.Id}/{window.Style}/{window.Flags}"))}, dialogues={string.Join(';', session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed).Select(dialogue => $"{dialogue.Text}/await:{dialogue.AwaitingInput}/state:{dialogue.Presentation?.NativeState}/token:{dialogue.TokenPosition}/confirm:{session.Host.CanConfirmDialogue(dialogue.RequestId)}"))}");
        if (!ready || new Ff7AutoPlayScriptedInput(session).ChooseInput() is not null || controller.Error is not null || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xEA) != outcome || session.Machine.Variables.ReadByte(FieldBank.Persistent0Byte, 0xE7) != harp || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The bomb did not reach its native message boundary or a positional dig input was admitted prematurely.");
        }
    }

    internal static void RunChoiceRouteContinuation()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_PROMPT_INPUT_SAVE")!;
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        Dictionary<string, Ff7SessionSave> checkpoints = [];
        using var policy = new Ff7AutoPlayPolicy(session, (key, save) =>
        {
            string name = "memory:" + key;
            checkpoints.Add(name, save);
            return name;
        });
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: policy.ChooseDialogueSelection);
        string choiceKey = policy.State.Choice!.Key;
        int initialChoices = policy.State.Coverage.Choices.Single(choice => choice.Key == choiceKey).CompletedOptions.Count;
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 100000 && controller.Steps < 1024 && timer.Elapsed.TotalSeconds < 25 && controller.Error is null && policy.State.PendingRestore is null; polls++)
        {
            controller.Advance(1);
            if (controller.Steps == 1)
            {
                var live = policy.State.Coverage.Choices.Single(choice => choice.Key == choiceKey);
                if (live.CompletedOptions.Count != initialChoices || policy.State.Active?.CompletionRecorded == true)
                {
                    throw new InvalidOperationException("An unrelated returned source completed the still-running worker choice.");
                }
            }
        }
        Console.WriteLine($"Original choice route: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Host.Process.Tick}, nativeChoiceOwner={policy.State.Choice?.NativeOwner?.Entity}, ownerPriority={session.Machine.Capture().Entities[2].Priority}, completedOptions={policy.State.Coverage.Choices.Single(choice => choice.Key == choiceKey).CompletedOptions.Count}, pendingRestore={policy.State.PendingRestore?.Path}, active={policy.State.Active?.EventId}, memoryCheckpoints={checkpoints.Count}, status={policy.State.Status}, error={controller.Error}");
        if (controller.Error is not null || policy.State.PendingRestore is null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The native worker choice did not reach its genuine completion and replay boundary.");
        }
    }

    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_NATIVE_PROMPT_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native prompt input fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream input = new(bytes, writable: false);
        using var session = Ff7Session.Restore(Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!), CombinedSessionCodec.Read(input).Ff7!);
        using var policy = new Ff7AutoPlayPolicy(session, (_, _) => throw new InvalidOperationException("The prompt boundary test does not store choices."));
        FieldScriptInputDemand[] demands = session.Machine.ReadCurrentInputDemands().ToArray();
        FieldHostRequest prompt = session.Machine.Capture().PendingRequests!.Single(request => request.Command is FieldDialogueCommand { FirstChoice: null });
        Console.WriteLine(string.Join(";", demands.Select(demand => $"{demand.Identity}:awaiting={demand.AwaitingInput}:mask={demand.Mask}:proof={session.Machine.ProveCurrentPromptChoiceInput(demand, prompt)}")));
        var controller = new Ff7AutoPlayController(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        Stopwatch timer = Stopwatch.StartNew();
        for (int polls = 0; polls < 10000 && controller.Steps < 64 && timer.Elapsed.TotalSeconds < 15 && controller.Error is null && !session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null); polls++)
        {
            controller.Advance(1);
        }
        Ff7Dialogue? choice = session.Host.Process.Dialogues.SingleOrDefault(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null);
        Console.WriteLine($"Original prompt input: callbacks={controller.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session.Host.Process.Tick}, choiceText={choice?.Text}, choiceOwner={session.Machine.Capture().PendingRequests?.SingleOrDefault(request => request.Id == choice?.RequestId)?.Entity}, activeAccepted={policy.State.Active?.Accepted}, status={policy.State.Status}, error={controller.Error}");
        if (choice is null || controller.Error is not null || !hash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("The current native prompt did not admit its real choice workflow.");
        }
    }
}
