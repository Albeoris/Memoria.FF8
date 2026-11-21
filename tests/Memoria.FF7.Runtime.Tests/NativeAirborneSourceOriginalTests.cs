using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeAirborneSourceOriginalTests
{
    internal static void Run()
    {
        Ff7Installation installation = Ff7Installation.Discover(Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR")!);
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_AIRBORNE_SOURCE_SAVE") ?? throw new InvalidOperationException("Select an airborne source fixture.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, writable: false);
        using Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(input).Ff7!);
        Ff7WorldRuntime world = session.World!;
        Ff7AutoPlayActiveEvent original = session.AutoPlayState!.Active!;
        string[] parts = original.EventId.Split(':');
        Ff7AutoPlayWorldScriptObservation target = world.ReadAutoPlayFrontierCatalog(int.Parse(parts[1]), int.Parse(parts[2])).Scripts.Single(item => item.Id == original.EventId);
        if (world.ReadAutoPlayCurrentModeTerrainEligibility(target) != false)
        {
            throw new InvalidOperationException("The airborne fixture still admits an impossible native terrain callback.");
        }
        string nativeBefore = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        string History(Ff7AutoPlayState state)
        {
            var item = state.Coverage.Visits.Single(visit => visit.Visit.Key == original.VisitId).Events.Single(item => item.Event.Id == original.EventId);
            return JsonSerializer.Serialize(new { item.Completed, item.Conditions?.ConsumedEntries, item.Conditions?.ConsumedExits, item.Routes, item.RetiredRoutes });
        }
        string historyBefore = History(session.AutoPlayState);
        using Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice during source admission."));
        Ff7AutoplayDriver driver = new(session);
        Stopwatch timer = Stopwatch.StartNew();
        for (int poll = 0; poll < 32 && timer.Elapsed.TotalSeconds < 30; poll++)
        {
            policy.ChooseInput(driver);
            if (policy.State.Active is not null)
            {
                break;
            }
        }
        Console.WriteLine($"Native airborne source: tick={world.Process.Tick}, seconds={timer.Elapsed.TotalSeconds:F3}, old={original.EventId}, new={policy.State.Active?.EventId}, kind={policy.State.Active?.Kind}, physical={driver.PhysicalPlanningWork}, status={policy.Status}");
        if (policy.State.Active?.EventId == original.EventId || nativeBefore != Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) || historyBefore != History(policy.State) || !SHA256.HashData(bytes).AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new InvalidOperationException("Rejecting an impossible airborne source altered native/history state or retained the invalid target.");
        }
    }
}
