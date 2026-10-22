using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCpuTrails
{
    public static NativeBattleCpuTrailAddressStep StartWorld(NativeBattleCpuTrailFrame frame, NativeBattleCpuTrailLayout layout, NativeBattleVector velocity, NativeBattleVector source, NativeBattleVector target, NativeBattleVector? drift, short actor, short progress, short increment, short scale, short textureV, short branches)
    {
        (frame, uint head) = AllocateWorldPoint(frame, layout, source, scale);
        if (head == 0)
        {
            return new(frame, 0);
        }
        (frame, uint address) = Append(frame, layout.WorldPoolIndex, layout.WorldCallback);
        if (address == 0)
        {
            return new(PointWord(frame, head + 6, -1), 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[48]).WriteWord(12, 0).WriteWord(14, 1).WriteDword(0x10, head).WriteDword(0x14, head);
        task = Vector(task, 0x18, source).WriteWord(0x1E, unchecked((ushort)progress));
        task = Vector(task, 0x20, target).WriteWord(0x26, unchecked((ushort)increment));
        task = Vector(task, 0x28, velocity).WriteWord(0x2E, unchecked((ushort)branches));
        if (drift is NativeBattleVector known)
        {
            task = Vector(task, 0x30, known);
        }
        else
        {
            for (int axis = 0; axis < 3; axis++)
            {
                frame = DrawRandom(frame, out ushort random);
                task = task.WriteWord(0x30 + axis * 2, unchecked((ushort)((random & 255) - 128)));
            }
        }
        task = task.WriteWord(0x36, unchecked((ushort)actor)).WriteWord(0x38, unchecked((ushort)scale)).WriteWord(0x3A, unchecked((ushort)textureV));
        return new(Write(frame, address, task), head);
    }

    public static NativeBattleCpuTrailAddressStep AllocateWorldPoint(NativeBattleCpuTrailFrame frame, NativeBattleCpuTrailLayout layout, NativeBattleVector position, short scale)
    {
        ValidateLayout(layout);
        uint free = PointDword(frame, layout.WorldFreePointerAddress);
        if (free != 0)
        {
            RequireWorldPoint(layout, free);
            frame = WriteArena(frame, layout.WorldFreePointerAddress, BitConverter.GetBytes(0u));
        }
        else
        {
            for (int index = 0; index < layout.PointCount; index++)
            {
                uint row = layout.WorldBankAddress + (uint)(index * 40);
                if (PointSigned(frame, row + 6) == -1)
                {
                    free = row;
                    break;
                }
            }
        }
        if (free == 0)
        {
            return new(frame, 0);
        }
        frame = PointVector(frame, free, position);
        frame = PointWord(PointWord(PointWord(PointWord(frame, free + 6, scale), free + 14, 2), free + 0x20, 512), free + 0x22, 768);
        frame = WriteArena(frame, free + 0x24, BitConverter.GetBytes(0u));
        return new(frame, free);
    }

    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    private static short PointSigned(NativeBattleCpuTrailFrame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address, 2), 2));
    private static uint PointDword(NativeBattleCpuTrailFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static NativeBattleCpuTrailFrame PointWord(NativeBattleCpuTrailFrame frame, uint address, short value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleCpuTrailFrame PointVector(NativeBattleCpuTrailFrame frame, uint address, NativeBattleVector value) => PointWord(PointWord(PointWord(frame, address, value.X), address + 2, value.Y), address + 4, value.Z);
    private static void RequireWorldPoint(NativeBattleCpuTrailLayout layout, uint address)
    {
        uint relative = unchecked(address - layout.WorldBankAddress);
        if (relative >= layout.PointCount * 40 || relative % 40 != 0)
        {
            throw new InvalidDataException("Native CPU world trail leaves its original point bank.");
        }
    }
}
