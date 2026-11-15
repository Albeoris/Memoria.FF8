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
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during world arrival."));
        Ff7AutoplayDriver driver = new(session);
        Ff7AutoplayInput input = policy.ChooseInput(driver);
        Check(policy.State.Active is null || policy.State.Active.Kind == Ff7AutoPlayTargetKind.WorldMesh, "The world policy still waits for a destroyed field allocation.");
        var oldCoverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == prior.VisitId).Events.Single(item => item.Event.Id == prior.EventId);
        Check(oldCoverage.Completed == previouslyCompleted, "An interrupted source changed its recorded completion.");
        long tick = session.World!.Process.Tick;
        driver.Apply(input);
        session.AdvanceTick(advanceClock: false);
        Check(session.World!.Process.Tick == tick + 1, "Normal world continuation did not consume its next native callback.");
        Check(SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(hash), "The original world arrival source changed.");
        Console.WriteLine("PASS original world arrival releases a destroyed field owner without fabricating completion and resumes native input");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
