using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeBlink
{
    public static bool Eligible(Ff7Actor actor, PcFieldCamera? camera, Ff7NativeModelPresentation configuration, Vector2 cameraOffset, bool screenMarginOverride, int originX = 0, int originY = 0)
    {
        if (!actor.Visible || configuration.Mask == 0 || camera is null || camera.AxisZ == Vector3.Zero && camera.Translation.Z <= 0)
        {
            return false;
        }
        Vector3 point = Ff7NativeModelPosition.PresentationPoint(actor, configuration);
        Vector3 projected = PcFieldProjection.Project(point, camera, new(originX, originY));
        int depth = unchecked((int)((double)projected.Z * 0.25));
        if (depth >= 3840)
        {
            return false;
        }
        if (!float.IsFinite(projected.X) || !float.IsFinite(projected.Y))
        {
            throw new NotSupportedException("Native model presentation has no defined screen coordinates at this camera depth.");
        }
        if (screenMarginOverride)
        {
            return true;
        }
        short x = unchecked((short)(unchecked((short)(int)projected.X) + unchecked((short)(int)cameraOffset.X) + 160));
        short y = unchecked((short)(unchecked((short)(int)projected.Y) + unchecked((short)(int)cameraOffset.Y) + 112));
        return y > originY - 120 && y < originY + 460 && x > originX - 40 && x < originX + 400;
    }

    public static Ff7FieldProcess Advance(Ff7FieldProcess process, IReadOnlyList<byte> random, Func<Ff7Actor, bool> eligible)
    {
        if (random.Count != 256)
        {
            throw new InvalidDataException("Native blink timing requires the original 256-byte table.");
        }
        Ff7Actor[] actors = process.Actors.ToArray();
        byte index = process.BlinkRandomIndex;
        foreach (int position in Enumerable.Range(0, actors.Length).OrderBy(position => actors[position].Slot))
        {
            Ff7Actor actor = actors[position];
            if (actor.AutomaticBlinkDisabled || !eligible(actor))
            {
                continue;
            }
            if (actor.BlinkCountdown == 0)
            {
                actors[position] = actor with { NativeEyeLeft = 2, NativeEyeRight = 2, BlinkCountdown = (ushort)((random[index] & 31) + 64) };
                index = unchecked((byte)(index + 1));
            }
            else
            {
                actors[position] = actor with { NativeEyeLeft = 1, NativeEyeRight = 1, BlinkCountdown = unchecked((ushort)(actor.BlinkCountdown - 1)) };
            }
        }
        return process with { Actors = actors, BlinkRandomIndex = index };
    }
}
