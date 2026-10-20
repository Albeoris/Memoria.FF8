using System.Numerics;
using System.Runtime.ExceptionServices;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal sealed record FieldActorSample(object Owner, long PositionRevision, long AnimationRevision, ActorVisual Visual, int FirstFrame, int TerminalFrame, bool Loop, int AnimationStep);
internal sealed record FieldPresentationFrame(object Owner, long Frame, int CameraIndex, IReadOnlyList<FieldActorSample> Actors, BackgroundScrollFrame Scroll, long CameraRevision = 0, int? CameraActorId = null);
internal sealed record FieldPresentationSample(IReadOnlyList<ActorVisual> Actors, FieldBackgroundBlend? Background);

/// <summary>Render-only history of completed field callbacks. Drawing never advances the VM or writes actors.</summary>
internal sealed class FieldPresentation
{
    private FieldPresentationFrame? previous;
    private FieldPresentationFrame? current;
    private ExceptionDispatchInfo? preparationError;
    private bool active;

    public void Reset()
    {
        previous = null;
        current = null;
        preparationError = null;
    }

    public void Commit(object owner, long frame, Func<FieldPresentationFrame> prepare)
    {
        if (!active)
        {
            return;
        }
        previous = current is not null && ReferenceEquals(owner, current.Owner) && frame == current.Frame + 1 ? current : null;
        current = null;
        preparationError = null;
        try
        {
            current = prepare();
            if (previous is not null && (previous.CameraIndex != current.CameraIndex || previous.CameraRevision != current.CameraRevision || previous.CameraActorId != current.CameraActorId || !SameBackgroundOwner(previous.Scroll.NativeWrap, current.Scroll.NativeWrap) || HasPlacementChange(previous, current)))
            {
                previous = null;
            }
        }
        catch (Exception exception)
        {
            previous = null;
            preparationError = ExceptionDispatchInfo.Capture(exception);
        }
    }

    public FieldPresentationSample Sample(FieldPresentationFrame latest, float fraction, bool smoothAnimation)
    {
        if (!float.IsFinite(fraction) || fraction is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction));
        }
        active = true;
        preparationError?.Throw();
        if (current is null || !SameSnapshot(current, latest))
        {
            previous = null;
        }
        current = latest;
        FieldPresentationFrame? before = fraction < 1 ? previous : null;
        Dictionary<int, FieldActorSample> oldActors = before?.Actors.ToDictionary(actor => actor.Visual.ObjectId) ?? [];
        ActorVisual[] actors = latest.Actors.Select(actor => Interpolate(oldActors.GetValueOrDefault(actor.Visual.ObjectId), actor, fraction, smoothAnimation)).ToArray();
        FieldBackgroundBlend? background = before is null ? null : new(ToBackground(before.Scroll), ToBackground(latest.Scroll), fraction);
        return new FieldPresentationSample(actors, background);
    }

    private static FieldBackgroundSample ToBackground(BackgroundScrollFrame frame) => new(frame.LayerOffsets, frame.ActorOffset, frame.NativeWrap);

    private static bool HasPlacementChange(FieldPresentationFrame before, FieldPresentationFrame after)
    {
        Dictionary<int, FieldActorSample> actors = before.Actors.ToDictionary(actor => actor.Visual.ObjectId);
        return after.Actors.Any(actor => actor.Visual.ObjectId == after.CameraActorId && (!actors.TryGetValue(actor.Visual.ObjectId, out FieldActorSample? old) || !ReferenceEquals(old.Owner, actor.Owner) || old.PositionRevision != actor.PositionRevision));
    }

    private static bool SameSnapshot(FieldPresentationFrame first, FieldPresentationFrame second)
    {
        if (!ReferenceEquals(first.Owner, second.Owner) || first.Frame != second.Frame || first.CameraIndex != second.CameraIndex || first.CameraRevision != second.CameraRevision || first.CameraActorId != second.CameraActorId || first.Actors.Count != second.Actors.Count || first.Scroll.ActorOffset != second.Scroll.ActorOffset || !SameDictionary(first.Scroll.LayerOffsets, second.Scroll.LayerOffsets) || !SameWrap(first.Scroll.NativeWrap, second.Scroll.NativeWrap))
        {
            return false;
        }
        for (int index = 0; index < first.Actors.Count; index++)
        {
            FieldActorSample a = first.Actors[index];
            FieldActorSample b = second.Actors[index];
            if (!ReferenceEquals(a.Owner, b.Owner) || a.PositionRevision != b.PositionRevision || a.AnimationRevision != b.AnimationRevision || a.FirstFrame != b.FirstFrame || a.TerminalFrame != b.TerminalFrame || a.Loop != b.Loop || a.AnimationStep != b.AnimationStep || a.Visual with { SkinTranslations = null, RegisteredTextures = null, GroundShadowRadii601To608 = null } != b.Visual with { SkinTranslations = null, RegisteredTextures = null, GroundShadowRadii601To608 = null } || !SameDictionary(a.Visual.SkinTranslations, b.Visual.SkinTranslations))
            {
                return false;
            }
        }
        return true;
    }

    private static bool SameBackgroundOwner(FieldNativeBackgroundWrapFrame? first, FieldNativeBackgroundWrapFrame? second)
    {
        return first is null || second is null ? first is null && second is null : first.FieldId == second.FieldId && first.ProgramHash == second.ProgramHash && first.NativeNormalVersion == second.NativeNormalVersion && first.Layers.Select(layer => (layer.Layer, layer.PeriodX, layer.PeriodY)).SequenceEqual(second.Layers.Select(layer => (layer.Layer, layer.PeriodX, layer.PeriodY)));
    }

    private static bool SameWrap(FieldNativeBackgroundWrapFrame? first, FieldNativeBackgroundWrapFrame? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }
        return SameBackgroundOwner(first, second) && first.Left == second.Left && first.Right == second.Right && first.Top == second.Top && first.Bottom == second.Bottom && first.Layers.SequenceEqual(second.Layers) && first.LayerCurrents.IsDefault == second.LayerCurrents.IsDefault && (first.LayerCurrents.IsDefault || first.LayerCurrents.SequenceEqual(second.LayerCurrents));
    }

    private static bool SameDictionary<T>(IReadOnlyDictionary<int, T>? first, IReadOnlyDictionary<int, T>? second)
    {
        return first is null || second is null ? first is null && second is null : first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out T? value) && EqualityComparer<T>.Default.Equals(pair.Value, value));
    }

    private static ActorVisual Interpolate(FieldActorSample? previous, FieldActorSample current, float fraction, bool smoothAnimation)
    {
        ActorVisual after = current.Visual;
        bool compatible = previous is not null && ReferenceEquals(previous.Owner, current.Owner) && previous.PositionRevision == current.PositionRevision && previous.Visual.ModelId == after.ModelId && previous.Visual.Visible == after.Visible;
        ActorVisual before = compatible ? previous!.Visual : after;
        bool sameAnimation = compatible && previous!.AnimationRevision == current.AnimationRevision && before.AnimationId == after.AnimationId && before.AdditionalControlDisabled == after.AdditionalControlDisabled && previous.FirstFrame == current.FirstFrame && previous.TerminalFrame == current.TerminalFrame && previous.Loop == current.Loop;
        float previousPhase = sameAnimation ? before.AnimationFrame : after.AnimationFrame;
        float currentPhase = after.AnimationFrame;
        int length = current.TerminalFrame - current.FirstFrame;
        if (sameAnimation && current.Loop && length > 0)
        {
            if (current.AnimationStep >= 0 && currentPhase < previousPhase)
            {
                currentPhase += length;
            }
            else if (current.AnimationStep < 0 && currentPhase > previousPhase)
            {
                currentPhase -= length;
            }
        }
        FieldAnimationBlend? animation = smoothAnimation ? new(previousPhase, currentPhase, sameAnimation ? fraction : 1, current.FirstFrame, current.TerminalFrame, current.Loop) : null;
        return after with
        {
            X = Lerp(before.X, after.X, fraction),
            Y = Lerp(before.Y, after.Y, fraction),
            Z = Lerp(before.Z, after.Z, fraction),
            Direction = before.Direction + MathF.IEEERemainder(after.Direction - before.Direction, 256) * fraction,
            RootTranslationZ = Lerp(before.RootTranslationZ, after.RootTranslationZ, fraction),
            GroundShadowCentre = Vector(before.GroundShadowCentre, after.GroundShadowCentre, fraction),
            FirstControlRotation = sameAnimation ? Rotation(before.FirstControlRotation, after.FirstControlRotation, fraction) : after.FirstControlRotation,
            SkinTranslations = sameAnimation ? Translations(before.SkinTranslations, after.SkinTranslations, fraction) : after.SkinTranslations,
            AnimationBlend = animation
        };
    }

    private static float Lerp(float first, float second, float fraction) => first + (second - first) * fraction;

    private static Vector3? Vector(Vector3? first, Vector3? second, float fraction) => first is Vector3 a && second is Vector3 b ? Vector3.Lerp(a, b, fraction) : second;

    private static Vector3? Rotation(Vector3? first, Vector3? second, float fraction)
    {
        if (first is not Vector3 a || second is not Vector3 b)
        {
            return second;
        }
        return a + new Vector3(MathF.IEEERemainder(b.X - a.X, MathF.Tau), MathF.IEEERemainder(b.Y - a.Y, MathF.Tau), MathF.IEEERemainder(b.Z - a.Z, MathF.Tau)) * fraction;
    }

    private static IReadOnlyDictionary<int, Vector3>? Translations(IReadOnlyDictionary<int, Vector3>? first, IReadOnlyDictionary<int, Vector3>? second, float fraction)
    {
        return first is not null && second is not null && first.Count == second.Count && second.Keys.All(first.ContainsKey) ? second.ToDictionary(pair => pair.Key, pair => Vector3.Lerp(first[pair.Key], pair.Value, fraction)) : second;
    }
}
