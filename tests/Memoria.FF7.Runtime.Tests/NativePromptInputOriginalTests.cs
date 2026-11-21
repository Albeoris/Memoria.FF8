using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativePromptInputOriginalTests
{
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
