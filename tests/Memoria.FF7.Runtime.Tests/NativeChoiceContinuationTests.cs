using System.Diagnostics;
using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeChoiceContinuationTests
{
    internal static void Run()
    {
        Ff7SessionSave primary = Fixture(100);
        Ff7SessionSave better = Fixture(200);
        FieldChoiceServiceProof proof = new(true, [6], [9], [0]);
        string before = JsonSerializer.Serialize(better);
        Check(Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better, proof) && before == JsonSerializer.Serialize(better), "An actual lower-cost same-benefit continuation was rejected or changed.");
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better, proof with { Complete = false }), "An unknown native effect certificate selected a branch.");
        byte[] changed = better.Vm.PersistentVariables.ToArray();
        changed[30] = 1;
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better with { Vm = better.Vm with { PersistentVariables = changed } }, proof), "A native progress flag was discarded as presentation.");
        byte[] map = better.Vm.MapVariables.ToArray();
        map[7] = 1;
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better with { Vm = better.Vm with { MapVariables = map } }, proof), "An unproved map flag was discarded.");
        Ff7Actor actor = better.Process.Actors[0];
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better with { Process = better.Process with { Actors = [actor with { Direction = 64 }] } }, proof), "A host-visible actor heading was discarded as presentation.");
        var suspended = better.Vm.Entities[0] with { SavedProgramCounters = [7, 0, 0, 0, 0, 0, 0, 0] };
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better with { Vm = better.Vm with { Entities = [suspended] } }, proof), "Suspended native work was discarded.");
        byte[] status = better.Progress.Characters[0].NativeRecord.ToArray();
        status[31] = 1;
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better with { Progress = better.Progress with { Characters = [better.Progress.Characters[0] with { NativeRecord = status }] } }, proof), "Different healing/status benefits were discarded.");
        CheckCandidatePublication(primary, better);
        CheckCertificate();
        Console.WriteLine("PASS actual service continuation utility, native flags/heading/suspended ownership, guest barrier and three-option immutable checkpoint selection");
    }

    private static Ff7SessionSave Fixture(uint gil)
    {
        byte[] record = new byte[64];
        record[44] = record[48] = record[56] = record[58] = 10;
        return new()
        {
            ResourceProfile = "authored",
            Language = "en",
            FieldName = "authored",
            FieldGeneration = 1,
            Progress = new() { InitialKernelData = [], Characters = [new(0, "authored", 1, 0, 10, 10, record)], Party = [0], Inventory = [], Materia = [], Gil = gil },
            Vm = new(1, "authored", "program", 0, true, new byte[1280], new byte[256], [new(0, 7, 0, new int[8], new FieldSyncState[8], Enumerable.Repeat(-1, 8).ToArray(), true)]),
            Process = new() { Actors = [new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true }] }
        };
    }

    private static void CheckCandidatePublication(Ff7SessionSave primary, Ff7SessionSave better)
    {
        CombinedSessionSave envelope = new() { Ff7 = primary, Ff8Payload = [1], Ff8PayloadFormat = "authored" };
        Dictionary<string, CombinedSessionSave> files = new() { ["primary"] = envelope };
        Ff7AutoPlayState state = new() { Choice = new("choice", null) };
        AutoPlayChoiceCoverage coverage = new() { Key = "choice", FirstOption = 2, OptionCount = 4, ActiveOption = 3, ContinuationCheckpoint = "primary" };
        byte guest = 1;
        bool corruptNative = false;
        string Write(string key, Ff7SessionSave snapshot)
        {
            Check(!files.ContainsKey(key), "A later branch overwrote an already selected actual checkpoint.");
            files.Add(key, envelope with { Ff7 = corruptNative ? snapshot with { Progress = snapshot.Progress with { Gil = 999 } } : snapshot, Ff8Payload = [guest] });
            return key;
        }
        string selected = Ff7AutoPlayChoiceWorkflow.PublishActualContinuation(state, state.Choice, coverage, better, envelope, Write, path => files[path])!;
        Check(selected is not null && coverage.ContinuationCheckpoint == selected, "A genuinely executed guest-equivalent branch was not selected.");
        string retained = JsonSerializer.Serialize(files[selected!]);
        coverage.ActiveOption = 4;
        guest = 2;
        Check(Ff7AutoPlayChoiceWorkflow.PublishActualContinuation(state, state.Choice!, coverage, Fixture(300), files[selected!], Write, path => files[path]) is null, "Changed actual guest state was discarded.");
        Check(coverage.ContinuationCheckpoint == selected && retained == JsonSerializer.Serialize(files[selected!]) && state.Choice!.RetiredCandidatePaths!.Contains("choice-candidate-4"), "A rejected later option changed the selected actual continuation or leaked its candidate.");
        coverage.ActiveOption = 5;
        guest = 1;
        corruptNative = true;
        Check(Ff7AutoPlayChoiceWorkflow.PublishActualContinuation(state, state.Choice!, coverage, Fixture(300), files[selected!], Write, path => files[path]) is null && coverage.ContinuationCheckpoint == selected && retained == JsonSerializer.Serialize(files[selected!]), "A wrong native checkpoint with identical guest metadata replaced the real selected branch.");
    }

    private static void CheckCertificate()
    {
        byte[] body = [0x80, 0x50, 6, 1, 0x48, 0x05, 3, 0, 2, 3, 6, 0x3B, 0x66, 20, 18, 0];
        ushort[][] entries = [new ushort[32], new ushort[32]];
        entries[1][1] = 2;
        FieldMachine machine = new(new FieldProgram([0, 0, .. body], entries));
        machine.Initialize();
        FieldChoiceServiceProof proof = machine.ProveChoiceService(1, 6, [new(1, 1)]);
        Check(proof.Complete && proof.ScratchMapBytes.Contains(6) && proof.ScratchMapBytes.Contains(21), $"Choice/query outputs did not use the native byte/word alias footprint: {proof.Reason}");
        entries[0][1] = (ushort)(2 + body.Length);
        FieldMachine consumer = new(new FieldProgram([0, 0, .. body, 0x3A, 0x60, 6, 0, 0, 0, 0], entries));
        consumer.Initialize();
        Check(!consumer.ProveChoiceService(1, 6, [new(1, 1), new(0, 1)]).Complete, "A future word reader of the choice byte was ignored.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    internal static void RunOriginal()
    {
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOICE_SERVICE_SAVE") ?? throw new InvalidOperationException("Select a native choice fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        Ff7SessionSave source = CombinedSessionCodec.Read(input).Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        Ff7Dialogue prompt = session.Host.Process.Dialogues.Single(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null);
        FieldHostRequest owner = (source.Vm.PendingRequests ?? []).Single(request => request.Id == prompt.RequestId);
        Ff7SessionSave[] results = new Ff7SessionSave[2];
        FieldChoiceServiceProof[] proofs = new FieldChoiceServiceProof[2];
        for (int option = 0; option < 2; option++)
        {
            using Ff7Session branch = session.ForkAutoPlayNavigation();
            int selection = prompt.FirstChoice!.Value + option;
            bool selected = false;
            bool returned = false;
            branch.NativeTickCompleted += tick => returned |= tick.ScriptReturns.Any(item => item.Entity == owner.Entity && item.Priority == owner.Priority);
            Stopwatch timer = Stopwatch.StartNew();
            int ticks = 0;
            while ((!returned || !Ff7AutoPlayDiscovery.NativeControlReady(branch)) && ticks < 1200 && timer.Elapsed < TimeSpan.FromSeconds(8))
            {
                if (!branch.HasPendingTick)
                {
                    foreach (Ff7Dialogue dialogue in branch.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed && dialogue.AwaitingInput && branch.Host.CanConfirmDialogue(dialogue.RequestId)).ToArray())
                    {
                        Ff7Dialogue acknowledged = dialogue;
                        if (dialogue.FirstChoice is not null)
                        {
                            if (selected || dialogue.RequestId != prompt.RequestId)
                            {
                                throw new InvalidOperationException("Another native choice invalidated this bounded projection.");
                            }
                            acknowledged = Ff7DialogueCursor.Select(dialogue, selection);
                            selected = true;
                        }
                        branch.Host.UpdateDialogue(Ff7DialogueCursor.Confirm(acknowledged, FieldText.Read(branch.Field.Scripts.Texts[dialogue.Text].Span)));
                    }
                    branch.SetInput(0, 0);
                }
                branch.AdvanceTick();
                ticks++;
                if (branch.Battle is not null || branch.World is not null || branch.FieldGeneration != source.FieldGeneration)
                {
                    throw new InvalidOperationException("A native handoff invalidated this bounded projection.");
                }
            }
            if (!returned || !selected || !Ff7AutoPlayDiscovery.NativeControlReady(branch))
            {
                throw new InvalidOperationException("The projected native choice did not return owned control.");
            }
            results[option] = branch.CaptureAutoPlayNavigation();
            proofs[option] = branch.ReadChoiceServiceProof(owner.Entity, owner.ProgramCounter);
            Check(proofs[option].Complete, "The actual native service footprint was not certified: " + proofs[option].Reason);
        }
        HashSet<int> certified = proofs[1].ScratchMapBytes.Concat(proofs[1].PresentationMapBytes).ToHashSet();
        Check(results[1].Progress.Gil > results[0].Progress.Gil && results[0].Vm.MapVariables.Select((value, index) => (value, index)).Where(item => results[1].Vm.MapVariables[item.index] != item.value).All(item => certified.Contains(item.index)), "Actual service cost/map differences escaped the native certificate.");
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(results[0], results[1], proofs[1]), "A native-only recovery/status benefit was incorrectly discarded without actual guest state.");
        for (int index = 0; index < results[0].Vm.Entities.Length; index++)
        {
            var first = results[0].Vm.Entities[index];
            var second = results[1].Vm.Entities[index];
            Check(first.Priority == second.Priority && first.SavedProgramCounters.AsSpan().SequenceEqual(second.SavedProgramCounters) && JsonSerializer.Serialize(first.SyncStates) == JsonSerializer.Serialize(second.SyncStates) && JsonSerializer.Serialize(first.SyncOwners) == JsonSerializer.Serialize(second.SyncOwners), "The compared service returns do not have the same native ownership.");
        }
        if (!bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("The diagnostic changed its original source.");
        }
        Console.WriteLine("PASS actual owned service returns, certified byte/word scratch and ambient footprint, native healing barrier; source unchanged; no guest-state equivalence claim");
    }
}
