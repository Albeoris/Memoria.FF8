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
        Validate();
        int flags = Flags;
        int focal = ProjectionDistance;
        int Advance(int value, int target, int speed, int flag)
        {
            if ((flags & flag) == 0)
            {
                return value;
            }
            if (Math.Abs(value - target) <= speed)
            {
                flags ^= flag;
                return target;
            }
            return value + Math.Sign(target - value) * speed;
        }
        focal = Advance(focal, alternateFoot ? 640 : 1024, 8, 1);
        // Foot already has the normal curvature and movement owners; bits2/4
        // complete the same-frame native bookkeeping without replacing them.
        flags &= ~6;
        bool changingPitch = (flags & 16) != 0;
        pitch = checked((short)Advance(pitch, alternateFoot ? -512 : -112, 16, 16));
        if (changingPitch && collisionRearmsPitch && collision)
        {
            //558D70 XORs this bit even if the preceding signed clamp cleared it.
            flags ^= 16;
        }
        offsetZ = checked((short)Advance(offsetZ, -7168, 512, 32));
        offsetY = checked((short)Advance(offsetY, -341, 512, 128));
        return (new NativeFootCameraTransition(flags, focal), pitch, offsetY, offsetZ);
    }
}
