using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect64Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect64Script
{
    public static NativeBattleEffect64Step Advance6E5DD0(NativeBattleEffect64Frame frame, uint address, NativeBattleEffect64Resources resources, NativeBattleEffect64Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint cursor = task.ReadDword(0x18);
        uint row = task.ReadDword(0x14);
        uint model = task.ReadDword(0xC);
        uint rows = task.ReadDword(0x10);
        short Operand()
        {
            short value = unchecked((short)frame.State.ReadWord(cursor));
            cursor = unchecked(cursor + 2);
            return value;
        }
        short Current(uint location) => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, location, 2).Read(0, 2));
        void Assign(uint location, short value)
        {
            frame = WriteBytes(frame, location, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
        }
        void Vector(uint location, bool add)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                uint field = location + (uint)(axis * 2);
                short value = Operand();
                Assign(field, add ? unchecked((short)(Current(field) + value)) : value);
            }
        }
        while (true)
        {
            uint origin = cursor;
            short opcode = Operand();
            switch (opcode)
            {
                case 0x1000:
                case 0x1001:
                    Vector(row, opcode == 0x1001);
                    break;
                case 0x1002:
                case 0x1003:
                    Vector(address + 0x1C, opcode == 0x1003);
                    break;
                case 0x1004:
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint field = row + (uint)(axis * 2);
                        Assign(field, unchecked((short)(Current(field) + Current(address + 0x1C + (uint)(axis * 2)))));
                    }
                    break;
                case 0x100A:
                    Assign(row + 6, Operand());
                    break;
                case 0x1010:
                case 0x1011:
                    Vector(row + 8, opcode == 0x1011);
                    break;
                case 0x1012:
                case 0x1013:
                    Vector(address + 0x24, opcode == 0x1013);
                    break;
                case 0x1014:
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint field = row + 8 + (uint)(axis * 2);
                        Assign(field, unchecked((short)(Current(field) + Current(address + 0x24 + (uint)(axis * 2)))));
                    }
                    break;
                case 0x1080:
                case 0x1081:
                case 0x1082:
                    uint source = unchecked(rows + (uint)(Operand() * 16));
                    int offset = opcode == 0x1082 ? 8 : 0;
                    int length = opcode == 0x1080 ? 16 : 8;
                    frame = WriteBytes(frame, row + (uint)offset, ReadBytes(frame, source + (uint)offset, length), length);
                    break;
                case 0x1090:
                    Assign(row + 6, 0x80);
                    break;
                case 0x10A0:
                case 0x10A1:
                    (frame, cursor) = NativeBattleEffect64Programme.Birth6E61E0(frame, cursor, model, rows, opcode == 0x10A1, resources, inputs);
                    break;
                case 0x10A8:
                    (frame, cursor) = NativeBattleEffect64Particles.Birth6E6710(frame, cursor, model, rows, inputs);
                    break;
                case 0x10A9:
                    (frame, cursor) = NativeBattleEffect64Particles.Birth6E68C0(frame, cursor, model, rows, resources, inputs);
                    break;
                case 0x10B0:
                    (frame, cursor) = NativeBattleEffect64Particles.Overlay6E6B60(frame, cursor, inputs);
                    break;
                case 0x10F0:
                    short count = unchecked((short)frame.State.ReadWord(cursor));
                    if (count != 0)
                    {
                        frame = frame with { State = frame.State.WriteWord(cursor, unchecked((ushort)(count - 1))) };
                        short relative = unchecked((short)frame.State.ReadWord(cursor + 4));
                        cursor = unchecked(origin + (uint)(relative * 2));
                    }
                    else
                    {
                        frame = frame with { State = frame.State.WriteWord(cursor, frame.State.ReadWord(cursor + 2)) };
                        cursor = unchecked(cursor + 6);
                    }
                    break;
                case 0x1100:
                    return new(frame, 2);
                default:
                    return new(Write(frame, address, Read(frame, address).WriteDword(0x18, cursor)), 0);
            }
        }
    }

    public static short ReadWord(NativeBattleEffect64Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
}
