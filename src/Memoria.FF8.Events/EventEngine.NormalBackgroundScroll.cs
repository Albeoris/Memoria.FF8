using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

/// <summary>The selected normal camera's current WORD8/10, published by an actual post-VM callback.</summary>
public sealed record NativeNormalBackgroundScrollCenter(int Version, int FieldId, int CameraIndex, long Frame, short X, short Y);

public sealed partial class EventEngine
{
    public Func<(short X, short Y)?>? ResolveNativeNormalBackgroundScrollCenter { get; set; }

    private void StepNativeNormalBackgroundScrollCenter()
    {
        if (Scene.ActorBackgroundScroll is not null || Scene.BackgroundScrollPositions.ContainsKey(Scene.CameraIndex))
        {
            Scene.NormalBackgroundScrollCenter = null;
            return;
        }
        (short X, short Y)? center = ResolveNativeNormalBackgroundScrollCenter?.Invoke();
        Scene.NormalBackgroundScrollCenter = center is { } current ? new NativeNormalBackgroundScrollCenter(1, FieldId, Scene.CameraIndex, Frame, current.X, current.Y) : null;
    }

    private static void ValidateNativeNormalBackgroundScrollCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.NormalBackgroundScrollCenter is not NativeNormalBackgroundScrollCenter normal)
        {
            return;
        }
        if (normal.Version != 1 || normal.CameraIndex is < 0 or > 1 || normal.FieldId != saved.FieldId || normal.CameraIndex != saved.Scene.CameraIndex || normal.Frame <= 0 || normal.Frame > saved.Frame || saved.Scene.FieldCallbackClock is not { Version: 1 } || normal.X is < -4095 or > 4096 || normal.Y is < -4095 or > 4096 || saved.Scene.ActorBackgroundScroll is not null || saved.Scene.BackgroundScrollPositions.ContainsKey(saved.Scene.CameraIndex) || saved.Scene.NativeFieldInitialization is not { Stage: NativeFieldInitializationStage.Completed })
        {
            throw new InvalidDataException("Saved normal background-scroll center has no authentic callback, field allocation or exclusive selected-layer authority.");
        }
    }
}
