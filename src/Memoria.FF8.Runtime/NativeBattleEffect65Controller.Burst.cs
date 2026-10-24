using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Controller
{
    private static NativeBattleEffect65Frame AdvanceLateBurst(NativeBattleEffect65Frame frame, uint main, int relative, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        frame = RequestWhite(frame, inputs);
        if (relative == 0)
        {
            frame = InitializeProgramme(frame, main + 0x48, 0xC8, 6, resources);
            frame = WriteBytes(frame, ChildDescriptor, NativeBattleEffectArena.Clear().Write(0, new byte[8]), 8);
            frame = HideEnabledActors(frame, 0, inputs);
            frame = NativeBattleEffect65Particles.Initialize6E2E20(frame);
            frame = NativeBattleEffect65Particles.Initialize6E3580(frame);
            frame = frame with { State = frame.State.Write(0x2544FD0, [0, 0, 1, 1]) };
            frame = inputs.Execute(frame, new NativeBattleEffect65ModelFlags(frame.State.ReadDword(CasterGlobal), 0xFFFF, 4));
            NativeBattleVector center = Vector(TaskView(Read(frame, main), 0x1C, 6));
            center = V(center.X, center.Y, center.Z + 2000);
            frame = BirthShards(frame, center);
        }
        if (frame.State.ReadDword(ControlGlobal) == 0)
        {
            for (int count = 0; count < 2; count++)
            {
                (frame, uint row) = NativeBattleEffect65Particles.Allocate6E35B0(frame);
                if (row == 0)
                {
                    break;
                }
                frame = Random(frame, out int randomX);
                frame = Random(frame, out int randomZ);
                int angleX = randomX * 300 / 32768 + 200;
                NativeBattleActionMatrix x = NativeBattleGfMatrices.Euler701310(V(angleX, 0, 0));
                NativeBattleActionMatrix z = NativeBattleGfMatrices.Euler701310(V(0, 0, randomZ));
                NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(z, x);
                NativeBattleEffectArena bytes = ReadBytes(frame, row, 32).Write(0, new byte[4]).Write(4, BitConverter.GetBytes((ushort)4096)).Write(6, BitConverter.GetBytes((ushort)32));
                bytes = CopyBytes(PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 0, bytes, 8, 18);
                bytes = bytes.Write(0x1A, BitConverter.GetBytes(unchecked((short)(-matrix.Rotation[2] >> 5)))).Write(0x1C, BitConverter.GetBytes(unchecked((short)(-matrix.Rotation[5] >> 5)))).Write(0x1E, BitConverter.GetBytes(unchecked((short)(-matrix.Rotation[8] >> 5))));
                frame = WriteBytes(frame, row, bytes, 32);
            }
        }
        int factor = NativeBattleCameraMath.Cosine((relative << 10) / 70);
        for (int index = 0; index < 3; index++)
        {
            factor = NativeBattleCameraMath.Cosine((4096 - factor) >> 2);
        }
        frame = frame with { State = frame.State.WriteDword(ScaleGlobal, unchecked((uint)(0x1200 - factor))) };
        NativeBattleActionMatrix callerMatrix = NativeBattlePandemona291Restoration.Rotation701270(0) with { X = 0, Y = 0, Z = 4096 };
        NativeBattleEffectArena caller = Caller48(frame, callerMatrix, 0x133AB98, 0, -1024, CommonCursor(frame));
        return NativeBattleEffect65Programme.Advance(frame, main + 0x48, 0xC8, caller, unchecked((int)frame.State.ReadDword(ControlGlobal)), resources, inputs, true, 0x1D8E054).Frame;
    }

    public static NativeBattleEffect65Frame BirthShards(NativeBattleEffect65Frame frame, NativeBattleVector center)
    {
        for (int count = 0; count < 128; count++)
        {
            (frame, uint row) = NativeBattleEffect65Particles.Allocate6E2E50(frame);
            if (row == 0)
            {
                throw new InvalidDataException("Effect65 exhausted its freshly initialized mandatory shard records.");
            }
            frame = Random(frame, out int polar);
            frame = Random(frame, out int angle);
            NativeBattleVector direction = Direction6A6020(polar * 1600 / 32768 - 800, (angle & 4095) - 2048);
            frame = Random(frame, out int speedValue);
            int speed = (speedValue & 63) + 32;
            NativeBattleEffectArena bytes = ReadBytes(frame, row, 22);
            bytes = PutPoint(bytes, 0, V(center.X + (direction.X >> 4), center.Y + (direction.Y >> 4), center.Z + (direction.Z >> 4))).Write(6, [unchecked((byte)(32768 / speed))]);
            bytes = PutPoint(bytes, 8, V(direction.X * speed >> 12, direction.Y * speed >> 12, direction.Z * speed >> 12));
            frame = Random(frame, out int size);
            bytes = bytes.Write(14, BitConverter.GetBytes(unchecked((ushort)((size & 0x3FFF) + 1024))));
            frame = Random(frame, out int firstAngle);
            frame = Random(frame, out int secondAngle);
            bytes = bytes.Write(16, BitConverter.GetBytes(unchecked((ushort)firstAngle))).Write(18, BitConverter.GetBytes(unchecked((ushort)secondAngle)));
            frame = Random(frame, out int firstStep);
            frame = Random(frame, out int secondStep);
            frame = Random(frame, out int source);
            bytes = bytes.Write(20, [unchecked((byte)((firstStep & 127) - 64)), unchecked((byte)((secondStep & 127) - 64))]).Write(7, [(byte)(source & 7)]);
            frame = WriteBytes(frame, row, bytes, 22);
        }
        return frame;
    }
}
