using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGastricJuice128Controller
{
    public static (NativeBattleGastricJuice128Frame Frame, uint Point) StartTrail(NativeBattleGastricJuice128Frame frame, NativeBattleVector source, NativeBattleVector destination, short tag, short progress, short increment, short scale, int angle)
    {
        (frame, uint head) = AllocatePoint(frame, PointView(source), scale);
        if (head == 0)
        {
            return (frame, 0);
        }
        (frame, uint address) = Append(frame, 2, 0x6B8DC0);
        if (address == 0)
        {
            return (WriteArena(frame, head + 6, BitConverter.GetBytes(ushort.MaxValue)), 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[68]).WriteWord(12, 0).WriteWord(14, 1).WriteDword(0x10, head).WriteDword(0x14, head).WriteWord(0x18, unchecked((ushort)source.X)).WriteWord(0x1A, unchecked((ushort)source.Y)).WriteWord(0x1C, unchecked((ushort)source.Z)).WriteWord(0x1E, unchecked((ushort)progress)).WriteWord(0x20, unchecked((ushort)destination.X)).WriteWord(0x22, unchecked((ushort)destination.Y)).WriteWord(0x24, unchecked((ushort)destination.Z)).WriteWord(0x26, unchecked((ushort)increment)).WriteWord(0x48, unchecked((ushort)tag)).WriteWord(0x4A, unchecked((ushort)angle)).WriteWord(0x4C, unchecked((ushort)scale));
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(destination.X - source.X, destination.Y - source.Y, destination.Z - source.Z).Matrix;
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x28 + index * 2, unchecked((ushort)basis.Rotation[index]));
        }
        return (Write(frame, address, task), head);
    }

    public static (NativeBattleGastricJuice128Frame Frame, uint Address) AllocatePoint(NativeBattleGastricJuice128Frame frame, NativeBattleEffectArena point, short tag)
    {
        for (int index = 0; index < PointCount; index++)
        {
            uint address = checked(Working(frame) + PointsOffset + (uint)(index * PointStride));
            if (BitConverter.ToInt16(Arena(frame).Read(Offset(address + 6, 2), 2)) == -1)
            {
                frame = frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(point, 0, Arena(frame), Offset(address, 8), 8) } };
                frame = WriteArena(frame, address + 6, BitConverter.GetBytes(tag));
                frame = WriteArena(frame, address + 14, BitConverter.GetBytes((ushort)10));
                frame = WriteArena(frame, address + 32, BitConverter.GetBytes((ushort)2048));
                return (WriteArena(frame, address + 36, BitConverter.GetBytes(0u)), address);
            }
        }
        return (frame, 0);
    }
}
