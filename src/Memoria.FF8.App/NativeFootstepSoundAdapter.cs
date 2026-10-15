using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8;

/// <summary>Original520260, with the retained533CD0 model origin and45FAA0 projection.</summary>
internal static class NativeFootstepSoundAdapter
{
    public static NativeFootstepModelOrigin Capture(FieldCamera camera, int cameraIndex, NativeFootstepProjectionQuery query)
    {
        NativeFootstepModelOrigin Unsupported(string reason)
        {
            return new NativeFootstepModelOrigin(1, query.Frame, query.ModelId, cameraIndex, null, reason);
        }

        if (query.Position is not FieldFixedPosition position || query.Actor.NativeDisplayOffset is not NativeDisplayOffsetState offset)
        {
            return Unsupported("Footstep model-origin capture requires the executed raw actor XYZ and display WORD offsets.");
        }
        if (!TryAxis(camera.AxisX, out FieldNativeCameraPoint axisX) || !TryAxis(camera.AxisZ, out FieldNativeCameraPoint axisZ) || !TryInteger(camera.Translation.X, out int tx) || !TryInteger(camera.Translation.Z, out int tz))
        {
            return Unsupported("Footstep model-origin capture requires original signed-WORD CA axes and exact DWORD translation.");
        }
        //472B30/531120 narrow SAR12 body coordinates plus display offsets to WORD.
        //533CD0/460840 store the unsaturated camera transform in model+68/+76.
        //The zero SVECTOR consumed by520260 therefore does not depend on rotation,
        //animation root translation, skin pose, DRAWENV placement or camera scroll.
        int x = unchecked((short)((position.X >> 12) + offset.X));
        int y = unchecked((short)((position.Y >> 12) + offset.Y));
        int z = unchecked((short)((position.Z >> 12) + offset.Z));
        FieldNativeCameraPoint point = new(x, y, z);
        int transformedX = FieldNativeCameraProbe.Transform(axisX, point, tx);
        int transformedZ = FieldNativeCameraProbe.Transform(axisZ, point, tz);
        return new NativeFootstepModelOrigin(1, query.Frame, query.ModelId, cameraIndex, new NativeFootstepOrigin(transformedX, transformedZ), null);
    }

    public static NativeFootstepSound Resolve(FieldCamera camera, ActorState actor, int mode, Func<FieldSoundEffectTable> readEffects)
    {
        if (mode is not (0 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        NativeFootstepModelOrigin origin = actor.NativeFootstepModelOrigin ?? throw new NotSupportedException("Native footstep sound requires a previously executed model-origin draw sample.");
        if (origin.Version != 1 || origin.Frame < 0 || origin.ModelId < 0 || origin.CameraIndex < 0 || origin.Translation is not NativeFootstepOrigin translation || origin.UnsupportedReason is not null)
        {
            throw new NotSupportedException(origin.UnsupportedReason ?? "Native footstep model-origin receipt is unsupported.");
        }
        //529FF0 precedes camera selection;4789A0 follows it. The caller supplies
        //that phase's CA/CAM focal; the origin remains the last enabled draw.
        if (!float.IsFinite(camera.FocalLength) || camera.FocalLength != MathF.Truncate(camera.FocalLength) || camera.FocalLength is < 1 or > ushort.MaxValue)
        {
            throw new NotSupportedException("Footstep projection requires the original positive focal WORD.");
        }
        (uint pan, uint volume) = Spatialize(translation, (ushort)camera.FocalLength);
        uint flags = 0x400000;
        uint effect;
        if (actor.NativeLadder is not null || actor.NativeLadderMode3 is not null)
        {
            effect = mode == 1 ? 63u : 64u;
        }
        else
        {
            NativeFootstepPairState pair = actor.NativeFootstepPair ?? throw new NotSupportedException("Footstep sound requires allocated or executed BYTE392/393.");
            byte index = mode == 1 ? pair.Second393 : pair.First392;
            if (index == 255)
            {
                bool special = actor.NativeFootstepSpecial ?? throw new NotSupportedException("Footstep sound requires the allocated/SETPC sticky native flag4000000.");
                effect = mode == 1 ? special ? 53u : 2u : special ? 54u : 3u;
            }
            else
            {
                FieldSoundEffectTable effects = readEffects();
                if (index > sbyte.MaxValue || index >= effects.EffectIds.Length)
                {
                    throw new NotSupportedException("Footstep sound BYTE392/393 indexes outside the original signed-byte field SFX table.");
                }
                effect = effects.EffectIds[index];
                flags = mode == 1 ? 0x800000u : 0x400000u;
            }
        }
        return new NativeFootstepSound(actor.ObjectId, mode, effect, pan, volume, flags);
    }

    internal static (uint Pan, uint Volume) Spatialize(NativeFootstepOrigin origin, ushort focal)
    {
        if (focal == 0)
        {
            throw new NotSupportedException("Native footstep projection requires a positive focal WORD.");
        }
        //45FAA0 SZ saturates to unsigned WORD, independently of signed IR3;
        //45E250 returns SZ>>2, before520260's signed IDIV24 distance falloff.
        int sz = Math.Clamp(origin.Z, 0, ushort.MaxValue);
        int depth = Math.Max(sz, focal >> 1);
        if (depth == 0)
        {
            throw new NotSupportedException("Native footstep projection has no positive depth after its focal near clamp.");
        }
        //Reuse the proved PC precision53 division/multiplication/magic-add and
        //DWORD wrapping sequence shared with the original normal camera.
        double ratio = focal / (double)depth;
        short screenX = (short)FieldNativeCameraProbe.Project(origin.X, ratio);
        uint pan = (uint)(Math.Clamp((int)screenX, -512, 508) / 4 + 128);
        uint volume = (uint)(127 - Math.Clamp((sz >> 2) / 24, 8, 110));
        return (pan, volume);
    }

    private static bool TryAxis(Vector3 axis, out FieldNativeCameraPoint words)
    {
        words = default;
        if (!TryInteger(axis.X * 4096, out int x) || !TryInteger(axis.Y * 4096, out int y) || !TryInteger(axis.Z * 4096, out int z) || x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue || z is < short.MinValue or > short.MaxValue)
        {
            return false;
        }
        words = new FieldNativeCameraPoint(x, y, z);
        return true;
    }

    private static bool TryInteger(float value, out int integer)
    {
        integer = 0;
        if (!float.IsFinite(value) || value != MathF.Truncate(value) || value is < -16777216 or > 16777216)
        {
            return false;
        }
        integer = (int)value;
        return true;
    }
}
