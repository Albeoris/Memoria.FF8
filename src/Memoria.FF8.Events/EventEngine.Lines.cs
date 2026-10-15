using System.Numerics;

namespace Memoria.FF8.Events;

public sealed record LineCrossing(int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, Vector3 Position, long ActionIndex);

public sealed partial class EventEngine
{
    private sealed record NativeLineContactChange(int ObjectId, bool Contact, bool Crossing, Vector3 Position, byte PendingEvents = 0, byte? FacingDirection = null, bool FacingContact = false);

    private readonly List<LineCrossing> lineCrossings = [];
    public IReadOnlyList<LineCrossing> LineCrossings => lineCrossings;
    public event Action<LineCrossing>? LineCrossed;

    private IReadOnlyList<NativeLineContactChange> PrepareNativeLineMovement(ActorState player, FieldFixedPosition proposal, int direction)
    {
        if (!program!.Objects.Any(obj => obj.IsDoor))
        {
            return [];
        }
        if (Scene.NativeDoorDriverVersion != 2)
        {
            throw new NotSupportedException($"Legacy field{FieldId} SETLINE state lacks persisted native initialization provenance; load a snapshot containing its initialized native state.");
        }
        return PrepareNativeDoorMovement(player, proposal, direction);
    }

    private void CommitNativeLineMovement(IReadOnlyList<NativeLineContactChange> changes)
    {
        if (Scene.NativeDoorDriverVersion == 2)
        {
            CommitNativeDoorMovement(changes);
        }
    }

    private void ValidatePendingNativeLineCrossings()
    {
        if (Scene.NativeDoorDriverVersion == 2)
        {
            ValidatePendingNativeDoorEvents();
            return;
        }
        if (IsUserControlEnabled && actors.Any(line => program!.Objects[line.ObjectId].IsDoor && line.NativeLineCrossingPending))
        {
            throw new NotSupportedException("Legacy native door pending contact lacks the proved direct-slot mapping; load a snapshot containing its initialized native state.");
        }
    }

    private void DispatchNativeLineCrossing(ActorState line)
    {
        if (Scene.NativeDoorDriverVersion == 2)
        {
            DispatchNativeDoorEvents(line);
        }
    }

    private void ValidateNativeLineCheckpoint(Checkpoint saved)
    {
        ValidateNativeDoorCheckpoint(saved);
        foreach (ActorState line in saved.Actors)
        {
            if (line.NativeLineContactVersion is < 0 or > 2 || line.NativeLineContactVersion == 2 && saved.Scene.NativeDoorDriverVersion != 2 || line.NativeLineCrossingPending && line.NativeLineContactVersion == 0)
            {
                throw new InvalidDataException("Saved native SETLINE contact has an invalid schema or unknown pending crossing.");
            }
            if (line.NativeLineContactVersion == 0)
            {
                continue;
            }
            if (saved.Scene.NativePlayerMovementVersion != 1 || line.LineStart is not Vector3 start || line.LineEnd is not Vector3 end || NativeInfPoint(start) != start || NativeInfPoint(end) != end || !ValidNativeLineOwner(line))
            {
                throw new InvalidDataException("Saved native SETLINE contact lacks certified player movement or its original endpoints/command/script7.");
            }
        }
    }

    private bool ValidNativeLineOwner(ActorState line)
    {
        if (program is null || (uint)line.ObjectId >= program.Objects.Count || (uint)line.LineOperation >= program.Operations.Count || program.Operations[line.LineOperation].Opcode != Albeoris.Games.FF8.Jsm.Jsm.Opcode.SETLINE)
        {
            return false;
        }
        EventObject obj = program.Objects[line.ObjectId];
        return obj.IsDoor && obj.Scripts.Any(script => line.LineOperation >= script.Start && line.LineOperation < script.End);
    }

    private bool EmptyNativeLineScript(ActorState line, int local)
    {
        EventScript script = program!.Objects[line.ObjectId].Scripts[local];
        return script.End == script.Start + 2 && program.Operations[script.Start].Opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.LBL && program.Operations[script.Start + 1].Opcode == Albeoris.Games.FF8.Jsm.Jsm.Opcode.RET;
    }

    private void DispatchLineCrossings(Vector3 previous, Vector3 position)
    {
        if (program is null || previous == position)
        {
            return;
        }
        foreach (ActorState line in actors)
        {
            if (!line.Active || !line.LineEnabled || line.LineStart is not Vector3 start || line.LineEnd is not Vector3 end)
            {
                continue;
            }
            if (line.LineContact && !IsOnLine(previous, start, end))
            {
                line.LineContact = false;
            }
            if (line.LineContact || !CrossesLine(previous, position, start, end))
            {
                continue;
            }
            EventObject obj = program.Objects[line.ObjectId];
            if (obj.Scripts.Count <= 7)
            {
                throw new InvalidDataException($"Field {FieldId} object {line.ObjectId} SETLINE operation {line.LineOperation} has no crossing script at offset 7.");
            }
            EventScript script = obj.Scripts[7];
            line.LineContact = true;
            if (fibers.Any(fiber => !fiber.Complete && fiber.ObjectId == line.ObjectId && fiber.Script.Id == script.Id))
            {
                continue;
            }
            Queue(line.ObjectId, script.Id, 1, false);
            LineCrossing crossing = new(FieldId, line.ObjectId, script.Id, line.LineOperation, Frame, position, activeActionIndex ?? actionLog.Count);
            lineCrossings.Add(crossing);
            LineCrossed?.Invoke(crossing);
        }
    }

    private static bool IsOnLine(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float lengthSquared = delta.LengthSquared();
        if (lengthSquared == 0)
        {
            return false;
        }
        float parameter = Math.Clamp(Vector3.Dot(point - start, delta) / lengthSquared, 0, 1);
        return Vector3.DistanceSquared(point, start + delta * parameter) < 0.0001f;
    }

    private static bool CrossesLine(Vector3 previous, Vector3 position, Vector3 start, Vector3 end)
    {
        Vector2 movement = new(position.X - previous.X, position.Y - previous.Y);
        Vector2 segment = new(end.X - start.X, end.Y - start.Y);
        Vector2 offset = new(start.X - previous.X, start.Y - previous.Y);
        float denominator = Cross(movement, segment);
        if (Math.Abs(denominator) < 0.000001f)
        {
            return false;
        }
        float movementParameter = Cross(offset, segment) / denominator;
        float segmentParameter = Cross(offset, movement) / denominator;
        if (movementParameter <= 0.000001f || movementParameter > 1 || segmentParameter < 0 || segmentParameter > 1)
        {
            return false;
        }
        float playerHeight = previous.Z + (position.Z - previous.Z) * movementParameter;
        float lineHeight = start.Z + (end.Z - start.Z) * segmentParameter;
        // Only the selected coplanar corridor lines are verified; do not trigger an overlapping line on another floor.
        return Math.Abs(playerHeight - lineHeight) < 1;
    }

    private static float Cross(Vector2 first, Vector2 second) => first.X * second.Y - first.Y * second.X;

    private static bool ActorsTouch(ActorState current, ActorState target)
    {
        // Installed PC handler 0x51ED80 -> 0x47B460: signed arithmetic shift of position deltas,
        // TARGET push502 plus CALLER talk504, strict XY comparison and Z bounds.
        // Keep these owners: Quistis's corridor follow script uses its enlarged talk radius to select idle.
        // Swapping the radii keeps ISTOUCH false and repeatedly reissues RMOVE/walk while Squall has stopped.
        int z = (int)MathF.Floor(target.Z - current.Z);
        if (z <= -127 || z >= 128)
        {
            return false;
        }
        int x = (int)MathF.Floor(target.X - current.X);
        int y = (int)MathF.Floor(target.Y - current.Y);
        int radius = (ushort)target.PushRadius + (ushort)current.TalkRadius;
        return (long)x * x + (long)y * y < (long)radius * radius;
    }
}
