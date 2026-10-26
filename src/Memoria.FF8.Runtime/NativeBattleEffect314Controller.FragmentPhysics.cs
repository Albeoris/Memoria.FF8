using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect314Controller
{
    public static NativeBattleEffect314Frame AdvanceFragments6642F0(NativeBattleEffect314Frame frame, uint main, NativeBattleEffect314Inputs inputs)
    {
        for (int index = 0; index < FragmentCapacity; index++)
        {
            uint address = checked(main + FragmentOffset + (uint)(index * FragmentStride));
            NativeBattleEffectArena row = ReadBytes(frame, address, FragmentStride);
            if (U16(row, 6) == 0)
            {
                continue;
            }
            ushort life = unchecked((ushort)(U16(row, 0xE) - 1));
            row = row.Write(0xE, BitConverter.GetBytes(life));
            bool free = life == 0;
            if (!free)
            {
                uint matrixAddress = unchecked((uint)I32(row, 0x10));
                NativeBattleEffectArena matrixBytes = ReadBytes(frame, matrixAddress, 20);
                NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(lane => I16(matrixBytes, lane * 2)).ToImmutableArray(), 0, 0, 0, 4096);
                (byte[] paddingBytes, bool[] paddingKnown) = matrixBytes.ReadSnapshot(18, 2);
                short? padding = paddingKnown.All(value => value) ? System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(paddingBytes) : null;
                NativeBattleGeometryRegisterStep registers = Registers(frame);
                registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, false, padding) };
                NativeBattleVector Point(int offset) => new(I16(row, offset), I16(row, offset + 2), I16(row, offset + 4));
                registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Point(0x14), I16(row, 0x1A)), false);
                row = StoreIr(row, 0x14, registers);
                registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Point(0x1A), 0), false);
                free = I32(row, 0x14) == 0 && I16(row, 0x18) == 0;
                if (!free)
                {
                    row = StoreIr(row, 0x1A, registers);
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Point(0x20), I16(row, 0x26)), false);
                    row = StoreIr(row, 0x20, registers);
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Point(0x26), 0), false);
                    row = StoreIr(row, 0x26, registers);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(I16(row, axis * 2) + I16(row, 8 + axis * 2)))));
                    }
                }
                frame = Publish(frame, registers);
            }
            if (free)
            {
                row = row.Write(6, new byte[2]);
                frame = WriteBytes(frame, main + 0x4724, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(address)), 4);
            }
            frame = WriteBytes(frame, address, row, FragmentStride);
        }
        return frame;
    }

    private static NativeBattleEffectArena StoreIr(NativeBattleEffectArena row, int offset, NativeBattleGeometryRegisterStep registers)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            int value = registers.Colors.Ir[lane] ?? throw new NotSupportedException("Effect314 fragment requires its current transformed IR value.");
            row = row.Write(offset + lane * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        return row;
    }
}
