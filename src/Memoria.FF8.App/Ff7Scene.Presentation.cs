using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private long previousActorGeneration = -1;
    private IReadOnlyDictionary<int, Ff7Actor> previousActors = new Dictionary<int, Ff7Actor>();
    private Ff7FieldProcess? previousFieldPresentation;
    public bool InterpolatePresentation { get; set; } = true;

    private void CapturePreviousActors()
    {
        previousActorGeneration = Session!.FieldGeneration;
        previousFieldPresentation = Session.Host.Process;
        previousActors = previousFieldPresentation.Actors.ToDictionary(actor => actor.Slot);
    }

    private void ResetInterpolationPresentation()
    {
        previousActorGeneration = -1;
        previousActors = new Dictionary<int, Ff7Actor>();
        previousFieldPresentation = null;
    }

    private bool CanInterpolateFieldPresentation => InterpolatePresentation && PresentationPreferences.FieldFps == 60 && !focusPaused && !userPaused && !debugMenu && error is null && Session is not null && previousActorGeneration == Session.FieldGeneration;
    private float FieldPresentationFraction => (float)Math.Clamp(tickRemainder * 30, 0, 1);

    private Vector2 PresentationCameraOffset(Ff7FieldProcess current, PcFieldCamera? movieCamera = null)
    {
        return CanInterpolateFieldPresentation && previousFieldPresentation is { } previous
            ? Ff7FieldPresentation.InterpolateCameraOffset(previous, current, FieldPresentationFraction, movieCamera)
            : FieldCameraOffset(current, movieCamera);
    }

    private Ff7Actor PresentationActor(Ff7Actor actor)
    {
        if (!actor.Visible)
        {
            return actor;
        }
        Ff7Actor current = Session!.PrepareActorPresentation(actor);
        if (!CanInterpolateFieldPresentation || !previousActors.TryGetValue(actor.Slot, out Ff7Actor? previous))
        {
            return current;
        }
        int frames = Session.Models[actor.Model].Animations[Path.ChangeExtension(Session.Field.Models.Models[actor.Model].Animations[actor.Animation], ".a")].Frames.Count;
        Ff7Actor before = previous.Visible && previous.Model == actor.Model ? Session.PrepareActorPresentation(previous) : previous;
        return Ff7ActorPresentation.Interpolate(before, current, FieldPresentationFraction, frames);
    }
}

internal static class Ff7FieldPresentation
{
    public static Vector2 InterpolateCameraOffset(Ff7FieldProcess previous, Ff7FieldProcess current, float fraction, PcFieldCamera? movieCamera = null)
    {
        Vector2 endpoint = Ff7NativeCamera.PresentationOffset(current, movieCamera);
        if (previous.Movie is { Playing: true } || current.Movie is { Playing: true })
        {
            return endpoint;
        }
        bool movingCamera = current.CameraFollowPlayer != false || current.CameraTracking is { Completed: false } || previous.CameraTracking is { Completed: false } || current.CameraScroll is { Completed: false } || previous.CameraScroll is { Completed: false };
        if (!movingCamera && (previous.CameraX != current.CameraX || previous.CameraY != current.CameraY))
        {
            return endpoint;
        }
        if (!float.IsFinite(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        return Vector2.Lerp(Ff7NativeCamera.PresentationOffset(previous), endpoint, Math.Clamp(fraction, 0, 1));
    }
}

internal static class Ff7ActorPresentation
{
    public static Ff7Actor Interpolate(Ff7Actor previous, Ff7Actor current, float fraction, int animationFrames)
    {
        if (previous.Model != current.Model || previous.Visible != current.Visible || previous.Entity != current.Entity || previous.Slot != current.Slot)
        {
            return current;
        }
        if (Math.Abs((long)current.X4096 - previous.X4096) > 32767L * 4096 || Math.Abs((long)current.Y4096 - previous.Y4096) > 32767L * 4096 || Math.Abs((long)current.Z4096 - previous.Z4096) > 32767L * 4096)
        {
            return current;
        }
        fraction = Math.Clamp(fraction, 0, 1);
        int Lerp(int before, int after) => checked((int)Math.Round(before + ((double)after - before) * fraction));
        int phase = current.AnimationFrame16;
        if (previous.Animation == current.Animation && previous.FirstAnimationFrame == current.FirstAnimationFrame && previous.LastAnimationFrame == current.LastAnimationFrame && previous.AnimationLoop == current.AnimationLoop)
        {
            int first = current.FirstAnimationFrame * 16;
            int end = Math.Min(animationFrames, current.LastAnimationFrame < 0 ? animationFrames : current.LastAnimationFrame + 1) * 16;
            if (current.AnimationLoop && end > first && phase < previous.AnimationFrame16)
            {
                phase += end - first;
            }
            phase = Lerp(previous.AnimationFrame16, phase);
            if (current.AnimationLoop && end > first && phase >= end)
            {
                phase = first + (phase - first) % (end - first);
            }
        }
        int direction = (previous.Direction + (int)Math.Round(Math.IEEERemainder(current.Direction - previous.Direction, 256) * fraction)) & 255;
        return current with
        {
            X4096 = Lerp(previous.X4096, current.X4096),
            Y4096 = Lerp(previous.Y4096, current.Y4096),
            Z4096 = Lerp(previous.Z4096, current.Z4096),
            OffsetX = Lerp(previous.OffsetX, current.OffsetX),
            OffsetY = Lerp(previous.OffsetY, current.OffsetY),
            OffsetZ = Lerp(previous.OffsetZ, current.OffsetZ),
            Direction = direction,
            AnimationFrame16 = phase
        };
    }
}
