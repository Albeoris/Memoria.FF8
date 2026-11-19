using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeActorFrontierTests
{
    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTOR_FRONTIER_SAVE") ?? throw new InvalidOperationException("External actor frontier required.");
        byte[] bytes = File.ReadAllBytes(path);
        Ff7Installation installation = Ff7Installation.Discover(root);
        Run(installation, bytes, false);
        Run(installation, bytes, true);
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The external authoritative source changed.");
    }

    private static void Run(Ff7Installation installation, byte[] bytes, bool cold)
    {
        using MemoryStream stream = new(bytes, false);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoPlayPolicy policy = CreatePolicy(session);
        Ff7AutoPlayActiveEvent owner = policy.State.Active ?? throw new InvalidOperationException("No retained native actor approach.");
        string attempts = JsonSerializer.Serialize(policy.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.UnavailableAttempts));
        // Explicit new-attempt supervision matches the App start contract; no native/refusal state changes.
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        long start = session.Host.Process.Tick;
        bool restored = false;
        bool admitted = false;
        int quanta = 0;
        void ObserveInteraction(int entity, int script, bool accepted)
        {
            if (accepted)
            {
                Check(entity == owner.Entity && script == owner.Script, "An unselected actor was admitted.");
                admitted = true;
            }
        }
        Ff7AutoPlayController CreateController()
        {
            session.InteractionScriptRequested += ObserveInteraction;
            return new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        }
        Ff7AutoPlayController controller = CreateController();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            controller.Start();
            while (controller.Running && !admitted && session.Battle is null && session.Host.Process.Tick - start < 96 && quanta++ < 512 && elapsed.Elapsed < TimeSpan.FromSeconds(30))
            {
                controller.Advance(1);
                Check(policy.State.Active is { } active && active.EventId == owner.EventId && active.ConditionFingerprint == owner.ConditionFingerprint && active.Generation == owner.Generation && active.Entity == owner.Entity && active.Script == owner.Script && active.NavigationKey == owner.NavigationKey, "The advancing ordinary approach lost its exact owner.");
                if (cold && !restored && session.Host.Process.Tick - start >= 19)
                {
                    Ff7SessionSave saved = session.Capture();
                    saved.Validate();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, saved);
                    policy = CreatePolicy(session);
                    controller = CreateController();
                    controller.Start();
                    restored = true;
                }
            }
            Check(controller.Error is null && admitted && session.Battle is null && (!cold || restored) && policy.State.Active?.Accepted == true, "Normal/cold automatic actor admission did not reach its genuine native boundary.");
            Check(attempts == JsonSerializer.Serialize(policy.State.Coverage.Visits.SelectMany(visit => visit.Events).SelectMany(item => item.UnavailableAttempts)), "An owned approach erased/promoted native refusals.");
            Check(policy.State.NavigationGoal is null && policy.State.NativeRevalidation is null && policy.State.NativeRoute is null, "Ordinary actor admission installed an unrelated route/task capability.");
            Console.WriteLine($"PASS native actor admission cold:{cold}, native:{session.Host.Process.Tick - start}, scheduling:{quanta}, exactOwner:{owner.EventId}, accepted:{policy.State.Active!.Accepted}, retainedGuardCount:{policy.State.NavigationGuards.Count}");
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static Ff7AutoPlayPolicy CreatePolicy(Ff7Session session) => new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
