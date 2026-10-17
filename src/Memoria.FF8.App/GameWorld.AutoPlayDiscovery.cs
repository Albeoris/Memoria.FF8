using System.Numerics;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal enum AutoPlayTargetKind
{
    Talk,
    Contact,
    Gateway
}

internal sealed record AutoPlayTarget(string Id, AutoPlayTargetKind Kind, float X, float Y, float Z, int Triangle, int ObjectId = -1, int? DestinationField = null, bool ChangesStory = false, int Order = 0, float? ApproachX = null, float? ApproachY = null, float? ApproachZ = null, int? ApproachTriangle = null, int Direction = 0, bool RequiresConfirm = false)
{
    public string CoverageId => string.Join(':', Id.Split(':').Take(3));
}

internal sealed partial class GameWorld
{
    public string AutoPlayRegion => Field?.Name is string name && name.Length >= 2 ? name[..2] : "field";

    public IReadOnlyList<AutoPlayTarget> DiscoverAutoPlayTargets()
    {
        if (Field is null || !HasCurrentFieldAllocation || !Events.CanAcceptMovementInput || Events.PlayerActor is not ActorState player || player.NativeScriptMovement is { Active: true, Suspended: false } || player.NativeJump is { Active: true } || player.NativeLadder is { Active: true } || player.NativeLadderMode3 is { Active: true })
        {
            return [];
        }
        return DiscoverAutoPlayTargets(Field, Events, player);
    }

    internal static IReadOnlyList<AutoPlayTarget> DiscoverAutoPlayTargets(FieldAssets field, EventEngine events, ActorState player)
    {
        JsmProgram program = JsmProgram.Read(field.Jsm);
        HashSet<int> reachable = AutoPlayReachableTriangles(field.Walkmesh, player.WalkmeshTriangle, events.Scene.LockedTriangles);
        HashSet<int> excluded = Enumerable.Range(0, field.Walkmesh.Triangles.Count).Where(index => !reachable.Contains(index)).ToHashSet();
        List<AutoPlayTarget> targets = [];
        bool Resolve(float x, float y, float z, out Vector3 position, out int triangle)
        {
            if (field.Walkmesh.TryPosition(x, y, out position, out triangle, excluded) && Math.Abs(position.Z - z) < 256)
            {
                return true;
            }
            return field.Walkmesh.TryPosition(x, y, out position, out triangle) && Math.Abs(position.Z - z) < 256;
        }
        int Order(int variant, int triangle, int? approachTriangle = null) => variant + (reachable.Contains(triangle) && (approachTriangle is null || reachable.Contains(approachTriangle.Value)) ? 0 : 100000);
        foreach (int objectId in events.Scene.NativeActorObjectIds)
        {
            ActorState actor = events.Actors[objectId];
            if (objectId == player.ObjectId || !actor.Active || actor.ModelId is < 0 or ushort.MaxValue)
            {
                continue;
            }
            EventObject obj = program.Objects[objectId];
            if (actor.TalkEnabled && obj.Scripts.Count > 2 && AutoPlayHasBody(program, obj.Scripts[2]))
            {
                AddActorTarget(actor, obj.Scripts[2], AutoPlayTargetKind.Talk, "talk", Math.Max(1, ((ushort)player.PushRadius + (ushort)actor.TalkRadius) * 0.75f));
            }
            if (actor.PushEnabled && obj.Scripts.Count > 3 && AutoPlayHasBody(program, obj.Scripts[3]))
            {
                AddActorTarget(actor, obj.Scripts[3], AutoPlayTargetKind.Contact, "push", Math.Max(1, ((ushort)player.PushRadius + (ushort)actor.PushRadius) * 0.25f));
            }
        }
        foreach (EventObject obj in program.Objects.Where(obj => obj.IsDoor))
        {
            ActorState actor = events.Actors[obj.Index];
            if (actor.Active && actor.LineEnabled && actor.LineStart is Vector3 start && actor.LineEnd is Vector3 end)
            {
                EventScript[] contacts = obj.Scripts.Skip(3).Take(5).Where(script => AutoPlayHasBody(program, script)).ToArray();
                if (contacts.Length > 0)
                {
                    AddSegment($"line:{obj.Index}", AutoPlayTargetKind.Contact, start, end, obj.Index, contacts, null);
                }
                if (obj.Scripts.Count > 2 && AutoPlayHasBody(program, obj.Scripts[2]))
                {
                    AddSegment($"line-action:{obj.Index}", AutoPlayTargetKind.Contact, start, end, obj.Index, [obj.Scripts[2]], null, true);
                }
            }
        }
        EventObject[] areas = program.NativeObjects.Select(index => program.Objects[index]).Where(obj => obj.IsArea).ToArray();
        for (int index = 0; index < events.InfAreaTriggers.Count; index++)
        {
            InfAreaTrigger area = events.InfAreaTriggers[index];
            if ((uint)area.AreaIndex < areas.Length && events.Actors[areas[area.AreaIndex].Index] is { Active: true, DoorLineEnabled: true } actor && areas[area.AreaIndex].Scripts.Count > 2 && AutoPlayHasBody(program, areas[area.AreaIndex].Scripts[2]))
            {
                AddSegment($"area:{index}", AutoPlayTargetKind.Contact, area.Start, area.End, actor.ObjectId, [areas[area.AreaIndex].Scripts[2]], null);
            }
        }
        if (!events.Scene.MapJumpDisabled)
        {
            for (int index = 0; index < events.InfGateways.Count; index++)
            {
                InfGateway gateway = events.InfGateways[index];
                if (gateway.Destination.FieldId != 0x7FFF && (gateway.Destination.FieldId < 72 || gateway.Destination.Triangle is >= 0 and < 0x7FFF))
                {
                    AddSegment($"gateway:{index}", AutoPlayTargetKind.Gateway, gateway.Start, gateway.End, -1, [], gateway.Destination.FieldId);
                }
            }
        }
        return targets.OrderBy(target => target.Order).ThenBy(target => target.Id, StringComparer.Ordinal).ToArray();

        void AddActorTarget(ActorState actor, EventScript script, AutoPlayTargetKind kind, string label, float radius)
        {
            // Multiple approaches allow the ordinary action query to resolve occlusion and competing actors.
            for (int side = 0; side < 8; side++)
            {
                float angle = side * MathF.PI / 4;
                float x = MathF.Round(actor.X + MathF.Sin(angle) * radius);
                float y = MathF.Round(actor.Y - MathF.Cos(angle) * radius);
                if (!Resolve(x, y, actor.Z, out Vector3 position, out int triangle))
                {
                    continue;
                }
                Vector3? approach = null;
                int? approachTriangle = null;
                if (kind == AutoPlayTargetKind.Contact)
                {
                    float distance = (ushort)player.PushRadius + (ushort)actor.PushRadius + 2;
                    if (!Resolve(actor.X + MathF.Sin(angle) * distance, actor.Y - MathF.Cos(angle) * distance, actor.Z, out Vector3 resolved, out int resolvedTriangle))
                    {
                        continue;
                    }
                    approach = resolved;
                    approachTriangle = resolvedTriangle;
                }
                targets.Add(new AutoPlayTarget($"{field.Id}:{label}:{actor.ObjectId}:{side}", kind, position.X, position.Y, position.Z, triangle, actor.ObjectId, AutoPlayScriptDestination(program, script), AutoPlayScriptChangesStory(program, script), Order(side, triangle, approachTriangle), approach?.X, approach?.Y, approach?.Z, approachTriangle, AutoPlayDirection(position, new Vector3(actor.X, actor.Y, actor.Z))));
            }
        }

        void AddSegment(string id, AutoPlayTargetKind kind, Vector3 start, Vector3 end, int objectId, IReadOnlyList<EventScript> scripts, int? destination, bool requiresConfirm = false)
        {
            Vector2 segment = new(end.X - start.X, end.Y - start.Y);
            if (segment.LengthSquared() == 0)
            {
                return;
            }
            Vector2 normal = Vector2.Normalize(new Vector2(-segment.Y, segment.X));
            float distance = Math.Max(1, Math.Min(8, (ushort)player.PushRadius / 2f));
            for (int sample = 0; sample < 3; sample++)
            {
                Vector3 midpoint = Vector3.Lerp(start, end, (sample + 1) / 4f);
                for (int side = 0; side < 2; side++)
                {
                    Vector2 offset = normal * distance * (side == 0 ? 1 : -1);
                    if (!Resolve(MathF.Round(midpoint.X + offset.X), MathF.Round(midpoint.Y + offset.Y), midpoint.Z, out Vector3 position, out int triangle) || !Resolve(MathF.Round(midpoint.X - offset.X), MathF.Round(midpoint.Y - offset.Y), midpoint.Z, out Vector3 approach, out int approachTriangle))
                    {
                        continue;
                    }
                    targets.Add(new AutoPlayTarget($"{field.Id}:{id}:{sample}:{side}", kind, position.X, position.Y, position.Z, triangle, objectId, destination ?? scripts.Select(script => AutoPlayScriptDestination(program, script)).FirstOrDefault(value => value is not null), scripts.Any(script => AutoPlayScriptChangesStory(program, script)), Order(sample * 2 + side, triangle, approachTriangle), approach.X, approach.Y, approach.Z, approachTriangle, AutoPlayDirection(approach, position), requiresConfirm));
                }
            }
        }
    }

    internal static HashSet<int> AutoPlayReachableTriangles(FieldWalkmesh walkmesh, int start, IReadOnlySet<int> locked)
    {
        HashSet<int> reached = [];
        Queue<int> pending = new();
        pending.Enqueue(start);
        while (pending.TryDequeue(out int index))
        {
            if ((uint)index >= walkmesh.Triangles.Count || locked.Contains(index) || !reached.Add(index))
            {
                continue;
            }
            WalkmeshTriangle triangle = walkmesh.Triangles[index];
            pending.Enqueue(triangle.NeighborA);
            pending.Enqueue(triangle.NeighborB);
            pending.Enqueue(triangle.NeighborC);
        }
        return reached;
    }

    private static int AutoPlayDirection(Vector3 from, Vector3 to) => (int)MathF.Round(MathF.Atan2(to.X - from.X, from.Y - to.Y) * 128 / MathF.PI) & 255;

    private static bool AutoPlayHasBody(JsmProgram program, EventScript script) => script.ReachableOperations.Any(index => program.Operations[index].Opcode is not (Jsm.Opcode.LBL or Jsm.Opcode.RET));

    internal static bool AutoPlayScriptChangesStory(JsmProgram program, EventScript script)
    {
        return AutoPlayScriptClosure(program, script).Any(index =>
        {
            JsmOperation operation = program.Operations[index];
            int width = operation.Opcode switch { Jsm.Opcode.POPM_B => 1, Jsm.Opcode.POPM_W => 2, Jsm.Opcode.POPM_L => 4, _ => 0 };
            // The canonical global story progress occupies the two bytes at 256.
            return width > 0 && operation.Parameter < 258 && operation.Parameter + width > 256;
        });
    }

    internal static int? AutoPlayScriptDestination(JsmProgram program, EventScript script)
    {
        HashSet<int> destinations = [];
        foreach (int index in AutoPlayScriptClosure(program, script))
        {
            JsmOperation operation = program.Operations[index];
            int count = operation.Opcode switch { Jsm.Opcode.MAPJUMP => 4, Jsm.Opcode.MAPJUMP3 => 5, Jsm.Opcode.MAPJUMPO => 2, _ => 0 };
            if (count > 0 && index >= count && Enumerable.Range(index - count, count).All(operand => program.Operations[operand].Opcode == Jsm.Opcode.PSHN_L))
            {
                destinations.Add(unchecked((ushort)program.Operations[index - count].Parameter));
            }
        }
        return destinations.Count == 1 ? destinations.Single() : null;
    }

    private static HashSet<int> AutoPlayScriptClosure(JsmProgram program, EventScript script)
    {
        HashSet<int> operations = [];
        Queue<EventScript> pending = new();
        pending.Enqueue(script);
        while (pending.TryDequeue(out EventScript? current))
        {
            foreach (int index in current.ReachableOperations)
            {
                if (!operations.Add(index))
                {
                    continue;
                }
                JsmOperation operation = program.Operations[index];
                if (operation.Opcode is not (Jsm.Opcode.REQ or Jsm.Opcode.REQSW or Jsm.Opcode.REQEW) || (uint)operation.Parameter >= program.NativeObjects.Count)
                {
                    continue;
                }
                EventObject target = program.Objects[program.NativeObjects[operation.Parameter]];
                int? label = index >= 1 && program.Operations[index - 1].Opcode == Jsm.Opcode.PSHN_L ? program.Operations[index - 1].Parameter : null;
                foreach (EventScript requested in target.Scripts.Where(item => label is null || item.Id == unchecked((ushort)label.Value)))
                {
                    pending.Enqueue(requested);
                }
            }
        }
        return operations;
    }
}
