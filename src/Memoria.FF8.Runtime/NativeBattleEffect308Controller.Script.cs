using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Controller
{
    public static NativeBattleEffect308Frame BirthScript667940(NativeBattleEffect308Frame frame, uint model, uint rows, uint source)
    {
        (frame, uint address) = AppendRequired(frame, ScriptDescriptor, ScriptCallback);
        frame = ClearDwords701200(frame, address + 0x14, 8);
        short row = Word308(ReadBytes(frame, source, 2), 0);
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(12, model).WriteDword(16, rows).WriteDword(20, unchecked(rows + (uint)(row << 4))).WriteDword(24, source + 2);
        return Write(frame, address, task);
    }

    public static NativeBattleEffect308Step AdvanceScript667990(NativeBattleEffect308Frame frame, uint address, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint pc = task.ReadDword(24);
        uint row = task.ReadDword(20);
        uint rows = task.ReadDword(16);
        uint model = task.ReadDword(12);
        while (true)
        {
            short opcode = Word308(ReadBytes(frame, pc, 2), 0);
            pc = checked(pc + 2);
            if (opcode is 0x1000 or 0x1001 or 0x1002 or 0x1003 or 0x1010 or 0x1011 or 0x1012 or 0x1013)
            {
                bool angular = opcode >= 0x1010;
                bool velocity = opcode is 0x1002 or 0x1003 or 0x1012 or 0x1013;
                bool add = opcode is 0x1001 or 0x1003 or 0x1011 or 0x1013;
                uint destination = velocity ? address + (angular ? 0x24u : 0x1Cu) : row + (angular ? 8u : 0u);
                for (int axis = 0; axis < 3; axis++)
                {
                    ushort value = BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, pc, 2).Read(0, 2));
                    pc = checked(pc + 2);
                    uint lane = checked(destination + (uint)(axis * 2));
                    if (add)
                    {
                        value = unchecked((ushort)(value + BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, lane, 2).Read(0, 2))));
                    }
                    frame = WriteBytes(frame, lane, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
                }
                continue;
            }
            switch (opcode)
            {
                case 0x1004:
                case 0x1014:
                {
                    uint velocity = address + (opcode == 0x1004 ? 0x1Cu : 0x24u);
                    uint destination = row + (opcode == 0x1004 ? 0u : 8u);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        uint lane = checked(destination + (uint)(axis * 2));
                        short value = Word308(ReadBytes(frame, lane, 2), 0);
                        short delta = Word308(ReadBytes(frame, checked(velocity + (uint)(axis * 2)), 2), 0);
                        frame = WriteBytes(frame, lane, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)(value + delta)))), 2);
                    }
                    if (opcode == 0x1004 && Word308(ReadBytes(frame, row + 2, 2), 0) > 0)
                    {
                        frame = WriteBytes(frame, row + 2, NativeBattleEffectArena.Clear(), 2);
                        short y = Word308(ReadBytes(frame, address + 0x1E, 2), 0);
                        frame = WriteBytes(frame, address + 0x1E, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)((y >> 2) - y)))), 2);
                    }
                    break;
                }
                case 0x1080:
                case 0x1081:
                case 0x1082:
                {
                    short selector = Word308(ReadBytes(frame, pc, 2), 0);
                    pc = checked(pc + 2);
                    uint source = unchecked(rows + (uint)(selector << 4));
                    int first = opcode == 0x1082 ? 8 : 0;
                    int end = opcode == 0x1080 ? 16 : first + 8;
                    for (int offset = first; offset < end; offset += 4)
                    {
                        frame = WriteBytes(frame, checked(row + (uint)offset), ReadBytes(frame, checked(source + (uint)offset), 4), 4);
                    }
                    break;
                }
                case 0x1090:
                    (frame, pc) = NativeBattleEffect308Fragments.Birth667DB0(frame, pc, model, rows, resources, inputs);
                    break;
                case 0x10A0:
                case 0x10A1:
                    (frame, pc) = NativeBattleEffect308Lower.BirthAct6684C0(frame, pc, model, rows, opcode == 0x10A1, resources, inputs);
                    break;
                case 0x10A8:
                    (frame, pc) = NativeBattleEffect308Lower.BirthParticle668970(frame, pc, model, rows, resources, inputs);
                    break;
                case 0x10A9:
                    (frame, pc) = NativeBattleEffect308Lower.BirthBurst668B20(frame, pc, model, rows, resources, inputs);
                    break;
                case 0x10B0:
                    (frame, pc) = NativeBattleEffect308Lower.SetBackground668D60(frame, pc, inputs, inputs.CapturePresentation ? NativeBattleOffsetBodyVisuals.At(308, frame.Effect.Pools, address, pc) : null);
                    break;
                case 0x10F0:
                {
                    ushort counter = BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(frame, pc, 2).Read(0, 2));
                    if (counter != 0)
                    {
                        frame = WriteBytes(frame, pc, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((ushort)(counter - 1)))), 2);
                        short relative = Word308(ReadBytes(frame, pc + 4, 2), 0);
                        pc = unchecked(pc + (uint)(relative * 2 - 2));
                    }
                    else
                    {
                        frame = WriteBytes(frame, pc, ReadBytes(frame, pc + 2, 2), 2);
                        pc = checked(pc + 6);
                    }
                    break;
                }
                case 0x1100:
                    return new(frame, 2);
                default:
                    return new(Write(frame, address, Read(frame, address).WriteDword(24, pc)), 0);
            }
        }
    }
}
