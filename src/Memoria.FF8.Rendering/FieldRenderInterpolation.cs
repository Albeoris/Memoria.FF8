using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed record FieldBackgroundSample(IReadOnlyDictionary<int, Vector2> LayerOffsets, Vector2 ActorOffset, FieldNativeBackgroundWrapFrame? NativeWrap);
public sealed record FieldBackgroundBlend(FieldBackgroundSample Previous, FieldBackgroundSample Current, float Fraction);

public sealed partial class FieldRenderer
{
    private FieldBackgroundBlend? presentationBackgroundBlend;

    private static bool SamePresentationBlend(FieldBackgroundBlend? first, FieldBackgroundBlend? second)
    {
        return first is null || second is null ? first is null && second is null : first.Fraction == second.Fraction && SameWrapFrame(first.Previous.NativeWrap, second.Previous.NativeWrap) && SameWrapFrame(first.Current.NativeWrap, second.Current.NativeWrap);
    }

    private static Vector3[] ActorPose(FieldModel model, ActorVisual actor)
    {
        return actor.AnimationBlend is FieldAnimationBlend blend ? model.PoseInterpolated(actor.AnimationId, blend.PreviousFrame, blend.CurrentFrame, blend.Fraction, actor.FirstControlRotation, actor.RootTranslationZ, actor.SkinTranslations, blend.FirstFrame, blend.TerminalFrame, blend.Loop) : model.Pose(actor.AnimationId, actor.AnimationFrame, actor.FirstControlRotation, actor.RootTranslationZ, actor.SkinTranslations);
    }

    private void ValidateBackgroundBlend(FieldBackgroundBlend? blend, int cameraIndex)
    {
        if (blend is null)
        {
            return;
        }
        if (!float.IsFinite(blend.Fraction) || blend.Fraction < 0 || blend.Fraction > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(blend));
        }
        ValidateNativeWrapFrame(blend.Previous.NativeWrap, cameraIndex, blend.Previous.ActorOffset, blend.Previous.LayerOffsets);
        ValidateNativeWrapFrame(blend.Current.NativeWrap, cameraIndex, blend.Current.ActorOffset, blend.Current.LayerOffsets);
        FieldNativeBackgroundWrapFrame? previous = blend.Previous.NativeWrap;
        FieldNativeBackgroundWrapFrame? current = blend.Current.NativeWrap;
        if ((previous is null) != (current is null) || previous is not null && current is not null && (previous.NativeNormalVersion != current.NativeNormalVersion || !previous.Layers.Select(layer => (layer.Layer, layer.PeriodX, layer.PeriodY)).SequenceEqual(current.Layers.Select(layer => (layer.Layer, layer.PeriodX, layer.PeriodY)))))
        {
            throw new NotSupportedException("Background presentation samples require the same wrapping layers and periods.");
        }
    }

    private static void ApplyBackgroundBlend(FieldBackgroundBlend? blend, ref IReadOnlyDictionary<int, Vector2> offsets, ref Vector2 actorOffset, ref FieldNativeBackgroundWrapFrame? frame, Vector2 jitter)
    {
        if (blend is null)
        {
            return;
        }
        FieldBackgroundSample sample = blend.Fraction == 0 ? blend.Previous : blend.Current;
        actorOffset = blend.Fraction is > 0 and < 1 ? Vector2.Lerp(blend.Previous.ActorOffset, blend.Current.ActorOffset, blend.Fraction) : sample.ActorOffset;
        frame = sample.NativeWrap;
        offsets = offsets.Keys.Union(blend.Previous.LayerOffsets.Keys).Union(blend.Current.LayerOffsets.Keys).ToDictionary(layer => layer, layer => (blend.Fraction is > 0 and < 1 ? Vector2.Lerp(blend.Previous.LayerOffsets.GetValueOrDefault(layer), blend.Current.LayerOffsets.GetValueOrDefault(layer), blend.Fraction) : sample.LayerOffsets.GetValueOrDefault(layer)) + jitter);
    }

    private bool TryPresentationPlacement(BackgroundTile tile, out Vector2 origin)
    {
        origin = default;
        if (presentationBackgroundBlend is not FieldBackgroundBlend blend || blend.Current.NativeWrap is not FieldNativeBackgroundWrapFrame current || blend.Previous.NativeWrap is not FieldNativeBackgroundWrapFrame previous)
        {
            return false;
        }
        FieldNativeBackgroundWrapLayer? currentLayer = current.Layers.Where(layer => layer.Layer == tile.Layer).Select(layer => (FieldNativeBackgroundWrapLayer?)layer).FirstOrDefault();
        if (currentLayer is not FieldNativeBackgroundWrapLayer next)
        {
            return false;
        }
        FieldNativeBackgroundWrapLayer last = previous.Layers.Single(layer => layer.Layer == tile.Layer);
        float x = InterpolateWrappedCoordinate(checked((short)tile.X), last.OffsetX, next.OffsetX, next.PeriodX, blend.Fraction);
        float y = InterpolateWrappedCoordinate(checked((short)tile.Y), last.OffsetY, next.OffsetY, next.PeriodY, blend.Fraction);
        float left = float.Lerp(previous.Left, current.Left, blend.Fraction);
        float right = float.Lerp(previous.Right, current.Right, blend.Fraction);
        float top = float.Lerp(previous.Top, current.Top, blend.Fraction);
        float bottom = float.Lerp(previous.Bottom, current.Bottom, blend.Fraction);
        x = RelocatePresentationCoordinate(x, left, right, next.PeriodX);
        y = RelocatePresentationCoordinate(y, top, bottom, next.PeriodY);
        origin = new Vector2(x, y);
        return true;
    }

    private static float InterpolateWrappedCoordinate(short coordinate, short previous, short current, int period, float fraction)
    {
        float difference = current - previous;
        if (period > 0)
        {
            difference -= MathF.Round(difference / period) * period;
        }
        return unchecked((short)(coordinate + previous)) + difference * fraction;
    }

    private static float RelocatePresentationCoordinate(float coordinate, float minimum, float maximum, int period)
    {
        if (period != 0 && coordinate < minimum)
        {
            coordinate += period;
        }
        else if (period != 0 && coordinate > maximum)
        {
            coordinate -= period;
        }
        return coordinate;
    }
}
