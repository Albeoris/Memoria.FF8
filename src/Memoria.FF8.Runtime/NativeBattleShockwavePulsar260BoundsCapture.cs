using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260BoundsStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary24, short Maximum, short Minimum);

public static class NativeBattleShockwavePulsar260BoundsCapture
{
    public static NativeBattleShockwavePulsar260BoundsStep Capture693000(NativeBattleEffectMemory memory, NativeBattleEffectArena retained24, IReadOnlyList<NativeBattlePackedMatrix32> bones, Func<int, int, byte[]> readGeometry, uint objectMask)
    {
        retained24.Validate();
        if (retained24.Regions.Any(region => region.Offset < 0 || region.Data.Length > 24 - region.Offset) || retained24.UnknownRegions.Any(region => region.Offset < 0 || region.Length > 24 - region.Offset))
        {
            throw new InvalidDataException("Shockwave Pulsar bounds require their physical24-byte allocation.");
        }
        NativeBattleEffectArena temporary = Dword(Dword(Dword(retained24, 0, 0x10000), 4, 0xFFFF0000), 8, 0).Write(0xC, BitConverter.GetBytes((short)4096));
        temporary = Dword(temporary, 0x14, objectMask);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Shockwave Pulsar bounds require their current GTE matrix."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Shockwave Pulsar bounds require their current GTE data."));
        int minimum = 0x10000;
        int maximum = -0x10000;
        int objects = I32(0);
        for (int index = 0; index < objects; index++)
        {
            int cursor = I32(checked(4 + index * 4));
            if ((objectMask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            int groups = I16(cursor);
            cursor = checked(cursor + 2);
            for (int group = 0; group < groups; group++)
            {
                int bone = I16(cursor);
                int count = I16(checked(cursor + 2));
                cursor = checked(cursor + 4);
                if (count < 0)
                {
                    throw new NotSupportedException("Shockwave Pulsar bounds require the original nonnegative vertex loop count.");
                }
                if ((uint)bone >= bones.Count)
                {
                    throw new InvalidDataException("Shockwave Pulsar bounds selected an absent current bone matrix.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                short? padding = matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, padding) };
                for (int vertex = 0; vertex < count; vertex++)
                {
                    byte[] xyz = Read(cursor, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    short y = unchecked((short)registers.Colors.Ir[1]!.Value);
                    temporary = temporary.Write(0x10, BitConverter.GetBytes(y));
                    minimum = Math.Min(minimum, y);
                    maximum = Math.Max(maximum, y);
                    temporary = Dword(Dword(temporary, 0, unchecked((uint)minimum)), 4, unchecked((uint)maximum));
                    cursor = checked(cursor + 6);
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary, unchecked((short)maximum), unchecked((short)minimum));

        short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Read(offset, 2));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
        byte[] Read(int offset, int length)
        {
            byte[] bytes = readGeometry(offset, length);
            return bytes.Length == length ? bytes : throw new InvalidDataException("Shockwave Pulsar bounds require exact source bytes.");
        }
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena target, int offset, uint value) => target.Write(offset, BitConverter.GetBytes(value));
}
