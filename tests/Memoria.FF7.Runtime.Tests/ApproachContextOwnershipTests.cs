using System.Numerics;
using System.Reflection;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

internal static class ApproachContextOwnershipTests
{
    internal static void Run()
    {
        Ff7Actor player = new() { Slot = 0, Entity = 0, Model = 0, PlayerControlled = true, CollisionRadius = 30 };
        Ff7Actor collider = new() { Slot = 1, Entity = 1, Model = 1, CollisionRadius = 10, X4096 = 100 * 4096 };
        Ff7Actor[] actors = [player, collider];
        Ff7Line[] lines = [new(0, 3, 20, 20, 0, 20, 40, 0, 1, 0)];
        Ff7AutoPlayTarget selected = new("line", 3, 3, Ff7AutoPlayTargetKind.LineCross, "native-condition", false, new(4, 4, 0), Triangle: 2);
        int destination = 2;
        int speed = 4096;
        bool run = true;
        byte[] blocked = [0];
        HashSet<int> boundaries = [];
        HashSet<int> gateways = [];
        HashSet<(int From, int To)> owned = [];
        string Context()
        {
            return Ff7AutoplayDriver.ReadApproachAdvisoryContext(1, selected.Position, destination, selected, player, speed, run, false, blocked, boundaries, null, gateways, owned, actors, lines);
        }
        string stable = Context();
        string physical = Ff7AutoplayDriver.ReadPhysicalGeometryContext(stable, actors, lines, false);
        actors = [player with { X4096 = 4096, Triangle = 1, Direction = 64 }, collider with { X4096 = 110 * 4096, Y4096 = 15 * 4096, Triangle = 4 }];
        lines = [lines[0] with { Y1 = 35, Y2 = 55, Enter = true, Touch = true }];
        selected = selected with { Position = new(4, 19, 0), SegmentStart = new(20, 35, 0), SegmentEnd = new(20, 55, 0) };
        Check(Context() == stable, "Incidental actor, selected-contact or LINE motion invalidated stable route intent.");
        string moved = Ff7AutoplayDriver.ReadPhysicalGeometryContext(stable, actors, lines, false);
        Check(moved != physical && Ff7AutoplayDriver.ReadPhysicalGeometryContext(stable, actors, lines, true) != moved, "Physical geometry or native line-block state failed to invalidate pending simulation.");
        Ff7AutoPlayTarget originalTarget = selected;
        selected = selected with { ConditionFingerprint = "changed-native-condition" };
        Check(Context() != stable, "A meaningful original script condition retained stale route intent.");
        selected = originalTarget;
        destination++;
        Check(Context() != stable, "A changed destination triangle retained stale advice.");
        destination--;
        player = player with { CollisionRadius = 31 };
        Check(Context() != stable, "A changed native player radius retained stale route intent.");
        player = player with { CollisionRadius = 30 };
        speed++;
        Check(Context() != stable, "A changed native movement speed retained stale route intent.");
        speed--;
        run = false;
        Check(Context() != stable, "A changed native run mode retained stale route intent.");
        run = true;
        blocked[0] = 2;
        Check(Context() != stable, "A native triangle block retained stale route intent.");
        blocked[0] = 0;
        boundaries.Add(4);
        Check(Context() != stable, "A spatial boundary retained stale route intent.");
        boundaries.Clear();
        gateways.Add(2);
        Check(Context() != stable, "A gateway admission change retained stale route intent.");
        gateways.Clear();
        owned.Add((0, 1));
        Check(Context() != stable, "A native owned-portal topology change retained stale route intent.");
        CheckAtomicLinePhase();
        CheckOwnership(stable, moved);
        CheckStaleAdviceFallback();
        CheckFinalApproachRejoinsCurrentRoute();
        Console.WriteLine("Approach context ownership checks passed.");
    }

    private static void CheckAtomicLinePhase()
    {
        foreach (Ff7AutoPlayTargetKind kind in new[] { Ff7AutoPlayTargetKind.LineEnter, Ff7AutoPlayTargetKind.LineLeave })
        {
            Ff7AutoPlayActiveEvent active = new("visit", "line", "condition", 1, 8, 5, kind, null, 1, "before");
            Ff7AutoPlayState state = new() { Active = active };
            Ff7AutoplayDriver driver = new(null!);
            int publications = 0;
            string intent = Ff7AutoPlayPolicy.ReadNativePlanningIntent(state);
            InvalidOperationException failure = new("native preparation failed");
            try
            {
                Ff7AutoPlayPolicy.PrepareLinePhase(state, driver, active, () => throw failure, () => publications++);
                throw new Exception("The native input preparation failure was swallowed.");
            }
            catch (InvalidOperationException error) when (ReferenceEquals(error, failure))
            {
            }
            Check(state.Active == active && publications == 0 && intent == Ff7AutoPlayPolicy.ReadNativePlanningIntent(state), "A failed native line preparation changed its owner phase, publication or authenticated intent.");
            Ff7AutoPlayPolicy.PrepareLinePhase(state, driver, active, () =>
            {
                driver.DeferPlanning();
                return default;
            }, () => publications++);
            Check(driver.PlanningPending && state.Active == active && publications == 0 && intent == Ff7AutoPlayPolicy.ReadNativePlanningIntent(state), "A cooperative pending native line preparation committed a phase before producing an applicable input.");
            driver.BeginPlanningStep();
            Ff7AutoplayInput ready = new(1, -1, Run: false);
            Check(Ff7AutoPlayPolicy.PrepareLinePhase(state, driver, active, () => ready, () => publications++) == ready && state.Active == (active with { LinePhase = 1 }) && publications == 1, "A ready native line input did not atomically commit exactly its own phase.");
        }
    }

    private static void CheckFinalApproachRejoinsCurrentRoute()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, Vector3.Zero, new(2, 0, 0), new(0, 2, 0), -1, 1, -1),
            new(1, new(2, 0, 0), new(2, 2, 0), new(0, 2, 0), 2, -1, 0),
            new(2, new(2, 0, 0), new(4, 0, 0), new(2, 2, 0), -1, -1, 1)
        ];
        Vector3 target = new(3.5f, 0.25f, 0);
        foreach (int source in new[] { 2, 0, 2, 0 })
        {
            var route = Ff7AutoplayDriver.ReadApproachRoutes(mesh, [0], source, 2);
            var step = Ff7AutoplayDriver.ReadPortalApproachStep(mesh, source, 2, route.Previous, new(0.5f, 0.5f, 0), target, 0);
            Check(step.FinalApproach == (source == 2), "A previous destination visit retained final approach after leaving its native triangle.");
            Check(source != 2 || step.Waypoint == target, "A repeated destination visit lost its actual final contact.");
        }
        var blocked = Ff7AutoplayDriver.ReadApproachRoutes(mesh, [2], 0, 2);
        bool rejected = false;
        try
        {
            Ff7AutoplayDriver.ReadPortalApproachStep(mesh, 0, 2, blocked.Previous, new(0.5f, 0.5f, 0), target, 0);
        }
        catch (Ff7AutoPlayNavigationUnavailableException)
        {
            rejected = true;
        }
        Check(rejected, "A retained final approach bypassed a newly blocked native route.");
    }

    private static void CheckOwnership(string stable, string physical)
    {
        Ff7AutoplayDriver driver = new(null!);
        driver.UpdateApproachGuideContext(stable);
        HashSet<(int From, int To)> advice = (HashSet<(int From, int To)>)Field("approachAdviceExclusions").GetValue(driver)!;
        advice.Add((0, 1));
        Field("committedWaypoint").SetValue(driver, Vector3.UnitX);
        object escape = Field("obstacleEscape").GetValue(driver)!;
        Type step = escape.GetType().GetGenericArguments()[0];
        escape.GetType().GetMethod("Enqueue")!.Invoke(escape, [Activator.CreateInstance(step, new object[] { new Ff7AutoplayInput(1, 0), 0, 0, 0, 0 })]);
        Ff7Actor source = new() { Slot = 0, Entity = 0, Model = 0, MoveSpeed = 1 };
        Ff7AutoplayDriver.PhysicalSearchCursor pending = new(source, Vector3.UnitX, _ => 1, (actor, _) => new(actor, false, false, []), (_, _) => true);
        Field("obstacleSearch").SetValue(driver, pending);
        driver.UpdateApproachGuideContext(stable);
        driver.UpdatePhysicalApproachContext(physical);
        Check(Field("obstacleSearch").GetValue(driver) is null && advice.Count == 1 && Equals(Field("committedWaypoint").GetValue(driver), Vector3.UnitX) && (int)escape.GetType().GetProperty("Count")!.GetValue(escape)! == 1, "A full physical snapshot change cleared chosen advice or a retained escape suffix.");
        driver.UpdateApproachGuideContext(stable + ":new-topology");
        Check(advice.Count == 0 && Field("committedWaypoint").GetValue(driver) is null && (int)escape.GetType().GetProperty("Count")!.GetValue(escape)! == 0, "An actual advisory intent/topology change failed to retire its prefix and hint.");
    }

    private static void CheckStaleAdviceFallback()
    {
        PcWalkmeshTriangle[] mesh =
        [
            new(0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 1, -1, -1),
            new(1, Vector3.UnitX, Vector3.One, Vector3.UnitY, 0, 2, -1),
            new(2, Vector3.One, new(2, 1, 0), Vector3.UnitY, 1, -1, -1)
        ];
        HashSet<(int From, int To)> native = [];
        HashSet<(int From, int To)> advice = [(0, 1)];
        var restored = Ff7AutoplayDriver.ReadAdvisedApproachRoutes(mesh, [0], 0, 2, null, null, native, advice);
        Check(restored.DiscardedAdvice && restored.Previous[0] == 1 && advice.SetEquals([(0, 1)]) && native.Count == 0, "Ephemeral advice alone caused a false native no-route result or mutated native constraints.");
        native.Add((1, 2));
        var forbidden = Ff7AutoplayDriver.ReadAdvisedApproachRoutes(mesh, [0], 0, 2, null, null, native, advice);
        Check(!forbidden.DiscardedAdvice && forbidden.Previous[0] < 0 && native.SetEquals([(1, 2)]), "Advice fallback bypassed a native forbidden portal.");
        var blocked = Ff7AutoplayDriver.ReadAdvisedApproachRoutes(mesh, [2], 0, 2, null, null, new HashSet<(int, int)>(), advice);
        Check(blocked.Previous[0] < 0 && !blocked.DiscardedAdvice, "Advice fallback bypassed an actually blocked native triangle.");
    }

    private static FieldInfo Field(string name)
    {
        return typeof(Ff7AutoplayDriver).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    private static void Check(bool value, string reason)
    {
        if (!value)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
