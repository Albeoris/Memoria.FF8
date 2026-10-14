using System.Collections.Immutable;
using System.Numerics;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Native475480 default normal-mode wrapping with a separately projected camera0 center.</summary>
public static class FieldNativeNormalBackgroundWrap
{
    public static bool SupportsMetadata(FieldAssets field, int cameraIndex)
    {
        if (field.Id < 72 || field.NativeExecutableHash != NativeFieldRuntimeProfile.FF8EnExecutableHash || cameraIndex != 0 || field.Cameras.Count != 1 || field.Camera != field.Cameras[0] || field.Camera.Pan != Vector2.Zero || field.Information is not FieldInformation information || information.CameraBounds.Count != 8 || information.ScreenBounds.Count == 0 || information.ScreenBounds[0] != new FieldBounds(0, 224, 320, 0) || field.Background.Count == 0 || field.Background.Any(tile => tile.Layer is < 0 or > 7 || tile.X is < short.MinValue or > short.MaxValue || tile.Y is < short.MinValue or > short.MaxValue))
        {
            return false;
        }
        FieldBounds selected = information.CameraBounds[0];
        if (selected.Left + 160 > selected.Right - 160 || selected.Top + 112 > selected.Bottom - 112)
        {
            return false;
        }
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct())
        {
            if ((information.BackgroundWrapMask & (1 << layer)) == 0)
            {
                continue;
            }
            FieldBounds bounds = information.CameraBounds[layer];
            if (bounds.Right is <= 0 or > short.MaxValue / 2 || bounds.Bottom is <= 0 or > short.MaxValue / 2)
            {
                return false;
            }
        }
        return true;
    }

    public static bool SupportsLayerCurrentMetadata(FieldAssets field, int cameraIndex)
    {
        if (!SupportsMetadata(field, cameraIndex))
        {
            return false;
        }
        FieldBounds bounds = field.Information!.CameraBounds[0];
        // Every clamped center's strict origin window fits native eighth
        // packing. WORD addition/one-step relocation/cull then proves every
        // intermediate current value, without replaying each curve/resource.
        return bounds.Left + 160 - 192 >= -4096 && bounds.Right - 160 + 176 <= 4095 && bounds.Top + 112 - 144 >= -4096 && bounds.Bottom - 112 + 128 <= 4095;
    }

    public static FieldNativeBackgroundWrapFrame? Prepare(FieldAssets field, int cameraIndex, FieldNativeCameraCenter center, ImmutableArray<FieldNativeBackgroundLayerCurrent> currents = default)
    {
        if (!SupportsMetadata(field, cameraIndex) || center.X is < -4096 or > 4095 || center.Y is < -4096 or > 4095)
        {
            return null;
        }
        bool hasCurrents = !currents.IsDefaultOrEmpty;
        if (hasCurrents && (!SupportsLayerCurrentMetadata(field, cameraIndex) || currents.Select(current => current.Layer).Distinct().Count() != currents.Length || currents.Any(current => current.Layer is < 1 or > 7 || current.X is < -4095 or > 4096 || current.Y is < -4095 or > 4096 || !field.Background.Any(tile => tile.Layer == current.Layer))))
        {
            return null;
        }
        FieldInformation information = field.Information!;
        FieldBounds selected = information.CameraBounds[0];
        if (center.X < selected.Left + 160 || center.X > selected.Right - 160 || center.Y < selected.Top + 112 || center.Y > selected.Bottom - 112)
        {
            return null;
        }
        ImmutableArray<FieldNativeBackgroundWrapLayer>.Builder layers = ImmutableArray.CreateBuilder<FieldNativeBackgroundWrapLayer>();
        foreach (int layer in field.Background.Select(tile => tile.Layer).Distinct().Order())
        {
            FieldNativeBackgroundLayerCurrent? current = hasCurrents ? currents.Where(current => current.Layer == layer).Select(current => (FieldNativeBackgroundLayerCurrent?)current).FirstOrDefault() : null;
            bool enabled = (information.BackgroundWrapMask & (1 << layer)) != 0;
            if (enabled || current is not null)
            {
                int offsetX = 0;
                int offsetY = 0;
                if (current is FieldNativeBackgroundLayerCurrent state && (!NativeBackgroundLayerPlacement.TryTranslation(center.X, state.OffsetX, state.ScaleX, state.X, out offsetX) || !NativeBackgroundLayerPlacement.TryTranslation(center.Y, state.OffsetY, state.ScaleY, state.Y, out offsetY)))
                {
                    return null;
                }
                FieldBounds bounds = information.CameraBounds[layer];
                layers.Add(new FieldNativeBackgroundWrapLayer((byte)layer, (short)offsetX, (short)offsetY, enabled ? checked((short)(2 * bounds.Right)) : (short)0, enabled ? checked((short)(2 * bounds.Bottom)) : (short)0));
            }
        }
        FieldNativeBackgroundWrapFrame frame = new(field.Id, Convert.ToHexString(SHA256.HashData(field.Jsm)), checked((short)(center.X - 192)), checked((short)(center.X + 176)), checked((short)(center.Y - 144)), checked((short)(center.Y + 128)), layers.ToImmutable(), hasCurrents ? 2 : 1, hasCurrents ? currents : ImmutableArray<FieldNativeBackgroundLayerCurrent>.Empty);
        foreach (BackgroundTile tile in field.Background)
        {
            FieldNativeBackgroundWrapLayer? wrapping = frame.Layers.Where(layer => layer.Layer == tile.Layer).Select(layer => (FieldNativeBackgroundWrapLayer?)layer).FirstOrDefault();
            if (wrapping is FieldNativeBackgroundWrapLayer layer && FieldNativeBackgroundTileProbe.Relocate((short)tile.X, (short)tile.Y, layer, frame) is FieldNativeBackgroundTileOrigin origin && (origin.X is < -4096 or > 4095 || origin.Y is < -4096 or > 4095))
            {
                return null;
            }
        }
        return frame;
    }

    public static bool Validate(FieldAssets field, int cameraIndex, Vector2 actorOffset, IReadOnlyDictionary<int, Vector2> offsets, FieldNativeBackgroundWrapFrame frame)
    {
        if (frame.NativeNormalVersion is not (1 or 2) || frame.NativeNormalVersion == 1 && !frame.LayerCurrents.IsDefaultOrEmpty || frame.NativeNormalVersion == 2 && frame.LayerCurrents.IsDefaultOrEmpty || !float.IsFinite(actorOffset.X) || !float.IsFinite(actorOffset.Y) || actorOffset.X != MathF.Truncate(actorOffset.X) || actorOffset.Y != MathF.Truncate(actorOffset.Y) || actorOffset.X is < -4095 or > 4096 || actorOffset.Y is < -4095 or > 4096)
        {
            return false;
        }
        FieldNativeBackgroundWrapFrame? expected = Prepare(field, cameraIndex, new FieldNativeCameraCenter((int)-actorOffset.X, (int)-actorOffset.Y), frame.LayerCurrents);
        return expected is not null && frame.FieldId == expected.FieldId && frame.ProgramHash == expected.ProgramHash && frame.Left == expected.Left && frame.Right == expected.Right && frame.Top == expected.Top && frame.Bottom == expected.Bottom && !frame.Layers.IsDefault && frame.Layers.SequenceEqual(expected.Layers) && field.Background.Select(tile => tile.Layer).Distinct().All(layer => offsets.GetValueOrDefault(layer) == actorOffset) && offsets.Keys.All(layer => field.Background.Any(tile => tile.Layer == layer));
    }
}
