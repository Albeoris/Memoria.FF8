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
                bool across = Ff7Session.CrossesLine(line, fromActor, toActor);
                var (push, angle) = ReadLinePush(line, from, nearest, toActor.Direction, Movement);
                changed = changed with { Across = line.Across || across, Push = line.Push || push, OnLine = push, ProximityAngle = angle };
            }
            SetLine(changed);
        }
        Process = Process with { LineBlocked = moving && blocked };
    }

    internal static (bool Push, byte Angle) ReadLinePush(Ff7Line line, Vector3 from, Vector3 nearest, int direction, Ff7NativeMovement? movement)
    {
        bool exact = from.X == nearest.X && from.Y == nearest.Y;
        byte angle = exact ? line.ProximityAngle : (movement ?? throw new NotSupportedException("FFVII lines require native direction tables.")).Direction((int)(nearest.X - from.X), (int)(nearest.Y - from.Y));
        return (exact || ((angle - direction + 64) & 255) < 128, angle);
    }

    public bool RequestLineTalk(Ff7Actor actor)
    {
        bool requested = false;
        foreach (Ff7Line line in Process.Lines.Where(line => CanRequestLineTalk(line, actor)))
        {
            SetLine(line with { Talk = true });
            requested = true;
        }
        return requested;
    }

    internal static bool CanRequestLineTalk(Ff7Line line, Ff7Actor actor)
    {
        return line.Enabled == 1 && line.OnLine && ((line.ProximityAngle - actor.Direction + 32) & 255) < 64;
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
    internal static bool CrossesLine(Ff7Line line, Ff7Actor fromActor, Ff7Actor toActor)
    {
        if (line.Enabled != 1)
        {
            return false;
        }
        Vector3 from = new(fromActor.X4096 >> 12, fromActor.Y4096 >> 12, fromActor.Z4096 >> 12);
        Vector3 to = new(toActor.X4096 >> 12, toActor.Y4096 >> 12, from.Z);
        Vector3 a = new(line.X1, line.Y1, line.Z1);
        Vector3 b = new(line.X2, line.Y2, line.Z2);
        int distance = DistanceToSegment(a, b, from, out _);
        if (distance < 0 || distance >= unchecked(fromActor.CollisionRadius * fromActor.CollisionRadius))
        {
            return false;
        }
        int fromSide = Side(a, b, from);
        int toSide = Side(a, b, to);
        return fromSide >= 0 && toSide < 0 || toSide >= 0 && fromSide < 0 || fromSide > 0 && toSide <= 0 || toSide > 0 && fromSide <= 0;
    }

    private void DispatchLineScripts()
    {
        foreach (var request in ReadNativeLineScriptRequests(() => Host))
        {
            RequestInteractionScript(request.Entity, request.Script);
        }
        Host.ClearLineRequests();
    }

    internal static IEnumerable<(int Entity, int Script)> ReadNativeLineScriptRequests(Func<Ff7FieldHost> readHost)
    {
        foreach (Ff7Line line in readHost().Process.Lines)
        {
            if (line.Talk && !readHost().Process.PlayerLocked)
            {
                yield return (line.Entity, 1);
            }
            if (line.Push)
            {
                yield return (line.Entity, 2);
            }
            if (line.Across)
            {
                yield return (line.Entity, 3);
            }
            if (line.Enter)
            {
                yield return (line.Entity, 5);
            }
            if (line.Leave)
            {
                yield return (line.Entity, 6);
            }
            if (line.Touch)
            {
                yield return (line.Entity, 4);
            }
        }
    }
}
