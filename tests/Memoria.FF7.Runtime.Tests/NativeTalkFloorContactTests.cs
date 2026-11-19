using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;

internal static class NativeTalkFloorContactTests
{
    internal static void Run()
    {
        TalkRegionGeometryTests.Run();
        PcWalkmeshTriangle[] mesh =
        [
            new(0, Vector3.Zero, new(0, 1000, 0), new(1000, 0, 0), -1, 1, -1),
            new(1, new(0, 1000, 0), new(1000, 1000, 0), new(1000, 0, 0), -1, -1, 0)
        ];
        short[] vectors = new short[512];
        byte[] angles = new byte[129];
        for (int direction = 0; direction < 256; direction++)
        {
            vectors[direction * 2] = (short)Math.Round(Math.Sin(direction * Math.PI / 128) * 4096);
            vectors[direction * 2 + 1] = (short)Math.Round(Math.Cos(direction * Math.PI / 128) * 4096);
        }
        for (int index = 0; index < angles.Length; index++)
        {
            angles[index] = (byte)Math.Round(Math.Asin(index / 128.0) * 128 / Math.PI);
        }
        Ff7NativeMovement movement = new(mesh, vectors, angles);
        Ff7Actor interior = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, Triangle = 1, X4096 = 500 * 4096, Y4096 = 500 * 4096, CollisionRadius = 46 };
        string source = JsonSerializer.Serialize(new { interior, mesh });
        Ff7Actor? control = Ff7AutoplayDriver.ReadNativeTalkFloorContact(interior, movement, [interior], [], false, [64], actual => actual.Direction == 64);
        Check(control is not null && control.X4096 == interior.X4096 && control.Y4096 == interior.Y4096, "Zero stride rejected valid interior footprint or invented displacement.");
        Ff7Actor vertex = interior with { X4096 = 1000 * 4096, Y4096 = 0 };
        Check(Ff7AutoplayDriver.ReadNativeTalkFloorContact(vertex, movement, [vertex], [], false, [64], _ => true) is null, "A wall vertex passed only because its geometric talk predicate was true.");
        Ff7Actor projected = interior with { Direction = 75 };
        byte[] legal = [0, 32, 64, 96, 128, 160, 192, 224];
        bool TalkReady(Ff7Actor actual) => Math.Abs(((actual.Direction - 75 + 128) & 255) - 128) < 64;
        Ff7NativeMoveResult Slide(byte heading) => new(projected with { Direction = heading == 75 || heading == 64 ? 139 : heading }, false, true, []);
        Check(Ff7AutoplayDriver.SelectNativeTalkFloorContact([75], Slide, TalkReady) is null, "Talk admission tested original facing instead of the corrected native result.");
        Ff7Actor? supported = Ff7AutoplayDriver.SelectNativeTalkFloorContact([64, 128], Slide, TalkReady);
        Check(supported is { Direction: 128 } && TalkReady(supported), "Finite legal native headings missed an admissible contact after the closest heading slid away.");
        Check(Ff7AutoplayDriver.ReadNativeTalkFloorContact(projected, movement, [projected], [], false, [128], actual => actual.Direction == projected.Direction) is null, "The real movement kernel's final direction was ignored.");
        Ff7Actor blocker = interior with { Entity = 1, Slot = 1, CollisionEnabled = true, CollisionRadius = 300 };
        Check(Ff7AutoplayDriver.ReadNativeTalkFloorContact(interior, movement, [interior, blocker], [], false, legal, _ => true) is null, "An independent native occupancy refusal was ignored.");
        Ff7Actor cold = JsonSerializer.Deserialize<Ff7Actor>(JsonSerializer.Serialize(interior))!;
        Check(Ff7AutoplayDriver.ReadNativeTalkFloorContact(cold, movement, [cold], [], false, [64], actual => actual.Direction == 64) == control, "Cold reconstruction changed native contact admission.");
        Check(source == JsonSerializer.Serialize(new { interior, mesh }), "Contact discovery mutated native source geometry.");
        Console.WriteLine("PASS native interior/wall/actual-facing finite-input contact admission, occupancy and cold reconstruction");
    }

    internal static void RunOriginal()
    {
        string root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR") ?? throw new InvalidOperationException("Original resources required.");
        string path = Environment.GetEnvironmentVariable("MEMORIA_FF7_ACTOR_FRONTIER_SAVE") ?? throw new InvalidOperationException("External actor frontier required.");
        byte[] bytes = File.ReadAllBytes(path);
        Ff7Installation installation = Ff7Installation.Discover(root);
        RunController(installation, bytes, false);
        RunController(installation, bytes, true);
        Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "The authoritative source changed.");
    }

    private static void RunController(Ff7Installation installation, byte[] bytes, bool cold)
    {
        using MemoryStream stream = new(bytes, false);
        Ff7Session session = Ff7Session.Restore(installation, CombinedSessionCodec.Read(stream).Ff7!);
        Ff7AutoPlayPolicy policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
        Ff7AutoPlayActiveEvent owner = policy.State.Active!;
        policy.State.FrontierProgressKey = null;
        policy.State.FrontierIdleSteps = 0;
        Ff7AutoPlayObservation initial = new Ff7AutoPlayDiscovery(session).Observe();
        Ff7AutoplayDriver probe = new(session);
        probe.ConfigureSpatialBoundaries(initial.Events, initial.Events.Single(target => target.Id == owner.EventId));
        string native = Ff7AutoplayDriver.ReadNativeRouteFingerprint(session);
        string history = JsonSerializer.Serialize(policy.State);
        int contacts = probe.DiscoverTalkContacts(session.Host.GetActor(owner.Entity), session.Host.Process.Actors.Single(actor => actor.PlayerControlled)).Contacts.Count;
        Check(native == Ff7AutoplayDriver.ReadNativeRouteFingerprint(session) && history == JsonSerializer.Serialize(policy.State), "Native contact projection mutated its source/history.");
        Check(contacts == 0, "The external unsupported-contact fixture has an admitted native contact and needs a different regression expectation.");
        HashSet<string> oldGuards = policy.State.NavigationGuards.Keys.ToHashSet(StringComparer.Ordinal);
        long start = session.Host.Process.Tick;
        bool restored = false;
        bool completed = false;
        Ff7AutoPlayController controller = CreateController();
        Ff7AutoPlayController CreateController() => new(session, policy.ChooseInput, chooseDialogueSelection: policy.ChooseDialogueSelection, readPlanningProgress: () => policy.WorkProgress);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        int scheduling = 0;
        try
        {
            controller.Start();
            while (controller.Running && session.Battle is null && !completed && elapsed.Elapsed < TimeSpan.FromSeconds(30) && scheduling++ < 2048)
            {
                controller.Advance(1);
                var coverage = policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == owner.VisitId).Events.Single(item => item.Event.Id == owner.EventId);
                completed = coverage.Completed && coverage.Conditions?.HasExecutedEntry(owner.ConditionFingerprint) == true && coverage.Conditions.ActiveEntry is null;
                if (cold && !restored && session.Host.Process.Tick - start >= 132)
                {
                    Ff7SessionSave saved = session.Capture();
                    saved.Validate();
                    policy.Dispose();
                    session.Dispose();
                    session = Ff7Session.Restore(installation, saved);
                    policy = new(session, (_, _) => throw new InvalidOperationException("Unexpected choice checkpoint."));
                    controller = CreateController();
                    controller.Start();
                    restored = true;
                }
            }
            Console.WriteLine($"TALK-CONTROLLER cold:{cold}, restored:{restored}, native:{session.Host.Process.Tick - start}, scheduling:{scheduling}, completed:{completed}, battle:{session.Battle is not null}, active:{policy.State.Active?.EventId}, accepted:{policy.State.Active?.Accepted}, goal:{policy.State.NavigationGoal}, error:{controller.Error}");
            bool refusedSource = policy.State.NavigationGuards.Any(pair => !oldGuards.Contains(pair.Key) && pair.Value.EventId == owner.EventId && pair.Value.NavigationKey == owner.NavigationKey && pair.Value.MovementFailure is null && policy.State.Coverage.Visits.Single(visit => visit.Visit.Key == owner.VisitId).Events.Single(item => item.Event.Id == owner.EventId).UnavailableAttempts.Any(attempt => attempt.SourceConditionSignature == owner.ConditionFingerprint && attempt.GuardKey == pair.Key));
            Check(controller.Error is null && !completed && refusedSource && session.Battle is not null && policy.State.Active is { } actualOwner && actualOwner.EventId != owner.EventId && policy.State.NavigationGoal is { EventId: not null } && (!cold || restored), "The source-native refusal did not preserve a real shared route through its next genuine boundary.");
        }
        finally
        {
            policy.Dispose();
            session.Dispose();
        }
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
