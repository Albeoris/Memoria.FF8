using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoplayDriver
{
    private ActorContactCatalog? actorContactCatalog;

    private void CancelActorContactCatalog()
    {
        actorContactCatalog?.Dispose();
        actorContactCatalog = null;
    }

    internal long ActorContactPlanningWork { get; private set; }

    internal sealed class ActorContactCatalog(string context, IEnumerator<(Vector3 Point, int Triangle)> source) : IDisposable
    {
        internal string Context { get; } = context;
        internal List<(Vector3 Point, int Triangle)> Contacts { get; } = [];
        internal bool Completed { get; private set; }
        internal (Vector3 Point, int Triangle)? SelectedContact { get; set; }
        private IEnumerator<(Vector3 Point, int Triangle)>? remaining = source;

        internal int Resume(Ff7NativePlanningQuantum quantum)
        {
            int work = 0;
            while (!Completed && quantum.TryBeginOperation())
            {
                work++;
                if (!remaining!.MoveNext())
                {
                    Completed = true;
                    Dispose();
                }
                else if (remaining.Current.Triangle >= 0 && !Contacts.Contains(remaining.Current))
                {
                    Contacts.Add(remaining.Current);
                }
            }
            return work;
        }

        public void Dispose()
        {
            remaining?.Dispose();
            remaining = null;
        }
    }

    private Ff7AutoplayInput? ReadActorPushInput(Ff7Actor player, Ff7Actor target, bool run, bool requireAdmission = true)
    {
        int desired = session.Host.Movement!.Direction((target.X4096 - player.X4096) >> 12, (target.Y4096 - player.Y4096) >> 12);
        Ff7Actor controlled = player with { MoveSpeed = session.GetPlayerMovementSpeed(run) };
        return SelectActorPushInput(target.Entity, run, desired, session.Field.Triggers.ControlDirection,
            direction => session.Host.Movement.StepDirection(controlled, direction, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, playerLocked: false, lineBlocked: session.Host.Process.LineBlocked),
            preview => !requireAdmission || AllowsPlannedMove(controlled, preview));
    }

    internal static Ff7AutoplayInput? SelectActorPushInput(int entity, bool run, int desired, int controlDirection, Func<byte, Ff7NativeMoveResult> preview, Func<Ff7NativeMoveResult, bool> allows)
    {
        foreach (var direction in Directions.OrderBy(direction => Math.Abs(((direction.Direction + controlDirection - desired + 128) & 255) - 128)))
        {
            Ff7NativeMoveResult step = preview((byte)((direction.Direction + controlDirection) & 255));
            if (step.PushEntities.Contains(entity) && allows(step))
            {
                return new(direction.X, direction.Y, Run: run);
            }
        }
        return null;
    }

    private IEnumerable<(Vector3 Point, int Triangle)> EnumerateActorContactRegion(Ff7Actor target, Ff7Actor player, IReadOnlySet<int> reachable)
    {
        byte[] headings = Directions.Select(direction => (byte)((direction.Direction + session.Field.Triggers.ControlDirection) & 255)).ToArray();
        Vector2[] footprint = headings.SelectMany(heading => session.Host.Movement!.ReadFootprintOffsets(heading, player.CollisionRadius)).ToArray();
        int radius = (player.CollisionRadius + target.CollisionRadius) / 2;
        int maximumSpeed = Math.Max(session.GetPlayerMovementSpeed(true), session.GetPlayerMovementSpeed(false));
        int range = checked((int)Math.Ceiling(player.CollisionRadius * Math.Sqrt(2) + radius + maximumSpeed / 256d * Math.Sqrt(2)) + 2);
        Vector3 targetPoint = new(target.X4096 >> 12, target.Y4096 >> 12, target.Z4096 >> 12);
        foreach (int triangle in reachable.Order())
        {
            foreach (Vector3 point in ReadTalkRegionPoints(session.Field.Walkmesh, reachable, triangle, targetPoint, range, player.CollisionRadius, footprint))
            {
                Ff7Actor projected = player with { X4096 = (int)(point.X * 4096), Y4096 = (int)(point.Y * 4096), Z4096 = (int)(point.Z * 4096), Triangle = triangle };
                Ff7Actor? floor = ReadNativeTalkFloorContact(projected, session.Host.Movement!, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, session.Host.Process.LineBlocked, headings, _ => true);
                if (floor is not null && (ReadActorPushInput(floor, target, true, false) is not null || ReadActorPushInput(floor, target, false, false) is not null))
                {
                    yield return (Position3D(floor), floor.Triangle);
                }
                else
                {
                    yield return (default, -1);
                }
            }
        }
    }

    internal static string ReadActorContactCatalogContext(long generation, string geometry, IEnumerable<int> reachable, bool lineBlocked, int controlDirection, int runSpeed, int walkSpeed)
    {
        string facts = $"{generation}:{geometry}:{string.Join(',', reachable.Order())}:{lineBlocked}:{controlDirection}:{runSpeed}:{walkSpeed}";
        return "native-actor-push:1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts)));
    }

    private Ff7AutoplayInput ChooseActorContactRegion(Ff7Actor target, Ff7Actor player, bool run)
    {
        if (ReadActorPushInput(player, target, run) is Ff7AutoplayInput contact)
        {
            navigationActionProgress = true;
            return contact;
        }
        HashSet<int> reachable = ReachableTriangles(player.Triangle);
        string context = ReadActorContactCatalogContext(session.FieldGeneration, ReadPhysicalAvailabilityFingerprint(target.Entity, session.Host.Process.Actors.Where(actor => actor.Slot != player.Slot).Select(actor => actor.Entity).ToArray()), reachable, session.Host.Process.LineBlocked, session.Field.Triggers.ControlDirection, session.GetPlayerMovementSpeed(true), session.GetPlayerMovementSpeed(false));
        if (actorContactCatalog?.Context != context)
        {
            CancelActorContactCatalog();
            actorContactCatalog = new(context, EnumerateActorContactRegion(target, player, reachable).GetEnumerator());
        }
        ActorContactPlanningWork += actorContactCatalog.Resume(navigationModeQuantum ?? new());
        if (!actorContactCatalog.Completed)
        {
            DeferPlanning();
            return default;
        }
        // Witnesses guide the route; only an actual native push can complete contact.
        // An empty constructive set is unknown and still uses the represented physical goal.
        var contacts = actorContactCatalog.Contacts.ToList();
        bool noWitness = contacts.Count == 0;
        if (noWitness)
        {
            foreach (int triangle in reachable)
            {
                var face = session.Field.Walkmesh[triangle];
                contacts.Add(((face.A + face.B + face.C) / 3, triangle));
            }
        }
        var selected = actorContactCatalog.SelectedContact is { } retained && contacts.Contains(retained)
            ? retained
            : ChooseFloorContact(contacts, player);
        actorContactCatalog.SelectedContact = selected;
        Ff7Actor controlled = player with { MoveSpeed = session.GetPlayerMovementSpeed(run) };
        Ff7AutoplayInput? direct = PreviewGoalInput(controlled, selected.Point, session.Field.Triggers.ControlDirection, session.Host.Movement!, session.Host.Process.Actors, session.Host.Process.BlockedTriangles, session.Host.Process.LineBlocked, AllowsPlannedMove, actor => ReadActorPushInput(actor, target, run) is not null, run);
        if (direct is Ff7AutoplayInput proved)
        {
            navigationActionProgress = true;
            return proved;
        }
        string? previousContext = navigationTalkContactContext;
        Func<Ff7Actor, bool>? previousGoal = navigationTalkContactGoal;
        bool previousRequiresSearch = navigationTalkContactRequiresSearch;
        navigationTalkContactContext = context;
        navigationTalkContactGoal = actor => ReadActorPushInput(actor, target, run) is not null;
        navigationTalkContactRequiresSearch = noWitness || player.Triangle == selected.Triangle;
        try
        {
            return Approach(selected.Point, selected.Triangle, run);
        }
        finally
        {
            navigationTalkContactContext = previousContext;
            navigationTalkContactGoal = previousGoal;
            navigationTalkContactRequiresSearch = previousRequiresSearch;
        }
    }
}
