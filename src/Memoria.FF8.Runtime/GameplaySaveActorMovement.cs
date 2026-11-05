using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Logical mode1 movement. Current raw placement belongs to Actor.Pose; geometry proposals are recreated by current providers.</summary>
public sealed record GameplaySaveMovementWorker([property: JsonRequired] bool Active, [property: JsonRequired] long FiberSequence, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Operation, [property: JsonRequired] GameplaySaveActorPosition Target, [property: JsonRequired] short Speed, [property: JsonRequired] short StopDistance, [property: JsonRequired] byte MovementDirection, [property: JsonRequired] byte FacingDirection, [property: JsonRequired] bool Asynchronous, [property: JsonRequired] bool AutomaticAnimation, [property: JsonRequired] int? TrackingObjectId, [property: JsonRequired] bool? ResetAnimation, [property: JsonRequired] bool Cancelled, [property: JsonRequired] bool HoldsFacing, [property: JsonRequired] bool Suspended, [property: JsonRequired] bool? AnimationFlag, [property: JsonRequired] byte? FacingHold, [property: JsonRequired] bool Accelerated)
{
    public void Validate()
    {
        if (FiberSequence <= 0 || ScriptId < 0 || Operation < 0 || Target is null || new[] { Target.X, Target.Y, Target.Z }.Any(value => (value >> 12) is < short.MinValue or > short.MaxValue) || TrackingObjectId < 0 || FacingHold > 1 || Cancelled && (Active || Suspended) || ResetAnimation == false)
        {
            throw new InvalidDataException("Gameplay movement worker contains invalid logical operands or ownership.");
        }
    }
}

public sealed record GameplaySaveMovementBackup([property: JsonRequired] long RequestSequence, [property: JsonRequired] int RequestScriptId, [property: JsonRequired] byte RequestSlot, [property: JsonRequired] GameplaySaveMovementWorker Worker, [property: JsonRequired] bool RestoreSuppressed);
public sealed record GameplaySaveActorMovement([property: JsonRequired] GameplaySaveMovementWorker? Worker, [property: JsonRequired] GameplaySaveMovementBackup? Backup)
{
    public void Validate()
    {
        Worker?.Validate();
        if (Backup is { } backup)
        {
            if (backup.RequestSequence <= 0 || backup.RequestScriptId < 0 || backup.RequestSlot > 7 || backup.Worker is null)
            {
                throw new InvalidDataException("Gameplay movement backup has an invalid request owner.");
            }
            backup.Worker.Validate();
        }
        if (Worker is null && Backup is null)
        {
            throw new InvalidDataException("Gameplay movement component has no logical worker or backup.");
        }
    }
}

/// <summary>Held VM issued bit, including the remaining actor-table or party-slot operand.</summary>
public sealed record GameplaySaveMovementIssue([property: JsonRequired] int Operation, [property: JsonRequired] int? TrackingObjectId, [property: JsonRequired] int? PartySlot);
