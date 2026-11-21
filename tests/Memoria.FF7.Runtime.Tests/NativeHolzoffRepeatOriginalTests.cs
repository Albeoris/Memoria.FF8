using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeHolzoffRepeatOriginalTests
{
    internal static void Run()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_HOLZOFF_TUTORIAL_SAVE") ?? throw new InvalidOperationException("Select a native introduction repeat fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave original = CombinedSessionCodec.Read(input).Ff7 ?? throw new InvalidDataException("The fixture has no native session.");
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        Dictionary<string, Ff7SessionSave> checkpoints = new(StringComparer.Ordinal);
        List<string> reads = [];
        string repeatKey = original.AutoPlay?.Choice?.Key ?? throw new InvalidDataException("The fixture has no repeated choice.");
        string parentKey = original.AutoPlay.Choice.Parent?.Key ?? throw new InvalidDataException("The fixture has no finite parent.");
        Ff7Session session = Ff7Session.Restore(installation, original);
        Ff7AutoPlayPolicy? policy = null;
        Ff7AutoPlayController? controller = null;
        long completedSteps = 0;
        int restores = 0;
        bool selectedExit = false;
        bool repeatReturned = false;
        Stopwatch timer = Stopwatch.StartNew();
        try
        {
            Start();
            for (int callback = 0; callback < 12000 && timer.Elapsed.TotalSeconds < 30 && controller!.Error is null && !controller.Completed; callback++)
            {
                controller.Advance(1);
                if (policy!.State.PendingRestore is not Ff7AutoPlayRestoreRequest restore)
                {
                    continue;
                }
                Ff7AutoPlayState state = policy.State.Clone();
                state.PendingRestore = null;
                Ff7SessionSave restored;
                if (!checkpoints.TryGetValue(restore.Path, out restored!))
                {
                    using FileStream saved = File.OpenRead(restore.Path);
                    restored = CombinedSessionCodec.Read(saved).Ff7 ?? throw new InvalidDataException("The registered choice checkpoint has no native session.");
                    reads.Add(restore.Path);
                }
                completedSteps += controller.Steps;
                policy.Dispose();
                session.Dispose();
                session = Ff7Session.Restore(installation, restored with { AutoPlay = state });
                restores++;
                Start();
            }
            Ff7AutoPlayState final = policy!.State;
            var repeat = final.Coverage.Choices.Single(choice => choice.Key == repeatKey);
            var parent = final.Coverage.Choices.Single(choice => choice.Key == parentKey);
            Console.WriteLine($"Native introduction: callbacks={completedSteps + controller!.Steps}, seconds={timer.Elapsed.TotalSeconds:F3}, selectedExit={selectedExit}, nativeReturn={repeatReturned}, restores={restores}, repeatCompleted={string.Join(',', repeat.CompletedOptions)}, repeatDeferred={string.Join(',', repeat.PolicyDeferredOptions)}, parentCompleted={string.Join(',', parent.CompletedOptions)}, tick={session.Machine.Tick}, locked={session.Host.Process.PlayerLocked}/{session.Host.Process.MenuDisabled}, choice={final.Choice?.Key}, error={controller.Error}");
            foreach (string read in reads.Distinct(StringComparer.Ordinal))
            {
                Console.WriteLine($"Registered native choice read: {read}");
            }
            Ff7SessionSave actual = session.Capture();
            if (!controller.Completed || controller.Error is not null || !selectedExit || !repeatReturned || !repeat.CompletedOptions.Contains(2) || repeat.CompletedOptions.Contains(1) || !repeat.PolicyDeferredOptions.Contains(1) || !parent.CompletedOptions.Contains(3) || !parent.CompletedOptions.Contains(4) || final.Choice is not null || final.PendingRestore is not null || JsonSerializer.Serialize(original.Progress.Inventory) != JsonSerializer.Serialize(actual.Progress.Inventory) || original.Progress.BattleRng?.CrtSeed != actual.Progress.BattleRng?.CrtSeed || new[] { 0x45, 0x46, 0x82, 0xC5, 0xC6, 256 + 0xCE }.Any(address => original.Vm.PersistentVariables[address] != actual.Vm.PersistentVariables[address]) || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
            {
                throw new InvalidOperationException("The native introduction did not leave its repeat loop and retain ordinary finite parent completion unchanged.", controller.Failure);
            }
        }
        finally
        {
            policy?.Dispose();
            session.Dispose();
        }

        void Start()
        {
            policy = new(session, (key, native) =>
            {
                string checkpoint = "memory:" + key;
                checkpoints[checkpoint] = native;
                return checkpoint;
            });
            int? Choose(Ff7Dialogue dialogue)
            {
                int? selected = policy.ChooseDialogueSelection(dialogue);
                if (dialogue is { Window: 0, Text: 11, FirstChoice: 1, LastChoice: 2 })
                {
                    selectedExit |= selected == 2;
                    if (selected != 2 || policy.State.PendingRestore is not null)
                    {
                        throw new InvalidOperationException("The current native repeat prompt selected its looping branch or restored before the live exit.");
                    }
                }
                return selected;
            }
            bool Ready()
            {
                bool native = session.Machine.Capture().Entities[1].ProgramCounter == 0x05C2 && !session.Host.Process.PlayerLocked && !session.Host.Process.MenuDisabled && !session.Host.Process.Dialogues.Any(dialogue => !dialogue.Completed) && (session.Machine.Variables.ReadByte(Memoria.FF7.Events.FieldBank.Persistent0Byte, 0x83) & 0x80) != 0;
                repeatReturned |= selectedExit && native;
                return repeatReturned && native && policy.State.Choice is null && policy.State.PendingRestore is null;
            }
            controller = new(session, policy.ChooseInput, advanceTick: () => session.AdvanceTick(advanceClock: false), chooseDialogueSelection: Choose, hasReachedGoal: Ready, readPlanningProgress: () => policy.WorkProgress, readFrontierProgress: () => policy.FrontierProgress);
            controller.Start();
        }
    }
}
