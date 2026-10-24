using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal static NativeBattleEffect285CameraSnapshot CaptureNativeEffect285Camera(NativeBattleCameraState camera)
    {
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Effect285 requires its current camera pose.");
        NativeBattleCameraPose saved = camera.CapturedPose ?? throw new NotSupportedException("Effect285 requires its actual captured camera pose.");
        return new(Point(pose.Eye, camera.EyePaddingF6), Point(pose.Target, camera.TargetPaddingFE), Point(saved.Eye, camera.CapturedEyePadding806), Point(saved.Target, camera.CapturedTargetPadding80E), camera.FocalLength, saved.FocalLength, camera.Roll);

        static NativeBattleEffectArena Point(NativeBattleCameraPoint point, short? padding)
        {
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
            return padding is short word ? result.Write(6, BitConverter.GetBytes(word)) : result;
        }
    }

    internal static NativeBattleCameraState ApplyNativeEffect285CameraPoint(NativeBattleCameraState camera, NativeBattleEffect285CameraPoint write)
    {
        NativeBattleCameraPose pose = camera.CurrentPose ?? throw new NotSupportedException("Effect285 requires its current camera pose.");
        byte[] bytes = write.Point8.Read(0, 6);
        NativeBattleCameraPoint point = new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        camera = camera.WriteEffectGlobals((write.Eye ? pose with { Eye = point } : pose with { Target = point }) with { FocalLength = camera.FocalLength, Roll = camera.Roll });
        if (!write.CopyPadding)
        {
            return camera;
        }
        (byte[] padding, bool[] known) = write.Point8.ReadSnapshot(6, 2);
        if (known[0] != known[1])
        {
            throw new NotSupportedException("Effect285 camera padding requires its retained WORD knownness.");
        }
        short? value = known[0] ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null;
        return write.Eye ? camera with { EyePaddingF6 = value } : camera with { TargetPaddingFE = value };
    }

    internal static NativeBattleGraphicsModel ApplyNativeEffect285ModelFlags(NativeBattleGraphicsModel model, NativeBattleEffect285ModelFlags write)
    {
        ushort flags = write.Width switch
        {
            1 => unchecked((ushort)((model.Flags0 & 0xFF00) | (((model.Flags0 & write.AndMask) | write.OrMask) & 0xFF))),
            2 => unchecked((ushort)((model.Flags0 & write.AndMask) | write.OrMask)),
            _ => throw new NotSupportedException("Effect285 model flag stores require their actual BYTE or WORD width.")
        };
        return model with { Flags0 = flags };
    }
}
