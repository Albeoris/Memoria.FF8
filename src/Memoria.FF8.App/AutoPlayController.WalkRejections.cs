using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;

namespace Memoria.FF8;

internal sealed record AutoPlayWalkRejectionGuard(int Version, string EventId, int ObjectId, string Predicate, string Geometry);
internal sealed record AutoPlayScriptedWalkReturn(int ScriptId, int Operation, FieldFixedPosition Target, short StopDistance);
internal sealed record AutoPlayWalkRejection(AutoPlayContactSource Source, AutoPlayWalkRejectionGuard Guard, FieldFixedPosition? Position = null, AutoPlayScriptedWalkReturn? ScriptedReturn = null);
internal sealed record AutoPlayPendingWalkInterruption(AutoPlayContactSource Source, FieldFixedPosition Position, AutoPlayWalkRejectionGuard Guard, AutoPlayScriptedWalkReturn? ScriptedReturn = null, long? ReturnSequence = null);
internal sealed record AutoPlayWalkAttempt(AutoPlayContactSource Source, FieldFixedPosition Position, IReadOnlyDictionary<int, AutoPlayWalkRejectionGuard> Guards, AutoPlayScriptedWalkReturn? ScriptedReturn, long? ReturnSequence);

internal sealed partial class AutoPlayController
{
    private AutoPlayWalkAttempt CaptureWalkAttempt(GameWorld world, AutoPlayContactSource source, IReadOnlyList<AutoPlayTarget> targets)
    {
        Dictionary<int, AutoPlayWalkRejectionGuard> guards = [];
        foreach (AutoPlayTarget owner in targets.Where(target => target.Kind == AutoPlayTargetKind.Contact && !target.RequiresConfirm).DistinctBy(target => target.ObjectId))
        {
            if (CaptureWalkRejectionGuard(world, owner.CoverageId, owner.ObjectId) is { } guard)
            {
                guards.Add(owner.ObjectId, guard);
            }
        }
        FieldFixedPosition position = world.Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("A walking attempt requires its retained native source position.");
        var (scriptedReturn, sequence) = ReadScriptedWalkReturn(world, position);
        return new(source, position, guards, scriptedReturn, sequence);
    }

    private static AutoPlayWalkRejectionGuard? CaptureWalkRejectionGuard(GameWorld world, string eventId, int objectId)
    {
        string? predicate = world.ReadAutoPlayWalkRejectionState(eventId, objectId);
        if (predicate is null)
        {
            return null;
        }
        ActorState actor = world.Events.Actors[objectId];
        var geometry = new
        {
            actor.Active, actor.ModelId, actor.X, actor.Y, actor.Z, actor.WalkmeshTriangle,
            actor.CollisionEnabled, actor.PushEnabled, actor.TalkEnabled, actor.LineEnabled, actor.DoorLineEnabled,
            actor.PushRadius, actor.TalkRadius,
            Start = actor.LineStart is { } start ? new[] { start.X, start.Y, start.Z } : null,
            End = actor.LineEnd is { } end ? new[] { end.X, end.Y, end.Z } : null,
            PartyActors = Enumerable.Range(0, 3).Select(slot => NativeFieldPartyBinding.TryGetObjectId(world.Events.Scene, slot, out int owner) ? (int?)owner : null).ToArray(),
            world.Events.Scene.RunEnabled, world.Events.PlayerMovementSpeedPercent
        };
        string fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(geometry, BeforeSnapshotJsonOptions)));
        return new(1, eventId, objectId, predicate, fingerprint);
    }

    private void BeginWalkInterruption(AutoPlayWalkAttempt attempt, AutoPlayWalkResult result)
    {
        if (result.InterruptingObjectId is int owner && attempt.Guards.TryGetValue(owner, out AutoPlayWalkRejectionGuard? guard))
        {
            run.PendingWalkInterruption = new(attempt.Source, attempt.Position, guard, attempt.ScriptedReturn, attempt.ReturnSequence);
        }
    }

    private void FinishWalkInterruption(GameWorld world)
    {
        if (run.PendingWalkInterruption is not { } pending)
        {
            return;
        }
        run.PendingWalkInterruption = null;
        if (world.Events.FieldId != pending.Source.FieldId || !world.HasCurrentFieldAllocation || VisitKey() != pending.Source.VisitKey)
        {
            return;
        }
        AutoPlayContactSource current = CaptureContactSource(world, VisitKey(), CurrentNavigationKey(world, VisitKey()), pending.Source.Target);
        AutoPlayWalkRejectionGuard? guard = CaptureWalkRejectionGuard(world, pending.Guard.EventId, pending.Guard.ObjectId);
        FieldFixedPosition position = world.Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("A returned walking attempt requires its native position.");
        if (guard != pending.Guard)
        {
            return;
        }
        var (scriptedReturn, sequence) = ReadScriptedWalkReturn(world, position);
        bool repeatedMove = scriptedReturn is not null && scriptedReturn == pending.ScriptedReturn && sequence > pending.ReturnSequence && SameScriptedWalkSource(pending.Source, current);
        bool stationary = SameWalkReturnDomain(pending.Source, current, pending.Position, position) && WalkReturnedWithoutAdvance(pending.Source.Target, pending.Position, position);
        if (!repeatedMove && !stationary)
        {
            return;
        }
        AutoPlayWalkRejection rejection = repeatedMove ? new(current, pending.Guard, position, scriptedReturn) : new(pending.Source, pending.Guard, pending.Position);
        if (!IsWalkRejected(rejection.Source, repeatedMove ? position : pending.Position, guard, run.WalkRejections, rejection.ScriptedReturn))
        {
            run.WalkRejections.Add(rejection);
            RecordRecentAction("Native walk returned before " + pending.Source.Target.Id + " via " + pending.Guard.EventId);
        }
    }

    private static (AutoPlayScriptedWalkReturn? Return, long? Sequence) ReadScriptedWalkReturn(GameWorld world, FieldFixedPosition position)
    {
        if (world.Events.PlayerActor is { NativeScriptMovement: { Active: false, Cancelled: false, SupersededByPartyJoin: false, Asynchronous: false, TargetNativeObjectId: null, StopDistance: > 0 } move } player &&
            move.Triangle == player.WalkmeshTriangle && position == new FieldFixedPosition(move.FixedX, move.FixedY, move.FixedZ))
        {
            return (new(move.ScriptId, move.Operation, new(move.TargetX, move.TargetY, move.TargetZ), move.StopDistance), move.FiberSequence);
        }
        return (null, null);
    }

    internal static bool WalkReturnedWithoutAdvance(AutoPlayTarget target, FieldFixedPosition before, FieldFixedPosition after)
    {
        double x = target.ApproachX ?? target.X;
        double y = target.ApproachY ?? target.Y;
        double z = target.ApproachZ ?? target.Z;
        double Distance(FieldFixedPosition position)
        {
            double dx = position.X / 4096d - x;
            double dy = position.Y / 4096d - y;
            double dz = position.Z / 4096d - z;
            return dx * dx + dy * dy + dz * dz;
        }
        return Distance(after) >= Distance(before);
    }

    private static bool SameWalkSourceDomain(AutoPlayContactSource before, AutoPlayContactSource after)
    {
        return before with { PlayerPosition = before.PlayerPosition with { Direction = 0 } } == after with { PlayerPosition = after.PlayerPosition with { Direction = 0 } };
    }

    internal static bool SameWalkReturnDomain(AutoPlayContactSource before, AutoPlayContactSource after, FieldFixedPosition original, FieldFixedPosition returned)
    {
        // A scripted return to the exact same shared edge may resolve either
        // triangle. Keep the original triangle when matching a future attempt.
        if (original == returned)
        {
            after = after with { PlayerPosition = after.PlayerPosition with { Triangle = before.PlayerPosition.Triangle } };
        }
        return SameWalkSourceDomain(before, after);
    }

    private static bool SameScriptedWalkSource(AutoPlayContactSource before, AutoPlayContactSource after)
    {
        return SameWalkSourceDomain(before with { PlayerPosition = before.PlayerPosition with { X = after.PlayerPosition.X, Y = after.PlayerPosition.Y } }, after);
    }

    internal static bool IsWalkRejected(AutoPlayContactSource source, FieldFixedPosition position, AutoPlayWalkRejectionGuard? guard, IReadOnlyList<AutoPlayWalkRejection> rejections, AutoPlayScriptedWalkReturn? scriptedReturn = null)
    {
        return guard is not null && rejections.Any(rejection => rejection.Guard == guard &&
            (rejection.ScriptedReturn is null ? rejection.Position == position && SameWalkSourceDomain(rejection.Source, source) : rejection.ScriptedReturn == scriptedReturn && SameScriptedWalkSource(rejection.Source, source)));
    }

    private IReadOnlyList<AutoPlayTarget> FilterRejectedWalkTargets(GameWorld world, string visit, string navigation, IReadOnlyList<AutoPlayTarget> targets)
    {
        if (run.WalkRejections.Count == 0)
        {
            return targets;
        }
        FieldFixedPosition position = world.Events.Scene.NativePlayerPosition ?? throw new NotSupportedException("Walking rejection matching requires the retained native player position.");
        var (scriptedReturn, _) = ReadScriptedWalkReturn(world, position);
        return targets.Where(target => !run.WalkRejections.Any(rejection =>
        {
            if (rejection.Guard.Version != 1 || rejection.Source.Version != 1)
            {
                throw new InvalidDataException("Unsupported automatic walking rejection receipt.");
            }
            if (rejection.Source.VisitKey != visit || rejection.Source.NavigationKey != navigation || rejection.Source.Target != target)
            {
                return false;
            }
            AutoPlayContactSource source = CaptureContactSource(world, visit, navigation, target);
            return IsWalkRejected(source, position, CaptureWalkRejectionGuard(world, rejection.Guard.EventId, rejection.Guard.ObjectId), [rejection], scriptedReturn);
        })).ToArray();
    }
}
