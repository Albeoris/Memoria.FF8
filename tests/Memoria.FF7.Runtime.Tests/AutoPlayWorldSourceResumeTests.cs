using System.Security.Cryptography;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldSourceResumeTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Configure original world resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_SOURCE_RESUME_SAVE") ?? throw new InvalidOperationException("Configure an external standalone world arrival.");
        byte[] bytes = File.ReadAllBytes(path);
        byte[] hash = SHA256.HashData(bytes);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Ff7AutoPlayActiveEvent prior = saved.AutoPlay!.Active!;
        bool previouslyCompleted = saved.AutoPlay.Coverage.Visits.Single(visit => visit.Visit.Key == prior.VisitId).Events.Single(item => item.Event.Id == prior.EventId).Completed;
        Check(saved.World is not null && prior.Kind != Ff7AutoPlayTargetKind.WorldMesh && prior.Generation != saved.FieldGeneration && !prior.TransitionOwned, "The supplied world arrival does not reproduce a destroyed field owner.");
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), saved);
        using Ff7AutoPlayPolicy policy = new(session, (key, _) => key);
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayInput input = policy.ChooseInput(driver);
        Check(policy.State.PendingRestore is not null || policy.State.Active is null || policy.State.Active.Kind == Ff7AutoPlayTargetKind.WorldMesh, "The world policy still waits for a destroyed field allocation.");
        var oldCoverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == prior.VisitId).Events.Single(item => item.Event.Id == prior.EventId);
        Check(oldCoverage.Completed == previouslyCompleted, "An interrupted source changed its recorded completion.");
        long tick = session.World!.Process.Tick;
        driver.Apply(input);
        session.AdvanceTick(advanceClock: false);
        Check(session.World!.Process.Tick == tick + 1, "Normal world continuation did not consume its next native callback.");
        Check(SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(hash), "The original world arrival source changed.");
        Console.WriteLine("PASS original world arrival releases a destroyed field owner without fabricating completion and resumes native input");
        if (Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_CONTROL_REASON_SAVE") is { } reasonPath)
        {
            ResumeEnabledControlWithRetainedReason(root, reasonPath);
        }
    }

    private static void ResumeEnabledControlWithRetainedReason(string root, string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream stream = new(bytes, writable: false);
        Ff7SessionSave saved = CombinedSessionCodec.Read(stream).Ff7!;
        Check(saved.World is { MovementEnabled: true, ControlLock: not 0 }, "The supplied source does not retain an enabled native control reason.");
        Ff7Installation installation = Ff7Installation.Discover(root);
        using (Ff7Session stationary = Ff7Session.Restore(installation, saved))
        {
            Ff7AutoPlayController stalled = new(stationary, _ => default);
            stalled.Start();
            for (int batch = 0; batch < 4000 && stalled.Steps <= Ff7AutoPlayProgressWatchdog.IdleStepLimit + 16 && stalled.Running; batch++)
            {
                stalled.ResetIdleDeadline();
                stalled.Advance(900);
            }
            Check(!stalled.Running && stalled.Failure is TimeoutException, $"Repeated host batches concealed a stationary native world: steps={stalled.Steps}, failure={stalled.Error}");
            Console.WriteLine($"PASS actual stationary world stops after {stalled.Steps} steps despite host deadline resets");
        }
        Dictionary<string, Ff7SessionSave> checkpoints = [];
        string WriteCheckpoint(string key, Ff7SessionSave snapshot)
        {
            checkpoints.Add(key, snapshot);
            return key;
        }
        Ff7Session session = Ff7Session.Restore(installation, saved);
        Ff7AutoPlayPolicy policy = new(session, WriteCheckpoint);
        Ff7WorldEntity player = session.World!.Process.Entities.Single(entity => entity.Model == session.World.Process.ControlledModel);
        Ff7AutoPlayController controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
        controller.Start();
        for (int step = 0; step < 256 && controller.Running && session.World is not null; step++)
        {
            controller.Advance(1);
            if (policy.State.PendingRestore is { } restore)
            {
                Ff7AutoPlayState state = policy.State.Clone();
                state.PendingRestore = null;
                Ff7SessionSave next = checkpoints[restore.Path] with { AutoPlay = state };
                policy.Dispose();
                session.Dispose();
                session = Ff7Session.Restore(installation, next);
                policy = new(session, WriteCheckpoint);
                controller = new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection);
                controller.Start();
            }
            Ff7WorldEntity current = session.World!.Process.Entities.Single(entity => entity.Model == session.World.Process.ControlledModel);
            if (current.X != player.X || current.Z != player.Z)
            {
                Check(session.World.Process.ControlLock == saved.World!.ControlLock, "Normal input erased the native retained reason.");
                Check(File.ReadAllBytes(path).SequenceEqual(bytes), "The original control reason source changed.");
                Console.WriteLine("PASS actual enabled world control with retained reason resumes normal autoplay movement");
                policy.Dispose();
                session.Dispose();
                return;
            }
        }
        string failure = controller.Error ?? policy.State.Status ?? "No native movement";
        policy.Dispose();
        session.Dispose();
        throw new InvalidOperationException($"Enabled world control did not resume physical movement: {failure}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
