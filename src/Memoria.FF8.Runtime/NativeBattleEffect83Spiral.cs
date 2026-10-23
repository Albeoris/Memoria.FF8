using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect83SpiralStep(NativeBattleEffectArena Faces, NativeBattleEffectArena Temporary148);

public static class NativeBattleEffect83Spiral
{
    public static NativeBattleEffect83SpiralStep AdvanceFace(NativeBattleEffectArena faces, int offset, NativeBattleEffectArena temporary148, bool quad)
    {
        if (offset < 32 || offset % 32 != 0 || offset > 0x7D00 - 32)
        {
            throw new InvalidDataException("Effect83 requires its bounded persistent face record after the leading row.");
        }
        short state = Read(6);
        if (state == 0)
        {
            short x = Temp(0x54);
            short y = Temp(0x56);
            short z = Temp(0x58);
            Write(0, y);
            for (int corner = 1; corner < (quad ? 4 : 3); corner++)
            {
                Write(0xA + (corner - 1) * 2, Temp(0x54 + corner * 8) - x);
                Write(0x10 + (corner - 1) * 2, Temp(0x56 + corner * 8) - y);
                Write(0x16 + (corner - 1) * 2, Temp(0x58 + corner * 8) - z);
            }
            int angle = NativePrecision53.RoundInteger(Math.Atan2(x, z) * (double)BitConverter.Int32BitsToSingle(0x4422F983));
            Write(4, angle & 0xFFF);
            int square = unchecked(x * x + z * z);
            temporary148 = Dword(temporary148, 0x88, unchecked((uint)square));
            Write(2, NativeBattleRayBomb10Geometry.Sqrt56BEC0(square));
            Write(6, Read(6) + 1);
            Write(0x1C, 4096);
            Write(8, 0);
        }
        else if (state == 1)
        {
            if (Read(2) >= Temp(0x90))
            {
                Write(8, 0);
                Write(6, 2);
            }
        }
        else
        {
            Write(8, Read(8) + 1);
            Write(0x1E, Read(0x1E) + 16);
            if (Read(0x1E) > 256)
            {
                Write(0x1E, 256);
            }
            Write(4, (Read(4) + Read(0x1E)) & 0xFFF);
            if (Read(8) >= 15)
            {
                Write(0x1C, Read(0x1C) - 32);
                if (Read(0x1C) < 256)
                {
                    Write(0x1C, 256);
                }
                temporary148 = Word(temporary148, 0x8E, Read(0x1C));
                int scale = Read(0x1C);
                foreach (int axis in new[] { 0xA, 0x10, 0x16 })
                {
                    for (int corner = 0; corner < (quad ? 3 : 2); corner++)
                    {
                        int field = axis + corner * 2;
                        Write(field, unchecked(Read(field) * scale) / 4096);
                    }
                }
            }
            int x = unchecked(NativeBattleCameraMath.Sine(Read(4)) * Read(2)) / 4096;
            int z = unchecked(NativeBattleCameraMath.Cosine(Read(4)) * Read(2)) / 4096;
            Write(0, Read(0) - 48);
            int y = Read(0);
            temporary148 = Dword(Dword(Dword(temporary148, 0x7C, unchecked((uint)x)), 0x80, unchecked((uint)y)), 0x84, unchecked((uint)z));
            for (int corner = 0; corner < (quad ? 4 : 3); corner++)
            {
                int dx = corner == 0 ? 0 : Read(0xA + (corner - 1) * 2);
                int dy = corner == 0 ? 0 : Read(0x10 + (corner - 1) * 2);
                int dz = corner == 0 ? 0 : Read(0x16 + (corner - 1) * 2);
                temporary148 = Word(Word(Word(temporary148, 0x54 + corner * 8, x + dx), 0x56 + corner * 8, y + dy), 0x58 + corner * 8, z + dz);
            }
        }
        return new(faces, temporary148);

        short Read(int field) => BinaryPrimitives.ReadInt16LittleEndian(faces.Read(offset + field, 2));
        short Temp(int field) => BinaryPrimitives.ReadInt16LittleEndian(temporary148.Read(field, 2));
        void Write(int field, int value)
        {
            faces = Word(faces, offset + field, value);
        }
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena memory, int offset, int value) => memory.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena memory, int offset, uint value) => memory.Write(offset, BitConverter.GetBytes(value));
}
