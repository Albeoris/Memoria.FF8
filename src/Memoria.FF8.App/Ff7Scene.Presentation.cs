using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private readonly CommittedPresentationHistory<(Ff7FieldHost Owner, long Tick), Ff7FieldPresentationFrame> fieldHistory = new(
        (first, second) => ReferenceEquals(first.Owner, second.Owner) && first.Tick == second.Tick,
        (first, second) => ReferenceEquals(first.Owner, second.Owner) && second.Tick == first.Tick + 1,
        (first, second) => first.State.Movie is not { Playing: true } && second.State.Movie is not { Playing: true });
    public bool InterpolatePresentation { get; set; } = true;

    private void CommitScenePresentation()
    {
        Ff7Session session = Session!;
        if (session.Battle is not null)
        {
            ResetInterpolationPresentation();
            worldHistory.Reset();
        }
        else if (session.Condor is not null || session.Submarine is not null)
        {
            ResetInterpolationPresentation();
            worldHistory.Reset();
        }
        else if (session.World is { } world)
        {
            ResetInterpolationPresentation();
            CommitWorldPresentation(world);
        }
        else if (session.ChocoboRace is { } race)
        {
            fieldHistory.Reset();
            waterAppearanceEffects.Clear();
            worldHistory.Reset();
            chocoboHistory.Commit((race, race.Tick), race.Capture);
        }
        else
        {
            chocoboHistory.Reset();
            worldHistory.Reset();
            Ff7FieldProcess state = session.Host.Process;
            fieldHistory.Commit((session.Host, state.Tick), () => PrepareFieldPresentation(session, state));
        }
    }

    private void ResetInterpolationPresentation()
    {
        fieldHistory.Reset();
        chocoboHistory.Reset();
        waterAppearanceEffects.Clear();
    }

    private float PresentationFraction(int fps)
    {
        bool interpolate = InterpolatePresentation && CommittedPresentationTime.ShouldInterpolate(fps, Ff7FieldTiming.CallbacksPerSecond) && !focusPaused && !userPaused && !debugMenu && error is null;
        return interpolate ? CommittedPresentationTime.Fraction(tickRemainder, Ff7FieldTiming.CallbacksPerSecond) : 1;
    }

    private (Ff7Actor[] Actors, Vector2 Camera, Vector2 Shake, IReadOnlyDictionary<int, Ff7ModelRenderOverrides> WaterEffects) SampleFieldPresentation(Ff7Session session, Ff7FieldProcess state, PcFieldCamera? movieCamera)
    {
        float fraction = PresentationFraction(PresentationPreferences.FieldFps);
        var (previous, current) = fieldHistory.Sample((session.Host, state.Tick), fraction, () => PrepareFieldPresentation(session, state), frame => Ff7FieldPresentation.Matches(frame.State, state));
        Vector2 camera = current.Camera ?? FieldCameraOffset(state, movieCamera);
        Vector2 shake = Ff7NativeCamera.PresentationShakeOffset(state);
        if (previous is null)
        {
            return (current.Actors, camera, shake, current.WaterEffects);
        }
        if (previous.Camera is Vector2 before && Ff7FieldPresentation.CanInterpolateCamera(previous.State, current.State))
        {
            camera = Vector2.Lerp(before, camera, fraction);
            shake = Vector2.Lerp(Ff7NativeCamera.PresentationShakeOffset(previous.State), shake, fraction);
        }
        Ff7Actor[] actors = current.Actors.Select(actor => actor.Visible && previous.ActorsBySlot.TryGetValue(actor.Slot, out Ff7Actor? old) ? Ff7ActorPresentation.Interpolate(old, actor, fraction, current.AnimationFrames[actor.Slot]) : actor).ToArray();
        return (actors, camera, shake, current.WaterEffects);
    }

    private Ff7FieldPresentationFrame PrepareFieldPresentation(Ff7Session session, Ff7FieldProcess state)
    {
        Ff7Actor[] actors = state.Actors.Select(actor => actor.Visible ? session.PrepareActorPresentation(actor) : actor).ToArray();
        Dictionary<int, int> animationFrames = [];
        foreach (Ff7Actor actor in actors.Where(actor => actor.Visible))
        {
            string animation = Path.ChangeExtension(session.Field.Models.Models[actor.Model].Animations[actor.Animation], ".a");
            animationFrames.Add(actor.Slot, session.Models[actor.Model].Animations[animation].Frames.Count);
        }
        // Video supplies its own current camera on the draw path and never interpolates with field history.
        Vector2? camera = state.Movie is { Playing: true } ? null : FieldCameraOffset(state);
        return new(state, actors, actors.ToDictionary(actor => actor.Slot), animationFrames, camera, PrepareWaterAppearanceEffects(session, state));
    }
}

internal sealed record Ff7FieldPresentationFrame(Ff7FieldProcess State, Ff7Actor[] Actors, IReadOnlyDictionary<int, Ff7Actor> ActorsBySlot, IReadOnlyDictionary<int, int> AnimationFrames, Vector2? Camera, IReadOnlyDictionary<int, Ff7ModelRenderOverrides> WaterEffects);

internal static class Ff7FieldPresentation
{
    public static Vector2 InterpolateCameraOffset(Ff7FieldProcess previous, Ff7FieldProcess current, float fraction, PcFieldCamera? movieCamera = null)
    {
        Vector2 endpoint = Ff7NativeCamera.PresentationOffset(current, movieCamera);
        if (!CanInterpolateCamera(previous, current))
        {
            return endpoint;
        }
        if (!float.IsFinite(fraction))
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        return Vector2.Lerp(Ff7NativeCamera.PresentationOffset(previous), endpoint, Math.Clamp(fraction, 0, 1));
    }

    internal static bool Matches(Ff7FieldProcess first, Ff7FieldProcess second)
    {
        return ReferenceEquals(first.Actors, second.Actors) && first.CameraEpoch == second.CameraEpoch && first.CameraX == second.CameraX && first.CameraY == second.CameraY && first.CameraShakeX == second.CameraShakeX && first.CameraShakeY == second.CameraShakeY && first.Movie == second.Movie;
    }

    internal static bool CanInterpolateCamera(Ff7FieldProcess previous, Ff7FieldProcess current)
    {
        if (previous.Movie is { Playing: true } || current.Movie is { Playing: true } || previous.CameraEpoch != current.CameraEpoch)
        {
            return false;
        }
        int? target = current.CameraTracking?.Slot ?? (current.CameraFollowPlayer != false ? current.Actors.SingleOrDefault(actor => actor.PlayerControlled)?.Slot : null);
        if (target is int slot && current.Actors.SingleOrDefault(actor => actor.Slot == slot) is { } actor && previous.Actors.SingleOrDefault(actor => actor.Slot == slot)?.PlacementEpoch != actor.PlacementEpoch)
        {
            return false;
        }
        return current.CameraFollowPlayer != false || current.CameraTracking is { Completed: false } || previous.CameraTracking is { Completed: false } || current.CameraScroll is { Completed: false } || previous.CameraScroll is { Completed: false } || previous.CameraX == current.CameraX && previous.CameraY == current.CameraY;
    }
}

internal static class Ff7ActorPresentation
{
    public static Ff7Actor Interpolate(Ff7Actor previous, Ff7Actor current, float fraction, int animationFrames)
    {
        if (previous.Model != current.Model || previous.Visible != current.Visible || previous.Entity != current.Entity || previous.Slot != current.Slot || previous.EntityBound != current.EntityBound || previous.PartyCharacter != current.PartyCharacter || previous.PlacementEpoch != current.PlacementEpoch || previous.AppearanceEpoch != current.AppearanceEpoch)
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
        if (previous.AnimationEpoch == current.AnimationEpoch && previous.Animation == current.Animation && previous.FirstAnimationFrame == current.FirstAnimationFrame && previous.LastAnimationFrame == current.LastAnimationFrame && previous.AnimationLoop == current.AnimationLoop)
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
