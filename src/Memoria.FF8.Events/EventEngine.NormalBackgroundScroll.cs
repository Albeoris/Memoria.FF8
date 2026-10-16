using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>The selected normal camera's current WORD8/10, published by an actual post-VM callback.</summary>
public sealed record NativeNormalBackgroundScrollCenter(int Version, int FieldId, int CameraIndex, long Frame, short X, short Y);

/// <summary>Process WORD1CE4788/1CE478A, retained across ordinary field loads.</summary>
public sealed record NativeLayerZeroScrollCurrent(int Version, short X, short Y);

public sealed partial class EventEngine
{
    public Func<(short X, short Y)?>? ResolveNativeNormalBackgroundScrollCenter { get; set; }

    private void StepNativeNormalBackgroundScrollCenter()
    {
        if (Scene.CameraIndex == 0 && Scene.ActorBackgroundScroll is not null || Scene.BackgroundScrollPositions.ContainsKey(Scene.CameraIndex))
        {
            Scene.NormalBackgroundScrollCenter = null;
            return;
        }
        (short X, short Y)? center = ResolveNativeNormalBackgroundScrollCenter?.Invoke();
        Scene.NormalBackgroundScrollCenter = center is { } current ? new NativeNormalBackgroundScrollCenter(1, FieldId, Scene.CameraIndex, Frame, current.X, current.Y) : null;
    }

    private void PublishNativeLayerZeroScrollCurrent()
    {
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            return;
        }
        (short X, short Y)? current;
        if (Scene.ActorBackgroundScroll is { X: short actorX, Y: short actorY })
        {
            current = (actorX, actorY);
        }
        else if (Scene.BackgroundScrollPositions.TryGetValue(0, out BackgroundScrollPosition? position))
        {
            current = position.X is short x && position.Y is short y ? (x, y) : null;
        }
        else if (Scene.CameraIndex == 1)
        {
            //475E60 mode0 explicitly zeros the nonselected row's current pair.
            current = (0, 0);
        }
        else
        {
            current = Scene.NormalBackgroundScrollCenter is { CameraIndex: 0 } normal ? (normal.X, normal.Y) : null;
        }
        Scene.NativeLayerZeroScrollCurrent = current is { } pair ? new NativeLayerZeroScrollCurrent(1, pair.X, pair.Y) : null;
    }

    private static void ValidateNativeNormalBackgroundScrollCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NativeLayerZeroScrollCurrent is { Version: not 1 })
        {
            throw new InvalidDataException("Saved process layer0 current words have unsupported ownership.");
        }
        if (saved.Scene.NormalBackgroundScrollCenter is not NativeNormalBackgroundScrollCenter normal)
        {
            return;
        }
        if (normal.Version != 1 || normal.CameraIndex is < 0 or > 1 || normal.FieldId != saved.FieldId || normal.CameraIndex != saved.Scene.CameraIndex || normal.Frame <= 0 || normal.Frame > saved.Frame || saved.Scene.FieldCallbackClock is not { Version: 1 } || normal.X is < -4095 or > 4096 || normal.Y is < -4095 or > 4096 || normal.CameraIndex == 0 && saved.Scene.ActorBackgroundScroll is not null || saved.Scene.BackgroundScrollPositions.ContainsKey(saved.Scene.CameraIndex) || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            throw new InvalidDataException("Saved normal background-scroll center has no authentic callback, field allocation or exclusive selected-layer authority.");
        }
    }
}
