using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeBlockedSourceNavigationTests
{
    public static void Run()
    {
        CheckSourceLeaf();
        CheckAvailabilityRevision();
        CheckOriginal();
    }

    private static void CheckSourceLeaf()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 1, -1, -1),
            new(1, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 0, 2, -1),
            new(2, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 1, -1, -1)
        ];
        foreach (bool native in new[] { true, false })
        {
            byte[] blocked = [native ? (byte)2 : (byte)0];
            HashSet<int> spatial = native ? [] : [1];
            var escape = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 1, 0, spatial);
            Check(escape.Previous[1] == 0 && escape.Distance[1] == 1, "The occupied restricted source could not leave for an ordinary neighbor.");
            Check(escape.Previous[2] == -1 && escape.Distance[2] == int.MaxValue, "A restricted source bridged an otherwise inaccessible component.");
            var reentry = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 0, 2, spatial);
            Check(reentry.Previous[0] == -1 && reentry.Previous[1] == -1, "After leaving, a restricted triangle remained admitted for reentry.");
            var sameSource = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 1, 1, spatial);
            Check(sameSource.Previous[1] == 1 && sameSource.Distance[1] == 0 && sameSource.Previous[0] == -1 && sameSource.Previous[2] == -1, "A same-source contact either lost its origin or expanded a restricted destination.");
            var forbiddenDestination = Ff7AutoplayDriver.ReadApproachRoutes(mesh, blocked, 0, 1, spatial);
            Check(forbiddenDestination.Previous.All(next => next == -1), "A restricted non-source destination was admitted.");
            Check(blocked[0] == (native ? 2 : 0) && spatial.SetEquals(native ? Array.Empty<int>() : new[] { 1 }), "Route guidance changed native or spatial restrictions.");
        }
        var admitted = Ff7AutoplayDriver.ReadApproachRoutes(mesh, [0], 0, 2, new HashSet<int> { 1 }, 1);
        Check(admitted.Previous[0] == 1 && admitted.Distance[0] == 2, "An explicitly admitted spatial region could not carry the planned route.");
        var stillBlocked = Ff7AutoplayDriver.ReadApproachRoutes(mesh, [2], 0, 2, new HashSet<int> { 1 }, 1);
        Check(stillBlocked.Previous[0] == -1, "Spatial admission overrode the native blocked-triangle owner.");
    }

    private static void CheckAvailabilityRevision()
    {
        string legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("native-facts|automatic-triangle-boundaries:1:2,7")));
        string current = Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("native-facts", new HashSet<int> { 7, 2 });
        Check(current != legacy, "The corrected route admission retained obsolete physical absence receipts.");
        Check(current == Ff7AutoplayDriver.ReadSpatialBoundaryFingerprint("native-facts", new HashSet<int> { 2, 7 }), "Physical absence changed with boundary enumeration order.");
    }

    private static void CheckOriginal()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF7_BLOCKED_SOURCE_SAVE");
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(path))
        {
            Console.WriteLine("SKIP blocked-source original navigation: configure resources and an external standalone field fixture.");
            return;
        }
        byte[] source = File.ReadAllBytes(path);
        using MemoryStream stream = new(source, writable: false);
        CombinedSessionSave envelope = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root);
        using Ff7Session session = Ff7Session.Restore(installation, envelope.Ff7!);
        string before = JsonSerializer.Serialize(session.Capture());
        Ff7AutoPlayObservation observation = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7Actor player = session.Host.Process.Actors.Single(actor => actor.PlayerControlled);
        Check((session.Host.Process.BlockedTriangles[player.Triangle >> 3] & (1 << (player.Triangle & 7))) != 0, "The supplied regression fixture does not occupy a blocked native source.");
        Ff7AutoPlayTarget target = observation.Events.First(item => item.Kind == Ff7AutoPlayTargetKind.LineTouch && item.EntryInputContract is not null && item.MayChangeNavigation);
        Ff7Line line = session.Host.Process.Lines.Last(item => item.Entity == target.Entity);
        Vector3 position = new(player.X4096 >> 12, player.Y4096 >> 12, player.Z4096 >> 12);
        var goal = Ff7AutoplayDriver.DiscoverLineContacts(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player, line).OrderBy(contact => Vector3.DistanceSquared(position, contact.Point)).ThenBy(contact => contact.Triangle).First();
        HashSet<int> boundaries = Ff7AutoplayDriver.ReadSpatialBoundaryTriangles(observation.Events);
        var routes = Ff7AutoplayDriver.ReadApproachRoutes(session.Field.Walkmesh, session.Host.Process.BlockedTriangles, player.Triangle, goal.Triangle, boundaries);
        Check(routes.Previous[player.Triangle] >= 0, "An original native contact still loses its route out of an occupied blocked source.");
        Ff7AutoplayDriver driver = new(session);
        driver.ConfigureSpatialBoundaries(observation.Events, target);
        driver.AvoidLinePush(observation.Events.Where(item => item.ChangesField && item.Entity != target.Entity && item.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave).Select(item => item.Entity).Distinct().ToArray());
        Ff7AutoplayInput input = driver.ChooseTargetInput(target, () => driver.ApproachLine(target.Entity, target.Kind));
        Check(input.Horizontal != 0 || input.Vertical != 0, "The original blocked-source contact produced no physical departure proposal.");
        CheckNativeProposal(session, player, input, boundaries);
        Check(before == JsonSerializer.Serialize(session.Capture()), "Read-only navigation changed script PCs, progress, RNG or saved planner metadata.");
        using MemoryStream serialized = new();
        CombinedSessionCodec.Write(serialized, envelope with { Ff7 = session.Capture() });
        serialized.Position = 0;
        using Ff7Session cold = Ff7Session.Restore(installation, CombinedSessionCodec.Read(serialized).Ff7!);
        Check(JsonSerializer.Serialize(observation) == JsonSerializer.Serialize(new Ff7AutoPlayDiscovery(cold).Observe()), "Cold restoration changed blocked-source contact metadata.");
        Ff7AutoplayDriver coldDriver = new(cold);
        coldDriver.ConfigureSpatialBoundaries(observation.Events, target);
        coldDriver.AvoidLinePush(observation.Events.Where(item => item.ChangesField && item.Entity != target.Entity && item.Kind is >= Ff7AutoPlayTargetKind.LineTalk and <= Ff7AutoPlayTargetKind.LineLeave).Select(item => item.Entity).Distinct().ToArray());
        Check(input == coldDriver.ChooseTargetInput(target, () => coldDriver.ApproachLine(target.Entity, target.Kind)), "Cold restoration changed the native departure proposal.");
        using Ff7AutoPlayPolicy policy = new(cold, (_, _) => throw new InvalidOperationException("A navigation-only observation requested a choice checkpoint."));
        string nativeBefore = NativeFacts(cold);
        string completedBefore = JsonSerializer.Serialize(policy.State.Coverage.Visits.SelectMany(visit => visit.Events.Where(item => item.Completed).Select(item => new { Visit = visit.Visit.Key, Event = item.Event.Id, item.Completed })));
        Ff7AutoplayInput planned = policy.ChooseInput(coldDriver);
        Check(policy.State.Active is not null && (planned.Horizontal != 0 || planned.Vertical != 0), "The saved negative deferrals still hide every pending native contact after the admission revision.");
        Check(nativeBefore == NativeFacts(cold), "Reobserving obsolete physical absence changed native owners before any input was applied.");
        Check(completedBefore == JsonSerializer.Serialize(policy.State.Coverage.Visits.SelectMany(visit => visit.Events.Where(item => item.Completed).Select(item => new { Visit = visit.Visit.Key, Event = item.Event.Id, item.Completed }))), "Refreshing negative physical receipts discarded completed event coverage.");
        Check(source.AsSpan().SequenceEqual(File.ReadAllBytes(path)), "The original regression source changed during read-only navigation.");
    }

    private static string NativeFacts(Ff7Session session)
    {
        return JsonSerializer.Serialize(session.Capture() with { AutoPlay = null });
    }

    private static void CheckNativeProposal(Ff7Session session, Ff7Actor player, Ff7AutoplayInput input, IReadOnlySet<int> boundaries)
    {
        Ff7Actor controlled = player with { MoveSpeed = unchecked((ushort)session.GetPlayerMovementSpeed(input.Run)) };
        int direction = input.Vertical < 0 ? input.Horizontal < 0 ? 32 : input.Horizontal > 0 ? 224 : 0 : input.Vertical > 0 ? input.Horizontal < 0 ? 96 : input.Horizontal > 0 ? 160 : 128 : input.Horizontal < 0 ? 64 : 192;
        int rotation = (input.Held & 8) != 0 ? 224 : (input.Held & 4) != 0 ? 32 : 0;
        Ff7NativeMoveResult preview = session.Host.Movement!.StepDirection(controlled, unchecked((byte)(direction + session.Field.Triggers.ControlDirection + rotation)), session.Host.Process.Actors, session.Host.Process.BlockedTriangles, false, session.Host.Process.LineBlocked);
        Check(preview.Moved && Ff7AutoplayDriver.AllowsSpatialMove(controlled, preview.Actor, boundaries, null), "The departure proposal bypassed native movement or an unselected spatial boundary.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
