using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7BattleSession
{
    private Ff7BattleActionState StartPrimaryTailLaser(Ff7BattleActionState action)
    {
        Ff7BattleQueuedCommand command = action.Command;
        Ff7BattleCameraPoint source = CameraPoint(JointPoint(command.ActorSlot, 15));
        Ff7BattleCameraPoint targetCenter = TargetRootCenter(command.TargetMask);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.ScheduleTailLaser(EnsurePrimaryEffects(), new(source.X, source.Y, source.Z), new(targetCenter.X, targetCenter.Y, targetCenter.Z), command.TargetMask);
        state = state with { PrimaryEffects = primary };
        return action with { TailLaserStarted = true, PrimaryCallbacksRunning = true };
    }

    private void ApplyTailLaserSecondaryReceipts(Ff7BattlePrimaryState primary)
    {
        if (primary.TailLaserSecondaries.Count == 0)
        {
            return;
        }
        Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
        foreach (Ff7BattleSecondaryCallback callback in primary.TailLaserSecondaries)
        {
            secondary = Ff7BattleSecondaryEffects.ScheduleTailLaser(secondary, (Ff7BattleTailLaserSecondary)callback);
        }
        state = state with { SecondaryEffects = secondary };
    }

    private static void ValidateTailLaserPrimary(Ff7BattlePrimaryState primary, Ff7BattleSnapshot snapshot)
    {
        bool HasTarget(int target) => snapshot.Actors.Any(actor => actor.Actor.Slot == target);
        if (primary.TailLaserSecondaries is null || primary.TailLaserSecondaries.Count > Ff7BattleSecondaryEffects.Capacity || primary.TailLaserSecondaries.Any(callback => !IsValidTailLaserSecondary(callback) || callback.Age != 0) || primary.Slots.Any(callback => callback switch
        {
            Ff7BattleTailLaserStage stage => stage.Age > 44 || stage.Source is null || stage.Center is null || stage.Targets == 0,
            Ff7BattleTailLaserSweep sweep => sweep.Age > 14 || sweep.Source is null || sweep.Sweep is null,
            Ff7BattleTailLaserBeam beam => beam.Age > 3 || beam.Source is null || beam.End is null,
            Ff7BattleTailLaserFragment fragment => fragment.Age > 14 || fragment.Position is null || fragment.Velocity is null || fragment.Frame is < 4 or > 19,
            Ff7BattleTailLaserImpact impact => impact.Age > 29 || impact.Position is null || !HasTarget(impact.Target),
            Ff7BattleTailLaserBlast blast => blast.Age > 29 || blast.Position is null,
            Ff7BattleTailLaserSmoke smoke => smoke.Age > 23 || smoke.Position is null,
            _ => false
        }) || primary.Draws.Any(draw => draw is Ff7BattleTailLaserSpriteDraw sprite && !IsValidTailLaserDraw(sprite.Sprite) || draw is Ff7BattleTailLaserMeshDraw mesh && (mesh.Resource is < 0 or > 2 || mesh.Position is null || mesh.Basis is null)))
        {
            throw new InvalidDataException("FFVII saved Tail Laser callback or packet is invalid.");
        }
    }

    private static bool IsValidTailLaserSecondary(Ff7BattleSecondaryCallback? callback) => callback switch
    {
        Ff7BattleTailLaserSecondarySprite { Age: >= 0 and <= 7, Position: not null, Resource: 5 or 6 or 7 or 8 or 10 } => true,
        Ff7BattleTailLaserSecondarySpark { Age: >= 0 and <= 7, Position: not null, Velocity: not null } => true,
        _ => false
    };

    private static bool IsValidTailLaserDraw(Ff7BattleTailLaserSprite? sprite) => sprite is { Resource: >= 3 and <= 12 and not 9, Position: not null, Frame: >= 0 and <= 19, Scale: > 0, Brightness: <= 128 };
}
