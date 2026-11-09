using System.Numerics;
using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    private void Line(int entity, FieldLineCommand command)
    {
        if (command.Operation == FieldLineOperation.Define)
        {
            if (Process.Lines.Length < 32)
            {
                Process = Process with { Lines = [.. Process.Lines, new(Process.Lines.Length, entity, command.X1, command.Y1, command.Z1, command.X2, command.Y2, command.Z2)] };
            }
            return;
        }
        Ff7Line line = Process.Lines.LastOrDefault(line => line.Entity == entity) ?? throw new InvalidDataException("A FFVII line operation requires its allocated LINE object.");
        Ff7Line changed = command.Operation switch
        {
            FieldLineOperation.Replace => line with { X1 = command.X1, Y1 = command.Y1, Z1 = command.Z1, X2 = command.X2, Y2 = command.Y2, Z2 = command.Z2 },
            FieldLineOperation.Enable => line with { Enabled = command.Value, Touch = command.Value != 0 && line.Touch },
            FieldLineOperation.Slip => line with { SlipDisabled = command.Value },
            _ => throw new NotSupportedException("Unsupported FFVII line operation.")
        };
        SetLine(changed);
    }

    public void UpdateLineContacts(Ff7Actor fromActor, Ff7Actor toActor, bool moving)
    {
        Vector3 from = new(fromActor.X4096 >> 12, fromActor.Y4096 >> 12, fromActor.Z4096 >> 12);
        Vector3 to = new(toActor.X4096 >> 12, toActor.Y4096 >> 12, from.Z);
        bool blocked = false;
        foreach (Ff7Line line in Process.Lines)
        {
            if (line.Enabled != 1)
            {
                continue;
            }
            Vector3 a = new(line.X1, line.Y1, line.Z1);
            Vector3 b = new(line.X2, line.Y2, line.Z2);
            int distance = Ff7Session.DistanceToSegment(a, b, from, out Vector3 nearest);
            bool touching = distance != -1 && distance < unchecked(fromActor.CollisionRadius * fromActor.CollisionRadius);
            Ff7Line changed = line with { Touch = touching, Enter = line.Enter || touching && !line.Touch, Leave = line.Leave || !touching && line.Touch, OnLine = false };
            if (moving && touching)
            {
                blocked |= line.SlipDisabled == 1;
                int fromSide = Ff7Session.Side(a, b, from);
                int toSide = Ff7Session.Side(a, b, to);
                bool across = fromSide >= 0 && toSide < 0 || toSide >= 0 && fromSide < 0 || fromSide > 0 && toSide <= 0 || toSide > 0 && fromSide <= 0;
                bool exact = from.X == nearest.X && from.Y == nearest.Y;
                byte angle = exact ? line.ProximityAngle : (Movement ?? throw new NotSupportedException("FFVII lines require native direction tables.")).Direction((int)(nearest.X - from.X), (int)(nearest.Y - from.Y));
                bool push = exact || ((angle - toActor.Direction + 64) & 255) < 128;
                changed = changed with { Across = line.Across || across, Push = line.Push || push, OnLine = push, ProximityAngle = angle };
            }
            SetLine(changed);
        }
        Process = Process with { LineBlocked = moving && blocked };
    }

    public bool RequestLineTalk(Ff7Actor actor)
    {
        bool requested = false;
        foreach (Ff7Line line in Process.Lines.Where(line => line.Enabled == 1 && line.OnLine && ((line.ProximityAngle - actor.Direction + 32) & 255) < 64))
        {
            SetLine(line with { Talk = true });
            requested = true;
        }
        return requested;
    }

    public void ClearLineOnLine()
    {
        Process = Process with { Lines = Process.Lines.Select(line => line with { OnLine = false }).ToArray() };
    }

    public void ClearLineRequests()
    {
        Process = Process with { Lines = Process.Lines.Select(line => line with { Talk = false, Push = false, Across = false, Enter = false, Leave = false }).ToArray() };
    }

    private void SetLine(Ff7Line line)
    {
        Process = Process with { Lines = Process.Lines.Select(current => current.Slot == line.Slot ? line : current).ToArray() };
    }
}

public sealed partial class Ff7Session
{
    private void DispatchLineScripts()
    {
        foreach (Ff7Line line in Host.Process.Lines)
        {
            if (line.Talk && !Host.Process.PlayerLocked)
            {
                Machine.TryRequestScript(line.Entity, 1);
            }
            if (line.Push)
            {
                Machine.TryRequestScript(line.Entity, 2);
            }
            if (line.Across)
            {
                Machine.TryRequestScript(line.Entity, 3);
            }
            if (line.Enter)
            {
                Machine.TryRequestScript(line.Entity, 5);
            }
            if (line.Leave)
            {
                Machine.TryRequestScript(line.Entity, 6);
            }
            if (line.Touch)
            {
                Machine.TryRequestScript(line.Entity, 4);
            }
        }
        Host.ClearLineRequests();
    }
}
