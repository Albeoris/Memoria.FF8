using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class ActorContactRegionTests
{
    internal static void Run()
    {
        Ff7Actor player = new() { Entity = 1, Slot = 0, Model = 0, PlayerControlled = true, Direction = 128 };
        Ff7NativeMoveResult push = new(player, false, false, [6]);
        var input = Ff7AutoplayDriver.SelectActorPushInput(6, false, 128, 0, _ => push, _ => true);
        Check(input is { Talk: false, Run: false } && (input.Value.Horizontal != 0 || input.Value.Vertical != 0), "A legal zero-motion collision failed to emit its actual movement input, or became Talk.");
        Check(Ff7AutoplayDriver.SelectActorPushInput(6, true, 128, 0, _ => push, _ => false) is null, "Native collision bypassed a protected admission guard.");
        Check(Ff7AutoplayDriver.SelectActorPushInput(7, true, 128, 0, _ => push, _ => true) is null, "An unrelated actor collision became selected contact.");
        int probes = 0;
        input = Ff7AutoplayDriver.SelectActorPushInput(6, true, 128, 32, heading =>
        {
            probes++;
            return heading == 224 ? push : new(player, true, false, []);
        }, _ => true);
        Check(input is { Run: true, Talk: false } && probes > 1, "Contact discovery used only the actor-facing axis instead of all native control axes.");
        string Context(IEnumerable<int> labels, string geometry = "stable") => Ff7AutoplayDriver.ReadActorContactCatalogContext(1, geometry, labels, false, 0, 4096, 1024);
        Check(Context([0, 1]) != Context([0, 2]) && Context([0, 1]) == Context([1, 0]) && Context([0, 1]) != Context([0, 1], "changed"), "Directed reachable sets sharing an anchor, or changed native geometry, reused a stale contact catalog.");
        bool disposed = false;
        IEnumerable<(Vector3 Point, int Triangle)> Candidates()
        {
            try
            {
                yield return (default, -1);
                yield return (new(2, 3, 4), 5);
                yield return (new(6, 7, 8), 9);
            }
            finally
            {
                disposed = true;
            }
        }
        using var catalog = new Ff7AutoplayDriver.ActorContactCatalog("source", Candidates().GetEnumerator());
        Check(catalog.Resume(new(1)) == 1 && !catalog.Completed && catalog.Contacts.Count == 0, "A cooperative negative candidate became completion or fake contact.");
        Check(catalog.Resume(new(1)) == 1 && !catalog.Completed && catalog.Contacts.Count == 1, "Catalog continuation restarted or lost its first real witness.");
        Check(catalog.Resume(new(8)) == 2 && catalog.Completed && catalog.Contacts.Count == 2 && disposed, "Finite contact work did not finish/dispose exactly once.");
        Console.WriteLine("PASS native ActorContact zero-motion Push, no Talk requirement, protected guard, exact owner and finite axes");
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
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTOR_CONTACT_SAVE") ?? throw new InvalidOperationException("Select a native actor-contact fixture.");
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Select original resources.");
        byte[] bytes = File.ReadAllBytes(path);
        using MemoryStream input = new(bytes, false);
        using Ff7Session session = Ff7Session.Restore(Ff7Installation.Discover(game), CombinedSessionCodec.Read(input).Ff7!);
        int entity = session.AutoPlayState!.Active!.Entity;
        long generation = session.FieldGeneration;
        bool admitted = false;
        bool returned = false;
        session.NativeInteractionScriptRequested += receipt => admitted |= session.FieldGeneration == generation && receipt.Entity == entity && receipt.Script == 2 && receipt.Accepted && receipt.Started;
        session.Machine.ScriptReturned += receipt => returned |= admitted && session.FieldGeneration == generation && receipt.Entity == entity && receipt.Priority == 1;
        Ff7AutoPlayObservation observed = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoPlayTarget selected = observed.Events.Single(target => target.Kind == Ff7AutoPlayTargetKind.ActorContact && target.Entity == entity);
        Ff7AutoPlayController controller = new(session, driver =>
        {
            driver.ConfigureSpatialBoundaries(observed.Events, selected);
            return driver.ChooseTargetInput(selected, observed.Events, run => driver.Contact(entity, run));
        }, advanceTick: () => session.AdvanceTick(advanceClock: false));
        controller.Start();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        TimeSpan previous = TimeSpan.Zero;
        int polls = 0;
        long memoryBefore = GC.GetTotalMemory(false);
        while (controller.Running && !returned && session.FieldGeneration == generation && elapsed.Elapsed < TimeSpan.FromSeconds(15) && GC.GetTotalMemory(false) < memoryBefore + 1024L * 1024 * 1024)
        {
            TimeSpan now = elapsed.Elapsed;
            session.AdvanceClock(now - previous);
            previous = now;
            controller.Advance(1);
            polls++;
        }
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Console.WriteLine($"CONTACT original: admitted={admitted}, returnedP1={returned}, callbacks={controller.Steps}, polls={polls}, tick={session.Machine.Tick}, triangle={player.Triangle}, planning={controller.PlanningProgress}, managedBytes={GC.GetTotalMemory(false)}, error={controller.Error}, elapsed={elapsed.Elapsed.TotalSeconds:F3}s; source unchanged={bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path))}");
        Check(controller.Error is null && admitted && returned && bytes.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "Original native ActorContact has not admitted and returned under ordinary driver input.");
    }
}
