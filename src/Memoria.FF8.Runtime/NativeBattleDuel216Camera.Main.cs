using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216CameraStep(NativeBattleDuel216Frame Frame, NativeBattleCameraState Camera);

public static partial class NativeBattleDuel216Camera
{
    public static NativeBattleDuel216CameraStep AdvanceMain6960E0(NativeBattleDuel216Frame frame, ushort tick, NativeBattleCameraState camera)
    {
        if (tick == 0)
        {
            camera = camera.Apply50A730();
            camera = camera.WriteEffectGlobals(Pose(Word(0x25088F8) - 136, -542, Word(0x25088FC) - 208, Word(0x25088F8) - 624, -688, Word(0x25088FC) - 1872) with { Roll = 0 });
            camera = camera with { Projection = camera.Projection is { } projection ? projection with { FocalLength = 288 } : new(null, 288, null, null) };
            frame = frame with { Staging = frame.Staging.Write(0x1D8E038, BitConverter.GetBytes((short)288)).Write(0x1D977A2, BitConverter.GetBytes((short)0)) };
        }
        if (tick == 8)
        {
            camera = camera.WriteEffectGlobals(Pose(Word(0x2508900) - 45, -483, Word(0x2508904) + 458, Word(0x2508900) + 1003, -585, Word(0x2508904) + 2515));
        }
        if (tick == 16)
        {
            camera = camera.WriteEffectGlobals(Pose(Word(0x2508900) - 98, -890, Word(0x2508904) - 153, Word(0x2508900) + 2508, -1315, Word(0x2508904) + 433));
        }
        if ((uint)(unchecked((short)tick) - 22) < 48)
        {
            NativeBattleCameraPose first = Pose(Word(0x2508900) + 193, Word(0x2508902) - 3119, Word(0x2508904) - 401, Word(0x2508900) + 978, Word(0x2508902) - 2440, Word(0x2508904) - 249);
            NativeBattleCameraPose second = Pose(Word(0x2508900) + 29, Word(0x2508902) - 9236, Word(0x2508904) - 658, Word(0x2508900) + 2995, Word(0x2508902) - 4892, Word(0x2508904) + 63);
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ((tick - 22) << 12) / 47));
        }
        if ((uint)(unchecked((short)tick) - 70) < 32)
        {
            NativeBattleCameraPose first = Pose(Word(0x2508900) - 25, Dword(0x2508908) - 15242, Word(0x2508904) + 5076, Word(0x2508900) - 129, Dword(0x2508908) - 15046, Word(0x2508904) + 7104);
            NativeBattleCameraPose second = Pose(Word(0x2508900) + 66, Dword(0x2508908) - 14621, Word(0x2508904) + 4559, Word(0x2508900) + 5394, Dword(0x2508908) - 14408, Word(0x2508904) + 4780);
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ((tick - 70) << 12) / 31));
        }
        if ((uint)(unchecked((short)tick) - 102) < 10)
        {
            NativeBattleCameraPose first = Pose(Word(0x2508900) + 66, Dword(0x2508908) - 14621, Word(0x2508904) + 4559, Word(0x2508900) + 5394, Dword(0x2508908) - 14408, Word(0x2508904) + 4780);
            NativeBattleCameraPose second = Pose(Word(0x2508900) + 18, Dword(0x2508908) - 2742, Word(0x2508904) - 39, Word(0x2508900) + 897, Dword(0x2508908) - 5827, Word(0x2508904) + 4452);
            camera = camera.WriteEffectGlobals(NativeBattleDiablos325Orbit.Interpolate657DF0(first, second, ((tick - 102) << 12) / 9));
        }
        if (tick == 112)
        {
            camera = camera.WriteEffectGlobals(Pose(Word(0x2508900) + 529, Word(0x2508902) - 1292, Word(0x2508904) - 714, Word(0x2508900) + 2309, Word(0x2508902) - 1984, Word(0x2508904) - 2628));
        }
        return new(frame, camera);

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(address, 2));
        int Dword(uint address) => BinaryPrimitives.ReadInt32LittleEndian(frame.Staging.Read(address, 4));
        NativeBattleCameraPose Pose(int targetX, int targetY, int targetZ, int eyeX, int eyeY, int eyeZ) => new(new(unchecked((short)eyeX), unchecked((short)eyeY), unchecked((short)eyeZ)), new(unchecked((short)targetX), unchecked((short)targetY), unchecked((short)targetZ)), camera.FocalLength, camera.Roll);
    }
}
