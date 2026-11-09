using Memoria.FF7.Runtime;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private long previousActorGeneration = -1;
    private IReadOnlyDictionary<int, Ff7Actor> previousActors = new Dictionary<int, Ff7Actor>();
    public bool InterpolatePresentation { get; set; } = true;

    private void CapturePreviousActors()
    {
        previousActorGeneration = Session!.FieldGeneration;
        previousActors = Session.Host.Process.Actors.ToDictionary(actor => actor.Slot);
    }

    private Ff7Actor PresentationActor(Ff7Actor actor)
    {
        if (!actor.Visible || !InterpolatePresentation || PresentationPreferences.FieldFps != 60 || focusPaused || userPaused || debugMenu || error is not null || previousActorGeneration != Session!.FieldGeneration || !previousActors.TryGetValue(actor.Slot, out Ff7Actor? previous))
        {
            return actor;
        }
        int frames = Session.Models[actor.Model].Animations[Path.ChangeExtension(Session.Field.Models.Models[actor.Model].Animations[actor.Animation], ".a")].Frames.Count;
        return Ff7ActorPresentation.Interpolate(previous, actor, (float)Math.Clamp(tickRemainder * 30, 0, 1), frames);
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
