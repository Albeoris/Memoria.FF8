using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record InfGateway(Vector3 Start, Vector3 End, FieldTransition Destination, int NativeIndex = -1, byte[]? NativeRecord = null);
public sealed record InfAreaTrigger(Vector3 Start, Vector3 End, int AreaIndex, byte Side, int NativeIndex = -1);

public sealed partial class EventEngine
{
    public IReadOnlyList<InfGateway> InfGateways { get; set; } = [];
    public IReadOnlyList<InfAreaTrigger> InfAreaTriggers { get; set; } = [];

    private void DispatchInfMovement(ActorState player, Vector3 previous, Vector3 proposal, int direction)
    {
        if (InfGateways.Count > 12 || InfAreaTriggers.Count > 12)
        {
            throw new InvalidDataException("Native INF has at most twelve gateways and twelve area triggers.");
        }
        Vector3 current = NativeInfPoint(previous);
        Vector3 proposed = NativeInfPoint(new Vector3(proposal.X, proposal.Y, previous.Z));
        int radius = (ushort)player.PushRadius;
        int radiusSquared = radius * radius;
        InfGateway? selectedGateway = null;
        foreach (InfGateway gateway in Scene.MapJumpDisabled ? [] : InfGateways)
        {
            if (gateway.Destination.FieldId == 0x7FFF || gateway.Destination.FieldId >= 72 && gateway.Destination.Triangle == 0xFFFF)
            {
                continue;
            }
            int distance = NativeInfDistance(gateway.Start, gateway.End, proposed, out _);
            if (distance == -1 || distance >= radiusSquared || !NativeInfCrosses(gateway.Start, gateway.End, current, proposed))
            {
                continue;
            }
            if (gateway.Destination.FieldId is < 0 or > ushort.MaxValue || gateway.Destination.Direction is < 0 or > 255 || gateway.Destination.FieldId >= 72 && (gateway.Destination.X == 0x7FFF || gateway.Destination.Triangle is < 0 or >= 0x7FFF))
            {
                throw new NotSupportedException("Reached INF gateway requires an unproved world or sentinel arrival contract; movement and transition packet preserved.");
            }
            selectedGateway = gateway;
        }
        Action? gatewayCommit = selectedGateway is null ? null : PrepareInfGatewayAdmission(selectedGateway);
        List<(ActorState Actor, bool Contact, bool Entry, int InfIndex)> changes = [];
        EventObject[] areas = program!.NativeObjects.Select(index => program.Objects[index]).Where(obj => obj.IsArea).ToArray();
        for (int infIndex = 0; infIndex < InfAreaTriggers.Count; infIndex++)
        {
            InfAreaTrigger trigger = InfAreaTriggers[infIndex];
            if (trigger.AreaIndex == 0xFF)
            {
                continue;
            }
            if ((uint)trigger.AreaIndex >= areas.Length)
            {
                throw new InvalidDataException("INF area index is outside the original JSM area group range.");
            }
            ActorState area = actors[areas[trigger.AreaIndex].Index];
            if (!area.DoorLineEnabled)
            {
                continue;
            }
            int distance = NativeInfDistance(trigger.Start, trigger.End, current, out Vector3 closest);
            bool nearby = distance != -1 && distance < radiusSquared;
            bool coincident = current.X == closest.X && current.Y == closest.Y;
            int mode = -1;
            if (nearby)
            {
                if (coincident || InfFacingContact(current, closest, direction))
                {
                    mode = trigger.Side;
                }
            }
            else if (trigger.Side <= 3 || NativeInfSide(trigger.Start, trigger.End, current) <= 0)
            {
                mode = trigger.Side is 2 or 4 ? 1 : trigger.Side is 3 or 5 ? 0 : -1;
            }
            if (mode is < 0 or > 5)
            {
                continue;
            }
            bool contact = mode % 2 == 0;
            bool priorContact = changes.LastOrDefault(change => change.Actor == area, (Actor: area, Contact: area.DoorLineContact, Entry: false, InfIndex: infIndex)).Contact;
            if (contact != priorContact)
            {
                changes.Add((area, contact, contact, trigger.NativeIndex));
            }
        }
        // Preparation and all area validation complete before committing either
        // the world packet or field contacts. The host commit is atomic.
        if (selectedGateway?.Destination.FieldId < 72)
        {
            gatewayCommit?.Invoke();
        }
        foreach ((ActorState area, bool contact, bool entry, int infIndex) in changes)
        {
            CommitInfAreaContact(area, contact, entry, infIndex);
        }
        if (selectedGateway?.Destination.FieldId >= 72)
        {
            gatewayCommit?.Invoke();
        }
    }

    private void CommitInfAreaContact(ActorState area, bool contact, bool entry, int infIndex)
    {
        area.DoorLineContact = contact;
        if (entry)
        {
            area.DoorLineEntryPending = true;
        }
    }

    private Action PrepareInfGatewayAdmission(InfGateway gateway)
    {
        FieldTransition destination = gateway.Destination;
        if (Scene.MapJumpDisabled || destination.FieldId == 0x7FFF || destination.FieldId is < 0 or > ushort.MaxValue || destination.Direction is < 0 or > 255 || destination.FieldId >= 72 && (destination.X == 0x7FFF || destination.Triangle is < 0 or >= 0x7FFF))
        {
            throw new NotSupportedException("INF gateway admission requires its current enabled original packet.");
        }
        if (UnsupportedWorldControlAliases() is string unsupportedAliases)
        {
            throw new NotSupportedException(unsupportedAliases);
        }
        NativeWorldTransitionRequest? request = destination.FieldId < 72 ? CreateWorldTransitionRequest(ReadInfWorldPacket(gateway), NativeWorldTransitionOrigin.Gateway, gateway) : null;
        Action? commit = request is null ? null : PrepareWorldTransitionCommit(request);
        return () =>
        {
            commit?.Invoke();
            if (request is not null)
            {
                Scene.WorldTransitionRequest = request;
            }
            else
            {
                services.RequestField(destination);
            }
            transitionRequested = true;
        };
    }

    private void DispatchInfAreaEntry(ActorState actor)
    {
        if (program!.Objects[actor.ObjectId].IsArea != true)
        {
            return;
        }
        if (!actor.DoorLineEnabled || !IsUserControlEnabled)
        {
            actor.DoorLineEntryPending = false;
            return;
        }
        if (!actor.DoorLineEntryPending)
        {
            return;
        }
        AdmitInfAreaEntry(actor);
        actor.DoorLineEntryPending = false;
    }

    private bool AdmitInfAreaEntry(ActorState actor)
    {
        EventObject area = program!.Objects[actor.ObjectId];
        if (area.Scripts.Count < 3)
        {
            throw new InvalidDataException("Reached INF area entry has no original local script2.");
        }
        if (!fibers.Any(fiber => fiber.ObjectId == actor.ObjectId && fiber.Priority == 6 && !fiber.Complete))
        {
            Queue(actor.ObjectId, area.Scripts[2].Id, 6, false);
            return true;
        }
        return false;
    }

    private static bool InfFacingContact(Vector3 current, Vector3 closest, int direction)
    {
        return (byte)(NativeDirection(current.X, current.Y, closest.X, closest.Y) - direction + 64) < 128;
    }

    private static Vector3 NativeInfPoint(Vector3 point)
    {
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z) || point.X is < short.MinValue or > short.MaxValue || point.Y is < short.MinValue or > short.MaxValue || point.Z is < short.MinValue or > short.MaxValue)
        {
            throw new NotSupportedException("INF movement coordinates exceed the verified signed field bounds.");
        }
        return new Vector3(MathF.Floor(point.X), MathF.Floor(point.Y), MathF.Floor(point.Z));
    }

    private static int NativeInfDistance(Vector3 start, Vector3 end, Vector3 point, out Vector3 closest)
    {
        if (NativeInfPoint(start) != start || NativeInfPoint(end) != end)
        {
            throw new InvalidDataException("INF endpoints must contain original signed16 integer coordinates.");
        }
        int x = (int)(end.X - start.X);
        int y = (int)(end.Y - start.Y);
        int z = (int)(end.Z - start.Z);
        int denominator = unchecked(x * x + y * y + z * z);
        if (denominator <= 0)
        {
            throw new NotSupportedException("Reached INF line has degenerate or unproved overflowing native geometry.");
        }
        int dot = unchecked(x * (int)(start.X - point.X) + y * (int)(start.Y - point.Y) + z * (int)(start.Z - point.Z));
        int parameter = unchecked(-256 * dot) / denominator;
        int closestX = (int)start.X + (unchecked(parameter * x) >> 8);
        int closestY = (int)start.Y + (unchecked(parameter * y) >> 8);
        int closestZ = (int)start.Z + (unchecked(parameter * z) >> 8);
        closest = new Vector3(closestX, closestY, closestZ);
        if (closestX < Math.Min(start.X, end.X) || closestX > Math.Max(start.X, end.X) || closestY < Math.Min(start.Y, end.Y) || closestY > Math.Max(start.Y, end.Y))
        {
            return -1;
        }
        int dx = closestX - (int)point.X;
        int dy = closestY - (int)point.Y;
        int dz = closestZ - (int)point.Z;
        return unchecked(dx * dx + dy * dy + dz * dz);
    }

    private static bool NativeInfCrosses(Vector3 start, Vector3 end, Vector3 previous, Vector3 proposal)
    {
        int before = NativeInfSide(start, end, previous);
        int after = NativeInfSide(start, end, proposal);
        return before < 0 ? after >= 0 : before > 0 ? after <= 0 : after != 0;
    }

    private static int NativeInfSide(Vector3 start, Vector3 end, Vector3 point)
    {
        int x = (int)(end.X - start.X);
        int y = (int)(end.Y - start.Y);
        return unchecked(x * (int)(point.Y - start.Y) - y * (int)(point.X - start.X));
    }
}
