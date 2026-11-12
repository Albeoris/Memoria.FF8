using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Native 20409D8 and 204007C for the ordinary foot camera's 558D70 transition.</summary>
public sealed record NativeFootCameraTransition([property: JsonRequired] int Flags, [property: JsonRequired] int ProjectionDistance)
{
    public static NativeFootCameraTransition TrainDismount => new(183, 1024);

    public void Validate()
    {
        if ((Flags & ~183) != 0 || ProjectionDistance is < 640 or > 1024)
        {
            throw new InvalidDataException("Invalid native foot camera transition words.");
        }
    }

    public NativeFootWorldRuntimeState Step(NativeFootWorldRuntimeState foot, bool collision)
    {
        var result = StepWords(foot.AnimationVariant == 1, foot.Forest!.CameraPitch, foot.CameraOffsetY, foot.CameraOffsetZ, collision, foot.AnimationVariant == 0);
        return foot with { CameraTransition = result.Transition, CameraOffsetY = result.OffsetY, CameraOffsetZ = result.OffsetZ, Forest = foot.Forest with { CameraPitch = result.Pitch } };
    }

    public (NativeFootCameraTransition Transition, short Pitch, short OffsetY, short OffsetZ) StepWords(bool alternateFoot, short pitch, short offsetY, short offsetZ, bool collision, bool collisionRearmsPitch)
    {
        return StepWords(pitch, offsetY, offsetZ, alternateFoot ? 640 : 1024, alternateFoot ? (short)-512 : (short)-112, -341, -7168, collisionRearmsPitch && collision);
    }

    internal (NativeFootCameraTransition Transition, short Pitch, short OffsetY, short OffsetZ) StepWords(short pitch, short offsetY, short offsetZ, int targetFocal, short targetPitch, short targetY, short targetZ, bool rearmPitch)
    {
        Validate();
        int flags = Flags;
        int focal = ProjectionDistance;
        int Advance(int value, int target, int difference, int speed, int flag)
        {
            if ((flags & flag) == 0)
            {
                return value;
            }
            if (Math.Abs(difference) <= speed)
            {
                flags ^= flag;
                return target;
            }
            return value - Math.Sign(difference) * speed;
        }
        focal = Advance(focal, targetFocal, focal - targetFocal, 8, 1);
        // Ordinary ground owners already use curve 5216. The transfer applies
        // its speed reset on entry, so bits 2 and 4 finish on this callback.
        flags &= ~6;
        bool changingPitch = (flags & 16) != 0;
        pitch = unchecked((short)Advance(pitch, targetPitch, unchecked((short)(pitch - targetPitch)), 16, 16));
        if (changingPitch && rearmPitch)
        {
            //558D70 XORs this bit even if the preceding signed clamp cleared it.
            flags ^= 16;
        }
        offsetZ = unchecked((short)Advance(offsetZ, targetZ, unchecked((short)(offsetZ - targetZ)), 512, 32));
        offsetY = unchecked((short)Advance(offsetY, targetY, unchecked((short)(offsetY - targetY)), 512, 128));
        return (new NativeFootCameraTransition(flags, focal), pitch, offsetY, offsetZ);
    }
}
