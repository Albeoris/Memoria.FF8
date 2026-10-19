using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static int AdvanceNativeUltraWavesEmitter(ref NativeBattleModulePoolTask task, byte[] particles, NativeBattleUltraWaves154Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool paused, Action<NativeBattleGfGeometryBatch> append)
    {
        NativeBattleVector direction = new(Word(task, 24), Word(task, 26), Word(task, 28));
        NativeBattleModuleAxisAngle alignment = NativeBattleModuleAlignment.Between571480(0, 0, -4096, direction.X, direction.Y, direction.Z);
        int alive = 0;
        uint flags = 0x33;
        int fade = 0;
        for (int particle = 0; particle < 100; particle++)
        {
            int offset = particle * 24;
            if ((Read(offset) & task.ReadWord(14)) == 0)
            {
                continue;
            }
            int life = Read(offset + 4);
            NativeBattleActionMatrix matrix = new(NativeBattleModuleAlignment.Rotation5714F0(alignment.Angle, alignment.Axis), Read(offset + 8), Read(offset + 10), Read(offset + 12), 4096);
            NativeBattleActionMatrix spin = new(NativeBattleModuleAlignment.Rotation5714F0(Read(offset + 14), direction), 0, 0, 0, 4096);
            spin = NativeBattleGfMatrices.Multiply56C090(spin, matrix);
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(spin, new(Read(offset + 22), 0, 0));
            matrix = matrix with { X = unchecked(matrix.X + unchecked((short)x)), Y = unchecked(matrix.Y + unchecked((short)y)), Z = unchecked(matrix.Z + unchecked((short)z)) };
            int scale = Read(offset + 6);
            matrix = NativeBattleGfMatrices.Compose56C2F0(view, NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale));
            if (life >= 6)
            {
                flags |= 0xC0;
                fade = (life - 6) * 682;
            }
            append(NativeBattleUltraWaves154Drawing.Ring(resources.Ring, matrix, projection, resources.RingColors[Read(offset + 2)], flags, fade, colors));
            if (paused)
            {
                continue;
            }
            Put(offset + 4, life + 1);
            if (life + 1 >= 12)
            {
                Put(offset, 0);
                continue;
            }
            Put(offset + 2, ((byte)Read(offset + 2) + 1) & 15);
            Put(offset + 6, scale + scale / 6);
            for (int axis = 0; axis < 3; axis++)
            {
                Put(offset + 8 + axis * 2, Read(offset + 8 + axis * 2) + Read(offset + 16 + axis * 2));
            }
            Put(offset + 14, Read(offset + 14) + 512);
            Put(offset + 22, Read(offset + 22) + 10);
            alive++;
        }
        if (paused)
        {
            return 0;
        }
        int frame = Word(task, 12);
        if (frame is >= 0 and <= 20)
        {
            for (int particle = 0; particle < 100; particle++)
            {
                int offset = particle * 24;
                if (Read(offset) != 0)
                {
                    continue;
                }
                Put(offset, task.ReadWord(14));
                Put(offset + 2, Word(task, 34));
                Put(offset + 4, 0);
                Put(offset + 6, 1024);
                for (int axis = 0; axis < 3; axis++)
                {
                    Put(offset + 8 + axis * 2, Word(task, 16 + axis * 2));
                    Put(offset + 16 + axis * 2, Word(task, 24 + axis * 2) * 150 >> 12);
                }
                Put(offset + 14, Word(task, 32));
                Put(offset + 22, 100);
                break;
            }
        }
        task = task.WriteWord(32, unchecked((ushort)(Word(task, 32) + 256))).WriteWord(34, (ushort)((unchecked((byte)Word(task, 34)) - 1) & 15)).WriteWord(12, unchecked((ushort)(frame + 1)));
        return frame + 1 >= 12 && alive == 0 ? 2 : 0;

        static short Word(NativeBattleModulePoolTask owner, int offset) => unchecked((short)owner.ReadWord(offset));
        short Read(int offset) => BinaryPrimitives.ReadInt16LittleEndian(particles.AsSpan(offset));
        void Put(int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(particles.AsSpan(offset), unchecked((short)value));
    }
}
