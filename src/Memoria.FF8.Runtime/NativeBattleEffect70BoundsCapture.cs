using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect70BoundsStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary24, short Maximum, short Minimum);

public static class NativeBattleEffect70BoundsCapture
{
    public static NativeBattleEffect70BoundsStep Capture6DFD90(NativeBattleEffectMemory memory, NativeBattleEffectArena retained24, IReadOnlyList<NativeBattlePackedMatrix32> bones, Func<int, int, byte[]> readGeometry, uint objectMask, NativeBattleCameraProjection projection)
    {
        NativeBattleEffectArena temporary = retained24.Write(0, BitConverter.GetBytes(0x10000)).Write(4, BitConverter.GetBytes(-0x10000)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes((short)4096)).Write(20, BitConverter.GetBytes(objectMask));
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect70 bounds require their current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect70 bounds require their current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Effect70 bounds require their current projection controls.");
        int minimum = 0x10000;
        int maximum = -0x10000;
        int objects = Dword(0);
        for (int item = 0; item < objects; item++)
        {
            int cursor = Dword(checked(4 + item * 4));
            if ((objectMask & (1u << (item & 31))) == 0)
            {
                continue;
            }
            int groups = Word(cursor);
            cursor = checked(cursor + 2);
            for (int group = 0; group < groups; group++)
            {
                int bone = Word(cursor);
                int count = Word(checked(cursor + 2));
                cursor = checked(cursor + 4);
                if (count < 0 || (uint)bone >= bones.Count)
                {
                    throw new InvalidDataException("Effect70 bounds leave the current packed bone or vertex directory.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, true, null) with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray() } };
                for (int vertex = 0; vertex < count; vertex++)
                {
                    byte[] xyz = readGeometry(cursor, 6);
                    NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
                    registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, point, 0), projection, controls.Dqa, controls.Dqb);
                    short y = unchecked((short)registers.Colors.Ir[1]!.Value);
                    temporary = temporary.Write(16, BitConverter.GetBytes(y));
                    minimum = Math.Min(minimum, y);
                    maximum = Math.Max(maximum, y);
                    temporary = temporary.Write(0, BitConverter.GetBytes(minimum)).Write(4, BitConverter.GetBytes(maximum));
                    cursor = checked(cursor + 6);
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary, unchecked((short)maximum), unchecked((short)minimum));

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(offset, 2));
        int Dword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(offset, 4));
    }
}
