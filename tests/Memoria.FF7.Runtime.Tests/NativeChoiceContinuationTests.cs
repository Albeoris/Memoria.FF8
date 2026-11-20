using System.Diagnostics;
using System.Reflection;
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
        TalkConditionDomainTests.Run();
        Ff7SessionSave primary = Fixture(100);
        Ff7SessionSave better = Fixture(200);
        FieldChoiceServiceProof proof = new(true, [6], [9], [0]);
        string context = Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary, proof);
        byte[] stepCounter = primary.Vm.PersistentVariables.ToArray();
        stepCounter[774] = 200;
        stepCounter[775] = 1;
        Check(context != Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary with { Vm = primary.Vm with { PersistentVariables = stepCounter } }, proof), "An uncertified step-counter read was discarded.");
        Check(Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary, proof with { FieldStepCounterIndependent = true }) == Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary with { Vm = primary.Vm with { PersistentVariables = stepCounter } }, proof with { FieldStepCounterIndependent = true }), "A proved step-independent service preference changed solely with walking count.");
        Check(context == Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary with { Progress = primary.Progress with { Encounters = primary.Progress.Encounters with { MovementCounter = 32 } } }, proof), "A nonmoving certified service preference changed solely with the encounter movement phase.");
        Check(context != Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary with { Progress = primary.Progress with { Encounters = primary.Progress.Encounters with { Danger = 1 } } }, proof), "Encounter danger was discarded from the service context.");
        Check(context != Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary with { Progress = primary.Progress with { Characters = [primary.Progress.Characters[0] with { HitPoints = 9 }] } }, proof), "A native injury reused the old service context.");
        Check(Ff7AutoPlayChoiceWorkflow.SelectCompletedOutcome(3, [new(2, 1, false)]) == 2 && Ff7AutoPlayChoiceWorkflow.SelectCompletedOutcome(3, [new(2, 0, true)]) == 2, "A remembered service preference overrode genuine story or item progression.");
        Check(JsonSerializer.Deserialize<Ff7AutoPlayState>("{}")!.ChoicePreferences.Count == 0, "A legacy save invented completed-choice preference metadata.");
        string before = JsonSerializer.Serialize(better);
        Check(Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better, proof) && before == JsonSerializer.Serialize(better), "An actual lower-cost same-benefit continuation was rejected or changed.");
        Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(primary, better, proof with { Complete = false }), "An unknown native effect certificate selected a branch.");
        byte[] changed = better.Vm.PersistentVariables.ToArray();
        changed[30] = 1;
        Check(context != Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(primary with { Vm = primary.Vm with { PersistentVariables = changed } }, proof), "Changed meaningful native flags reused the old service context.");
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
        Check(proof.FieldStepCounterIndependent, "A service without native step-counter reads did not certify independence.");
        foreach (byte address in new byte[] { 5, 6, 7 })
        {
            byte[] reading = [0x81, 0x6E, 6, address, 0, 0];
            ushort[][] readingEntries = [new ushort[32], new ushort[32]];
            readingEntries[1][1] = 2;
            FieldMachine reader = new(new FieldProgram([0, 0, .. reading], readingEntries));
            reader.Initialize();
            FieldChoiceServiceProof dependent = reader.ProveChoiceService(1, 2, [new(1, 1)]);
            Check(dependent.Complete && !dependent.FieldStepCounterIndependent, "A valid word alias of the walking counter was certified independent or unsupported: " + dependent.Reason);
        }
        ushort[][] windowEntries = [new ushort[32], new ushort[32]];
        windowEntries[1][1] = 2;
        FieldMachine windowReader = new(new FieldProgram([0, 0, 0x42, 0x0E, 0, 0, 5, 0, 0], windowEntries));
        windowReader.Initialize();
        FieldChoiceServiceProof windowProof = windowReader.ProveChoiceService(1, 2, [new(1, 1)]);
        Check(windowProof.Complete && !windowProof.FieldStepCounterIndependent, "A deferred window word-parameter read was certified independent or unsupported: " + windowProof.Reason);
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
        CombinedSessionSave combined = CombinedSessionCodec.Read(input);
        Ff7SessionSave source = combined.Ff7!;
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), source);
        Ff7Dialogue prompt = session.Host.Process.Dialogues.Single(dialogue => !dialogue.Completed && dialogue.FirstChoice is not null);
        FieldHostRequest owner = (source.Vm.PendingRequests ?? []).Single(request => request.Id == prompt.RequestId);
        int[] guestIds = combined.Mappings.Where(mapping => mapping.StoryGame == SessionGame.Ff7 && mapping.AppearanceGame == SessionGame.Ff8).Select(mapping => mapping.StoryCharacter).Distinct().ToArray();
        ModeledGuestBridge? guest = guestIds.Length == 0 ? null : new(guestIds);
        session.GuestFieldVitals = guest;
        Ff7SessionSave[] results = new Ff7SessionSave[2];
        CombinedSessionSave[] envelopes = new CombinedSessionSave[2];
        FieldChoiceServiceProof[] proofs = new FieldChoiceServiceProof[2];
        for (int option = 0; option < 2; option++)
        {
            using Ff7Session branch = session.ForkAutoPlayNavigation();
            ModeledGuestBridge? branchGuest = guest?.Clone();
            branch.GuestFieldVitals = branchGuest;
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
            envelopes[option] = combined with { Ff7 = results[option], Ff8Payload = branchGuest?.Capture() ?? combined.Ff8Payload, Ff8PayloadFormat = branchGuest is null ? combined.Ff8PayloadFormat : "authored-service-vitals" };
            proofs[option] = branch.ReadChoiceServiceProof(owner.Entity, owner.ProgramCounter);
            Check(proofs[option].Complete, "The actual native service footprint was not certified: " + proofs[option].Reason);
        }
        HashSet<int> certified = proofs[1].ScratchMapBytes.Concat(proofs[1].PresentationMapBytes).ToHashSet();
        Check(results[1].Progress.Gil > results[0].Progress.Gil && results[0].Vm.MapVariables.Select((value, index) => (value, index)).Where(item => results[1].Vm.MapVariables[item.index] != item.value).All(item => certified.Contains(item.index)), "Actual service cost/map differences escaped the native certificate.");
        if (guest is not null)
        {
            Check(Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(results[0], results[1], proofs[1], out string? reason), "Actual guest-bridge native service returns failed dominance: " + reason);
            CheckFinishedReuse(session, combined, source, results[1], envelopes[0], prompt, guest, proofs[1]);
        }
        else
        {
            Check(!Ff7AutoPlayChoiceWorkflow.ActualContinuationDominates(results[0], results[1], proofs[1]), "A native-only recovery/status benefit was incorrectly discarded without actual guest state.");
        }
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
        Console.WriteLine("PASS actual owned service returns and same-key context preference with production guest-bridge contract; original source unchanged; guest vitals are an authored model");
    }

    private static void CheckFinishedReuse(Ff7Session session, CombinedSessionSave combined, Ff7SessionSave source, Ff7SessionSave actualNo, CombinedSessionSave actualYes, Ff7Dialogue prompt, ModeledGuestBridge guest, FieldChoiceServiceProof proof)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-service-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string key = source.AutoPlay!.Choice!.Key;
            Ff7AutoPlayState state = new() { Active = source.AutoPlay.Active, Choice = new(key, source.AutoPlay.Active) };
            AutoPlayChoiceCoverage coverage = new() { Key = key, FirstOption = prompt.FirstChoice!.Value, OptionCount = 2, ActiveOption = prompt.LastChoice, ContinuationCheckpoint = "primary" };
            state.Coverage.Choices.Add(coverage);
            CombinedSessionSave baseline = combined with { Ff7 = session.CaptureAutoPlayNavigation(), Ff8Payload = guest.Capture(), Ff8PayloadFormat = "authored-service-vitals" };
            string Write(string name, Ff7SessionSave snapshot)
            {
                string path = Path.Combine(directory, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name))) + ".ffsave");
                CombinedSessionCodec.Save(path, baseline with { Ff7 = snapshot, Ff8Payload = guest.Capture() });
                return path;
            }
            CombinedSessionSave Read(string path)
            {
                using FileStream input = File.OpenRead(path);
                return CombinedSessionCodec.Read(input);
            }
            string? selected = Ff7AutoPlayChoiceWorkflow.PublishActualContinuation(state, state.Choice, coverage, actualNo, actualYes, Write, Read, baseline, proof);
            Check(selected is not null && state.ChoicePreferences.ContainsKey(key), "The actual dominant service option did not retain its BEFORE-choice context.");
            state.Choice = null;
            coverage.ActiveOption = null;
            coverage.CompletedOptions = [prompt.FirstChoice.Value, prompt.LastChoice!.Value];
            coverage.Finished = true;
            AutoPlayPlanner<Ff7AutoPlayDeferral> planner = new(state.Coverage);
            Ff7AutoPlayChoiceWorkflow workflow = new(session, planner, state, Write);
            Check(workflow.ChooseSelection(prompt) == prompt.LastChoice, "The SAME finished native choice did not reuse its actual dominant option under the unchanged BEFORE context.");
            Check(Directory.GetFiles(directory).Length == 1, "The completed-context observation file was not retired immediately.");
            guest.Hp = 1;
            guest.Mp = 1;
            guest.Status = 1;
            Check(workflow.ChooseSelection(prompt) == prompt.FirstChoice, "A guest injury/MP/status change reused the old decline preference.");
            Check(Directory.GetFiles(directory).Length == 1, "An invalid guest context leaked its observation checkpoint.");
            Check(state.ChoicePreferences[key].NativeContext == Ff7AutoPlayChoiceWorkflow.ReadServiceNativeContext(baseline.Ff7!, proof), "Preference validity was recorded from an AFTER-return phase instead of its BEFORE-choice decision context.");
        }
        finally
        {
            foreach (string path in Directory.GetFiles(directory))
            {
                File.Delete(path);
            }
            Directory.Delete(directory);
        }
    }

    private sealed class ModeledGuestBridge(int[] characters) : IFf7GuestFieldVitals
    {
        public ushort Hp { get; set; } = 100;
        public ushort Mp { get; set; } = 50;
        public int Status { get; set; }
        public IReadOnlyList<int> StoryCharacters => characters;
        public Ff7GuestFieldVitalsView Read() => new(characters[0], "authored guest", 1, Hp, 100);
        public void Recover(bool clearStatuses)
        {
            Hp = 100;
            Mp = 50;
            if (clearStatuses)
            {
                Status = 0;
            }
        }
        public void AddHitPoints(short amount) => Hp = (ushort)Math.Clamp(Hp + amount, 0, 100);
        public byte[] Capture() => JsonSerializer.SerializeToUtf8Bytes(new { Hp, Mp, Status });
        public ModeledGuestBridge Clone() => new(characters) { Hp = Hp, Mp = Mp, Status = Status };
    }
}
