using System.Diagnostics;
using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeInstructionInputOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_INSTRUCTION_INPUT_SAVE") ?? throw new InvalidOperationException("Select a native choice-restoration fixture.");
        Dictionary<string, byte[]> originalFiles = [];
        CombinedSessionSave Read(string file)
        {
            byte[] bytes = File.ReadAllBytes(file);
            originalFiles.TryAdd(file, SHA256.HashData(bytes));
            using MemoryStream input = new(bytes, writable: false);
            return CombinedSessionCodec.Read(input);
        }
        CombinedSessionSave envelope = Read(path);
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        Dictionary<string, CombinedSessionSave> memory = [];
        Ff7Session? session = null;
        Ff7AutoPlayPolicy? policy = null;
        Ff7AutoPlayController? controller = null;
        bool observedReturn = false;
        bool selectedAlternative = false;
        string? choiceKey = null;
        long callbacks = 0;
        int restores = 0;
        void Open(CombinedSessionSave saved)
        {
            policy?.Dispose();
            session?.Dispose();
            envelope = saved;
            session = Ff7Session.Restore(installation, saved.Ff7!);
            policy = new(session, (key, native) =>
            {
                string id = "memory:" + key;
                memory[id] = envelope with { Ff7 = native };
                return id;
            });
            choiceKey ??= policy.State.Choice?.Key;
            session.NativeTickCompleted += tick => observedReturn |= policy.State.Coverage.Choices.Single(item => item.Key == choiceKey).ActiveOption == 4 && tick.ScriptReturns.Any(owner => owner.Entity == 10 && owner.Priority == 1);
            int? Choose(Ff7Dialogue dialogue)
            {
                int? selection = policy.ChooseDialogueSelection(dialogue);
                selectedAlternative |= selection == 4 && Ff7AutoPlayChoiceWorkflow.ReadNativeChoiceOwner(session.Capture(), dialogue)?.ProgramCounter == 0x155E;
                return selection;
            }
            controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: Choose, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
            controller.Start();
        }
        try
        {
            Open(envelope);
            bool repairQueued = policy!.State.PendingRestore is not null;
            Stopwatch timer = Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < 30 && callbacks < 1000 && controller!.Error is null)
            {
                if (policy.State.PendingRestore is { } request)
                {
                    Ff7AutoPlayState state = policy.State.Clone();
                    state.PendingRestore = null;
                    CombinedSessionSave saved = memory.TryGetValue(request.Path, out var transient) ? transient : Read(request.Path);
                    Open(saved with { Ff7 = saved.Ff7! with { AutoPlay = state } });
                    restores++;
                    continue;
                }
                long previous = controller.Steps;
                controller.Advance(1);
                callbacks += controller.Steps - previous;
                if (observedReturn && policy.State.Choice is null && policy.State.PendingRestore is null && !session!.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled)
                {
                    break;
                }
            }
            if (controller!.Error is null && policy.State.PendingRestore is null)
            {
                long previous = controller.Steps;
                controller.Advance(1);
                callbacks += controller.Steps - previous;
            }
            var coverage = policy.State.Coverage.Choices.Single(item => item.Key == choiceKey);
            Console.WriteLine($"Choice observation recovery: queued={repairQueued}, selected4={selectedAlternative}, actualReturn4={observedReturn}, callbacks={callbacks}, restores={restores}, seconds={timer.Elapsed.TotalSeconds:F3}, tick={session!.Machine.Tick}, completed={string.Join(',', coverage.CompletedOptions.Order())}, choice={policy.State.Choice?.Key}, active={policy.State.Active?.EventId}/{policy.State.Active?.Returned}, status={policy.State.Status}, error={controller!.Error}");
            if (!repairQueued || !selectedAlternative || !observedReturn || !coverage.CompletedOptions.Contains(4) || !coverage.CompletedOptions.Contains(3) || controller.Error is not null || policy.State.Choice is not null || policy.State.PendingRestore is not null || session.Host.Process.PlayerLocked || session.Host.Process.MenuDisabled || originalFiles.Any(file => !file.Value.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(file.Key)))))
            {
                throw new InvalidOperationException("The registered native alternative did not replay and publish its actual return with intact source checkpoints.");
            }
        }
        finally
        {
            policy?.Dispose();
            session?.Dispose();
        }
    }
}
