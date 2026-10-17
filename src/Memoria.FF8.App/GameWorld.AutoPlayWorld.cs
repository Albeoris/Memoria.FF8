using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record WorldAutoPlayTarget(string Id, int X, int RawZ, int DestinationField, int Order);

internal sealed partial class GameWorld
{
    public IReadOnlyList<WorldAutoPlayTarget> DiscoverAutoPlayWorldTargets()
    {
        EnsureField();
        if (session.State.Scene != SceneKind.WorldMap || worldScene is null)
        {
            return [];
        }
        return worldScene.DiscoverAutoPlayEntrances();
    }

    public PlayerActionResult ExecuteAutoPlayWorldTarget(WorldAutoPlayTarget target)
    {
        EnsureField();
        WorldMapScene scene = worldScene ?? throw new InvalidOperationException("World automatic travel requires the current world scene.");
        WorldAutoPlayTarget current = scene.DiscoverAutoPlayEntrances().SingleOrDefault(item => item.Id == target.Id) ?? throw new InvalidOperationException("The requested world entrance is no longer reachable.");
        if (current != target)
        {
            throw new InvalidOperationException("The requested world entrance differs from its current discovery.");
        }
        scene.DebugTeleportToEntrance(target);
        int frames = Math.Max(1, NativeWorldEntryControl.DelayAfterCallback(scene.State.Frame, 0));
        PlayerActionResult result = Apply(new PlayerAction(PlayerActionKind.Wait, Frames: frames));
        if (result.Trigger.Kind == PlayerTriggerKind.FieldTransition)
        {
            if (result.Trigger.FieldId != target.DestinationField)
            {
                throw new NotSupportedException("The actual world entrance callback entered a different destination from its discovery.");
            }
            return result;
        }
        if (result.Accepted && (result.Trigger.Kind is PlayerTriggerKind.Dialogue or PlayerTriggerKind.Battle or PlayerTriggerKind.Movie or PlayerTriggerKind.PartyMenu or PlayerTriggerKind.Tutorial || result.Trigger.Kind == PlayerTriggerKind.WorldMap && scene.State.EventUi is { ScriptMode: not 0 }))
        {
            return result;
        }
        throw new NotSupportedException("The actual world entrance callback neither entered its destination nor yielded to a native scene owner.");
    }
}
