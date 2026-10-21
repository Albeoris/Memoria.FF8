using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed record FieldBackgroundSample(IReadOnlyDictionary<int, Vector2> LayerOffsets, Vector2 ActorOffset, FieldNativeBackgroundWrapFrame? NativeWrap, IReadOnlyDictionary<int, short>? LayerScales = null);
public sealed record FieldBackgroundBlend(FieldBackgroundSample Previous, FieldBackgroundSample Current, float Fraction);

public sealed partial class FieldRenderer
{
    private FieldBackgroundBlend? presentationBackgroundBlend;
    private FieldBackgroundSample? viewportPrevious;
    private FieldBackgroundSample? viewportCurrent;
    private float viewportFraction;
    private float viewportCameraShift;
    private IReadOnlyDictionary<int, float> viewportLayerCorrections = new Dictionary<int, float>();

    private float CameraPresentationShift(int cameraIndex, float scroll)
    {
        if (!widescreen || OutputWidth <= Width * scale || field.Information is not { ScreenBounds.Count: > 0 } information || (uint)cameraIndex >= information.CameraBounds.Count)
        {
            return 0;
        }
        FieldBounds bounds = information.CameraBounds[cameraIndex];
        float imageWidth = (bounds.Right - bounds.Left) * scale;
        if (imageWidth <= 0)
        {
            return 0;
        }
        float imageLeft = offsetX + (drawingOrigin.X + bounds.Left + scroll) * scale;
        float left = imageWidth >= OutputWidth ? Math.Clamp(imageLeft, OutputWidth - imageWidth, 0) : (OutputWidth - imageWidth) / 2;
        return (left - imageLeft) / scale;
    }

    private static float ScaledCamera(float center, short ratio)
    {
        // Preserve native signed integer division at whole centers and blend only the presentation fraction.
        float low = MathF.Floor(center);
        float high = MathF.Ceiling(center);
        return float.Lerp((int)low * ratio / 256, (int)high * ratio / 256, center - low);
    }

    private void PrepareViewportSamples(FieldBackgroundSample latest, FieldBackgroundBlend? blend, int cameraIndex)
    {
        viewportPrevious = blend?.Previous ?? latest;
        viewportCurrent = blend?.Current ?? latest;
        viewportFraction = blend?.Fraction ?? 1;
        float previousShift = CameraPresentationShift(cameraIndex, viewportPrevious.ActorOffset.X);
        float currentShift = CameraPresentationShift(cameraIndex, viewportCurrent.ActorOffset.X);
        viewportCameraShift = float.Lerp(previousShift, currentShift, viewportFraction);
        // The shared origin already contributes the camera shift. Replace only each layer's camera term; scripted offsets and current words remain in its supplied offset.
        viewportLayerCorrections = field.Background.Select(tile => tile.Layer).Distinct().ToDictionary(layer => layer, layer => float.Lerp(Correction(viewportPrevious, previousShift, layer), Correction(viewportCurrent, currentShift, layer), viewportFraction) - viewportCameraShift);

        static float Correction(FieldBackgroundSample sample, float shift, int layer)
        {
            short ratio = sample.LayerScales?.GetValueOrDefault(layer, (short)256) ?? (short)256;
            if (sample.LayerScales is null && sample.NativeWrap is { LayerCurrents.IsDefaultOrEmpty: false } frame)
            {
                foreach (FieldNativeBackgroundLayerCurrent current in frame.LayerCurrents)
                {
                    if (current.Layer == layer)
                    {
                        ratio = current.ScaleX;
                        break;
                    }
                }
            }
            float center = -sample.ActorOffset.X;
            return ScaledCamera(center, ratio) - ScaledCamera(center - shift, ratio);
        }
    }

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
        if (viewportCurrent?.NativeWrap is not FieldNativeBackgroundWrapFrame current || viewportPrevious?.NativeWrap is not FieldNativeBackgroundWrapFrame previous || presentationBackgroundBlend is null && viewportCameraShift == 0 && viewportLayerCorrections.GetValueOrDefault(tile.Layer) == 0)
        {
            return false;
        }
        FieldNativeBackgroundWrapLayer? currentLayer = current.Layers.Where(layer => layer.Layer == tile.Layer).Select(layer => (FieldNativeBackgroundWrapLayer?)layer).FirstOrDefault();
        if (currentLayer is not FieldNativeBackgroundWrapLayer next)
        {
            return false;
        }
        FieldNativeBackgroundWrapLayer last = previous.Layers.Single(layer => layer.Layer == tile.Layer);
        float x = InterpolateWrappedCoordinate(checked((short)tile.X), last.OffsetX, next.OffsetX, next.PeriodX, viewportFraction);
        float y = InterpolateWrappedCoordinate(checked((short)tile.Y), last.OffsetY, next.OffsetY, next.PeriodY, viewportFraction);
        float left = float.Lerp(previous.Left, current.Left, viewportFraction);
        float right = float.Lerp(previous.Right, current.Right, viewportFraction);
        float top = float.Lerp(previous.Top, current.Top, viewportFraction);
        float bottom = float.Lerp(previous.Bottom, current.Bottom, viewportFraction);
        // Moving the cull window inversely is equivalent to moving the raw tile before relocation.
        // The shared origin also moves the central clip, so only the layer residual belongs here.
        float shift = viewportLayerCorrections.GetValueOrDefault(tile.Layer);
        if ((viewportCameraShift != 0 || shift != 0) && next.PeriodX > 0)
        {
            // A changed presentation camera can place the authenticated raw word more than
            // one period from this strip. Choose its equivalent copy without changing that word.
            if (x < left - shift)
            {
                x += MathF.Ceiling((left - shift - x) / next.PeriodX) * next.PeriodX;
            }
            else if (x > right - shift)
            {
                x -= MathF.Ceiling((x - right + shift) / next.PeriodX) * next.PeriodX;
            }
        }
        else
        {
            x = RelocatePresentationCoordinate(x, left - shift, right - shift, next.PeriodX);
        }
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
