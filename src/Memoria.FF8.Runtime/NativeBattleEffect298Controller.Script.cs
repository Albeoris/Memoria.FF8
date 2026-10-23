using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect298Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static NativeBattleEffect298Frame BirthScript670C10(NativeBattleEffect298Frame frame, uint script, NativeBattleEffect298Resources resources)
    {
        (frame, uint address) = Append(frame, 0x2507890, ScriptCallback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect298 script birth requires its mandatory native task slot.");
        }
        frame = ClearDwords701200(frame, address + 12, 6);
        short row = ReadScriptWord298(frame, resources, script);
        uint destination = unchecked(frame.State.ReadDword(ArenaGlobal) + (uint)((row + 0x17B) << 4));
        return Write(frame, address, Read(frame, address).WriteDword(12, destination).WriteDword(16, script + 2));
    }

    public static (NativeBattleEffect298Frame Frame, int Result) AdvanceScript670C60(NativeBattleEffect298Frame frame, uint address, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleModulePoolTask scriptTask = Read(frame, address);
        uint cursor = scriptTask.ReadDword(16);
        uint row = scriptTask.ReadDword(12);
        short ReadOperand()
        {
            short value = ReadScriptWord298(frame, resources, cursor);
            cursor = unchecked(cursor + 2);
            return value;
        }
        void Assign(uint destination, short value)
        {
            frame = WriteBytes(frame, destination, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
        }
        short Current(uint location) => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(frame, location, 2).Read(0, 2));
        void Vector(uint destination, bool add)
        {
            for (int index = 0; index < 3; index++)
            {
                uint field = destination + (uint)(index * 2);
                short operand = ReadOperand();
                Assign(field, add ? unchecked((short)(Current(field) + operand)) : operand);
            }
        }
        while (true)
        {
            uint origin = cursor;
            short opcode = ReadOperand();
            switch (opcode)
            {
                case 0x1000:
                    Vector(row, false);
                    break;
                case 0x1001:
                    Vector(row, true);
                    break;
                case 0x1002:
                    Vector(address + 20, false);
                    break;
                case 0x1003:
                    Vector(address + 20, true);
                    break;
                case 0x1004:
                    for (int index = 0; index < 3; index++)
                    {
                        Assign(row + (uint)(index * 2), unchecked((short)(Current(row + (uint)(index * 2)) + Current(address + 20 + (uint)(index * 2)))));
                    }
                    if (Current(row + 2) > 0)
                    {
                        Assign(row + 2, 0);
                        short velocity = Current(address + 22);
                        Assign(address + 22, unchecked((short)((velocity >> 2) - velocity)));
                    }
                    break;
                case 0x1010:
                    Vector(row + 8, false);
                    break;
                case 0x1011:
                    Vector(row + 8, true);
                    break;
                case 0x1012:
                    Vector(address + 28, false);
                    break;
                case 0x1013:
                    Vector(address + 28, true);
                    break;
                case 0x1014:
                    for (int index = 0; index < 3; index++)
                    {
                        Assign(row + 8 + (uint)(index * 2), unchecked((short)(Current(row + 8 + (uint)(index * 2)) + Current(address + 28 + (uint)(index * 2)))));
                    }
                    break;
                case 0x1080:
                case 0x1081:
                case 0x1082:
                    short sourceRow = ReadOperand();
                    uint source = unchecked(frame.State.ReadDword(ArenaGlobal) + (uint)((sourceRow + 0x17B) << 4));
                    int offset = opcode == 0x1082 ? 8 : 0;
                    int length = opcode == 0x1080 ? 16 : 8;
                    frame = WriteBytes(frame, row + (uint)offset, ReadBytes(frame, source + (uint)offset, length), length);
                    break;
                case 0x1090:
                    (frame, cursor) = BirthFragments6710E0(frame, cursor, frame.State.ReadDword(CasterGlobal), frame.State.ReadDword(ArenaGlobal) + 0x17B0, resources, inputs);
                    break;
                case 0x10A0:
                case 0x10A1:
                    (frame, cursor) = BirthProgramme671990(frame, cursor, frame.State.ReadDword(CasterGlobal), frame.State.ReadDword(ArenaGlobal) + 0x17B0, opcode == 0x10A1, resources, inputs);
                    break;
                case 0x10A8:
                    (frame, cursor) = BirthSprite671E40(frame, cursor, frame.State.ReadDword(CasterGlobal), frame.State.ReadDword(ArenaGlobal) + 0x17B0, resources, inputs);
                    break;
                case 0x10A9:
                    (frame, cursor) = BirthSpriteBatch671FF0(frame, cursor, frame.State.ReadDword(CasterGlobal), frame.State.ReadDword(ArenaGlobal) + 0x17B0, resources, inputs);
                    break;
                case 0x10F0:
                    short count = ReadScriptWord298(frame, resources, cursor);
                    if (count != 0)
                    {
                        frame = frame with { State = frame.State.WriteWord(cursor, unchecked((ushort)(count - 1))) };
                        short relative = ReadScriptWord298(frame, resources, cursor + 4);
                        cursor = unchecked(origin + (uint)(relative * 2));
                    }
                    else
                    {
                        ushort reset = unchecked((ushort)ReadScriptWord298(frame, resources, cursor + 2));
                        frame = frame with { State = frame.State.WriteWord(cursor, reset) };
                        cursor = unchecked(cursor + 6);
                    }
                    break;
                case 0x1100:
                    return (frame, 2);
                default:
                    return (Write(frame, address, Read(frame, address).WriteDword(16, cursor)), 0);
            }
        }
    }

    private static short ReadScriptWord298(NativeBattleEffect298Frame frame, NativeBattleEffect298Resources resources, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.State.ReadStatic(resources, address, 2));
}
