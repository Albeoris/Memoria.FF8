using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>552EB0's executed signed-WORD camera angles and retained203ED04 offset.</summary>
public sealed record NativeTrainCameraGeometry([property: JsonRequired] short Pitch, [property: JsonRequired] short Yaw, [property: JsonRequired] short OffsetZ)
{
    public static NativeTrainCameraGeometry Evaluate(NativeWorldPathCamera camera)
    {
        NativeWorldPathPosition eye = camera.Eye ?? throw new InvalidDataException("Scripted552EB0 requires its executed eye position.");
        if (camera.FootState is not null)
        {
            throw new InvalidDataException("Scripted552EB0 cannot consume normal-foot camera words.");
        }
        int dx = unchecked((short)(camera.Target.X - eye.X));
        int dy = unchecked((short)(camera.Target.Y - eye.Y));
        int dz = unchecked((short)(camera.Target.Z - eye.Z));
        long squared = (long)dx * dx + (long)dy * dy + (long)dz * dz;
        long horizontal = (long)dx * dx + (long)dz * dz;
        if (squared is <= 0 or > int.MaxValue || horizontal > int.MaxValue || (int)Math.Sqrt(squared) > short.MaxValue)
        {
            throw new NotSupportedException("Native scripted camera exceeds its finite signed-WORD distance contract.");
        }
        return new NativeTrainCameraGeometry(unchecked((short)NativeWorldScriptedPathMath.NativeAngle(-dy, (int)Math.Sqrt(horizontal))), unchecked((short)NativeWorldScriptedPathMath.NativeAngle(dx, dz)), checked((short)-(int)Math.Sqrt(squared)));
    }
}
