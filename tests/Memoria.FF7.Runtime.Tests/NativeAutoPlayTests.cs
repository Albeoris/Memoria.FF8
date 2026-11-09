using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class NativeAutoPlayTests
{
    public static void Run()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed autoplay: original resources or external standalone fixture are unset.");
            return;
        }
        CombinedSessionSave envelope;
        using (FileStream input = File.OpenRead(path))
        {
            envelope = CombinedSessionCodec.Read(input);
        }
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        Ff7SessionSave? captured = null;
        Ff7AutoPlayController controller = new(session, _ => default, checkpoint: snapshot => captured = snapshot);
        long originalTick = session.Host.Process.Tick;
        controller.Start();
        controller.Pause();
        controller.Advance(3);
        Check(session.Host.Process.Tick == originalTick && controller.Steps == 0, "Paused autoplay advanced native simulation.");
        controller.Resume();
        controller.Advance(3);
        Check(session.Host.Process.Tick > originalTick && controller.Steps is > 0 and <= 3, "Resumed autoplay did not obey its scheduler budget.");
        controller.SaveAndStop();
        Check(!controller.Running && captured is not null, "Stopping autoplay omitted its standalone state.");
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, envelope with { Ff7 = captured });
        serialized.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(serialized).Ff7!);
        Check(JsonSerializer.Serialize(session.Capture()) == JsonSerializer.Serialize(cold.Capture()), "Autoplay standalone cold restoration changed native state.");
        string temporary = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ffsave");
        try
        {
            CombinedSessionCodec.Save(temporary, envelope);
            byte[] validBytes = File.ReadAllBytes(temporary);
            try
            {
                Memoria.AutoPlay.AutoPlayCheckpoint.Save(temporary, () => envelope with { ActiveGame = SessionGame.Ff7, Ff7 = null }, CombinedSessionCodec.Save);
                throw new Exception("Corrupt replacement checkpoint was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Check(File.ReadAllBytes(temporary).SequenceEqual(validBytes), "Codec failure changed the valid standalone checkpoint bytes.");
        }
        finally
        {
            File.Delete(temporary);
        }
        Ff7AutoPlayController failed = new(session, _ => throw new NotSupportedException("Explicit native policy boundary."), checkpoint: snapshot => captured = snapshot);
        failed.Start();
        failed.Advance();
        Check(!failed.Running && failed.Error!.Contains("Explicit native policy boundary.") && captured!.Vm.Tick == session.Machine.Tick, "Autoplay masked the native failure or failed to checkpoint its exact initialized boundary.");
        Exception nativeFailure = new NotSupportedException("Preserve this native failure.");
        int writes = 0;
        Ff7AutoPlayController writeFailure = new(session, _ => throw nativeFailure, checkpoint: _ =>
        {
            if (++writes > 1)
            {
                throw new IOException("Checkpoint destination unavailable.");
            }
        });
        writeFailure.Start();
        writeFailure.Advance();
        Check(ReferenceEquals(writeFailure.Failure, nativeFailure) && writeFailure.Error!.Contains("Checkpoint destination unavailable."), "Checkpoint writer failure replaced the original native exception or omitted its diagnostic.");
    }

    public static void RunCompletion()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed autoplay completion: original resources or external standalone fixture are unset.");
            return;
        }
        byte[] sourceBytes = File.ReadAllBytes(path);
        using MemoryStream input = new(sourceBytes, false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7SessionSave source = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session reached = Ff7Session.Restore(installation, source);
        string unchanged = JsonSerializer.Serialize(reached.Capture());
        Ff7SessionSave? final = null;
        Ff7AutoPlayController alreadyReached = new(reached, _ => throw new Exception("Completed autoplay requested another input."), present: () => throw new Exception("Completed autoplay advanced presentation."), checkpoint: snapshot => final = snapshot, hasReachedGoal: () => true);
        alreadyReached.Start();
        alreadyReached.Advance(8);
        Check(alreadyReached.Completed && !alreadyReached.Running && alreadyReached.Steps == 0 && alreadyReached.Error is null && alreadyReached.Failure is null && final is not null, "An already reached target did not checkpoint and stop successfully.");
        Check(JsonSerializer.Serialize(reached.Capture()) == unchanged && JsonSerializer.Serialize(final) == unchanged, "Completing an already reached target changed native state.");
        Check(alreadyReached.Status.StartsWith("Completed: ", StringComparison.Ordinal), "Successful target completion omitted its visible status.");
        alreadyReached.Start();
        Check(!alreadyReached.Completed && alreadyReached.Running, "Starting autoplay retained its previous completion state.");
        alreadyReached.SaveAndStop();
        Check(!alreadyReached.Completed && !alreadyReached.Running, "Manual stop was reported as target completion.");

        using Ff7Session stepped = Ff7Session.Restore(installation, source);
        long originalTick = stepped.Host.Process.Tick;
        int choices = 0;
        Ff7AutoPlayController afterStep = new(stepped, _ =>
        {
            choices++;
            return default;
        }, hasReachedGoal: () => stepped.Host.Process.Tick > originalTick);
        afterStep.Start();
        afterStep.Advance(8);
        Check(afterStep.Completed && !afterStep.Running && afterStep.Steps == 1 && choices == 1 && stepped.Host.Process.Tick == originalTick + 1, "Autoplay continued past the completed native target step.");

        int lastEntity = source.Vm.Entities.Length - 1;
        Ff7SessionSave tailFixture = source with { Vm = source.Vm with { CycleCursor = new Memoria.FF7.Events.FieldCycleCursor(lastEntity, 0, source.Vm.Entities[lastEntity].ProgramCounter) }, TickContinuation = new(source.Vm.Input ?? throw new InvalidDataException("The completion tail fixture requires its saved native input.")) };
        using Ff7Session pending = Ff7Session.Restore(installation, tailFixture);
        string clockBefore = JsonSerializer.Serialize(pending.Progress.Clock);
        Ff7AutoPlayController finishPending = new(pending, _ => throw new Exception("A pending native cycle requested a new input."), hasReachedGoal: () =>
        {
            Check(!pending.HasPendingTick, "Target completion inspected an incomplete native cycle.");
            return true;
        });
        finishPending.Start();
        finishPending.Advance(8);
        Check(finishPending.Completed && finishPending.Steps == 1 && !pending.HasPendingTick && pending.Machine.Tick == source.Vm.Tick + 1 && JsonSerializer.Serialize(pending.Progress.Clock) == clockBefore, "Target completion skipped or repeated the pending cycle prefix instead of finishing its tail once.");

        using Ff7Session failed = Ff7Session.Restore(installation, source);
        IOException writerError = new("Completion checkpoint destination unavailable.");
        int writes = 0;
        Ff7AutoPlayController failedCheckpoint = new(failed, _ => throw new Exception("A reached target requested input after its checkpoint failed."), checkpoint: _ =>
        {
            if (++writes > 1)
            {
                throw writerError;
            }
        }, hasReachedGoal: () => true);
        failedCheckpoint.Start();
        failedCheckpoint.Advance(8);
        Check(!failedCheckpoint.Completed && !failedCheckpoint.Running && ReferenceEquals(failedCheckpoint.Failure, writerError) && failedCheckpoint.Error!.Contains(writerError.Message) && failedCheckpoint.Steps == 0, "Checkpoint failure was reported as successful target completion or lost its original exception.");
        Check(File.ReadAllBytes(path).SequenceEqual(sourceBytes), "Completion checks changed their authoritative external standalone.");
    }
    public static void RunGoal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AUTOPLAY_GOAL_CHECKPOINT");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP installed autoplay goal: original resources or external goal fixture are unset.");
            return;
        }
        byte[] sourceBytes = File.ReadAllBytes(path);
        using MemoryStream input = new(sourceBytes, false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(input);
        Ff7SessionSave source = envelope.Ff7!;
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, source);
        Ff7OpeningAutoPlayPolicy policy = new(session);
        Check(policy.HasReachedGoal(), "The observed post-fall native dialogue did not satisfy the actual route goal.");
        string nativeBefore = JsonSerializer.Serialize(session.Capture());
        Ff7SessionSave? checkpointed = null;
        Ff7AutoPlayController controller = new(session, _ => throw new Exception("Arrival completion requested another control input."), checkpoint: state => checkpointed = state, hasReachedGoal: policy.HasReachedGoal);
        controller.Start();
        controller.Advance(8);
        Check(controller.Completed && !controller.Running && controller.Failure is null && controller.Error is null && controller.Steps == 0 && checkpointed is not null, "Observed arrival did not checkpoint and complete cleanly without a new tick.");
        Check(JsonSerializer.Serialize(session.Capture()) == nativeBefore && JsonSerializer.Serialize(checkpointed) == nativeBefore && !session.CanSave, "Arrival completion changed native state, confirmed the dialogue or forced native save permission.");

        Ff7Actor[] hiddenActors = source.Process.Actors.Select(actor => actor.Entity == 6 ? actor with { Visible = false } : actor).ToArray();
        using Ff7Session hidden = Ff7Session.Restore(installation, source with { Process = source.Process with { Actors = hiddenActors } });
        Check(!new Ff7OpeningAutoPlayPolicy(hidden).HasReachedGoal(), "An invisible meeting actor was accepted as visible church arrival.");
        Ff7Dialogue target = source.Process.Dialogues.Single(dialogue => dialogue.Window == 1 && dialogue.Text == 18 && !dialogue.Completed);
        Ff7Dialogue queued = Ff7DialogueCursor.Confirm(target, FieldText.Read(session.Field.Scripts.Texts[target.Text].Span));
        using Ff7Session confirming = Ff7Session.Restore(installation, source with { Process = source.Process with { Dialogues = source.Process.Dialogues.Select(dialogue => dialogue.RequestId == target.RequestId ? queued : dialogue).ToArray() } });
        Check(!new Ff7OpeningAutoPlayPolicy(confirming).HasReachedGoal(), "A queued dialogue transition was accepted as a stable arrival boundary.");
        using Ff7Session obscured = Ff7Session.Restore(installation, source with { Process = source.Process with { FadeState = source.Process.FadeState! with { QuadColorArgb = 0xFF000000 } } });
        Check(!new Ff7OpeningAutoPlayPolicy(obscured).HasReachedGoal(), "An opaque fade was accepted as visible church arrival.");
        using Ff7Session legacy = Ff7Session.Restore(installation, source with { Process = source.Process with { PreviousMap = null } });
        Check(new Ff7OpeningAutoPlayPolicy(legacy).HasReachedGoal(), "An absent legacy origin marker prevented recognition of the same native visible goal.");
        Check(File.ReadAllBytes(path).SequenceEqual(sourceBytes), "Goal verification changed the authoritative arrival checkpoint bytes.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
