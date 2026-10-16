using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>Physical scroll row WORD8/10, preserved by52B4F0 independently of its reset mode/status.</summary>
public sealed record NativeBackgroundScrollCurrent(int Version, short X, short Y);

public sealed partial class EventEngine
{
    private static Dictionary<int, NativeBackgroundScrollCurrent> CaptureNativeBackgroundScrollCurrents(SceneEventState scene, bool executedCallback)
    {
        Dictionary<int, NativeBackgroundScrollCurrent> currents = [];
        bool nativeAllocation = scene.NativeFieldLoadVersion == 1 && scene.NativeFieldInitialization is { Version: 1, Stage: NativeFieldInitializationStage.Completed, ExecutableHash: NativeFieldRuntimeProfile.FF8EnExecutableHash, FieldClock: 20 };
        bool normalCallback = nativeAllocation && scene.NormalBackgroundScrollCenter is { Version: 1, Frame: > 0 } normal && normal.CameraIndex == scene.CameraIndex && normal.FieldId == scene.NativeFieldInitialization!.FieldId;
        bool actorCallback = nativeAllocation && scene.ActorBackgroundScroll is { Status: 1 or 2 } actor && NativeActorScrollPhase.IsValid(actor);
        bool physicalCallback = nativeAllocation && (executedCallback || normalCallback || actorCallback);
        for (int layer = 1; layer < 8; layer++)
        {
            if (scene.ActorBackgroundScroll is { X: short actorX, Y: short actorY } retained && retained.Layer == layer)
            {
                currents[layer] = new(1, actorX, actorY);
            }
            else if (scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? position))
            {
                if (position.X is short x && position.Y is short y)
                {
                    currents[layer] = new(1, x, y);
                }
            }
            else if (layer == scene.CameraIndex)
            {
                if (normalCallback)
                {
                    NativeNormalBackgroundScrollCenter selected = scene.NormalBackgroundScrollCenter!;
                    currents[layer] = new(1, selected.X, selected.Y);
                }
                else if (!executedCallback && scene.NativeBackgroundScrollCurrents?.TryGetValue(layer, out NativeBackgroundScrollCurrent? retainedCurrent) == true)
                {
                    currents[layer] = retainedCurrent;
                }
            }
            else if (physicalCallback)
            {
                //475E60 mode0 writes zero to each nonselected physical row.
                currents[layer] = new(1, 0, 0);
            }
            else if (scene.NativeBackgroundScrollCurrents?.TryGetValue(layer, out NativeBackgroundScrollCurrent? prior) == true)
            {
                currents[layer] = prior;
            }
        }
        return currents;
    }

    private static void ValidateNativeBackgroundScrollCurrents(Checkpoint saved)
    {
        if (saved.Scene.NativeBackgroundScrollCurrents is not { } currents)
        {
            return;
        }
        foreach ((int layer, NativeBackgroundScrollCurrent current) in currents)
        {
            if (layer is < 1 or > 7 || current is not { Version: 1 })
            {
                throw new InvalidDataException("Saved physical background current lacks its original row/WORD ownership.");
            }
            if (saved.Scene.BackgroundScrollPositions.TryGetValue(layer, out BackgroundScrollPosition? explicitRow) && (explicitRow.X != current.X || explicitRow.Y != current.Y) || saved.Scene.ActorBackgroundScroll is { } actor && actor.Layer == layer && (actor.X != current.X || actor.Y != current.Y))
            {
                throw new InvalidDataException("Saved physical background current disagrees with its active row.");
            }
            if (saved.Scene.NormalBackgroundScrollCenter is { } normal && normal.CameraIndex == layer && (normal.X != current.X || normal.Y != current.Y))
            {
                throw new InvalidDataException("Saved physical background current disagrees with its selected normal callback.");
            }
        }
    }
}
