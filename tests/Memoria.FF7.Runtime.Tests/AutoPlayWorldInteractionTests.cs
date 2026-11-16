using System.Text.Json;
using Memoria.AutoPlay;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class AutoPlayWorldInteractionTests
{
    internal static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original world resources.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_WORLD_INTERACTION_SAVE") ?? throw new InvalidOperationException("Select an external exhausted field-entry frontier.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(root), CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World ?? throw new InvalidOperationException("The fixture has no native world allocation.");
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("A read-only planning query wrote a dialogue checkpoint."));
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        foreach (var visit in policy.State.WorldVisits.Values)
        {
            visit.DiscoveryComplete = true;
        }
        string before = JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
        Ff7AutoplayDriver driver = new(session);
        for (int step = 0; step < 1100 && policy.State.Active is null; step++)
        {
            _ = policy.ChooseInput(driver);
        }
        Ff7AutoPlayActiveEvent active = policy.State.Active ?? throw new InvalidOperationException("Recorded complete discovery prevented rebuilding the current executable catalog.");
        string[] identity = active.EventId.Split(':');
        Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(int.Parse(identity[1]), int.Parse(identity[2])).Scripts.Single(script => script.Script == int.Parse(identity[4]));
        var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == active.VisitId).Events.Single(item => item.Event.Id == active.EventId);
        if (!target.Complete || target.Analysis.MayEnterField || !target.Analysis.MayInteract || coverage.Event.Kind != AutoPlayEventKind.Interaction)
        {
            throw new InvalidOperationException("A standalone native world interaction was not selected after field-entry exhaustion.");
        }
        if (before != JsonSerializer.Serialize(session.Capture() with { AutoPlay = null }) || !bytes.SequenceEqual(File.ReadAllBytes(path)))
        {
            throw new InvalidOperationException("Selecting a native world interaction changed VM, progress, physics or source bytes.");
        }
        Console.WriteLine($"PASS original standalone world interaction {active.EventId}, retained complete-discovery compatibility and exact native/source preservation");
    }
}
