using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    private static NativeBattleEffect68Frame Decoration(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs, int kind)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = Phase(task, 4);
        if (phase == 0)
        {
            if (kind == 2)
            {
                task = InitializeStat(task, Read(frame, task.ReadDword(0x18)), inputs.ReadDescriptor208(frame, task.ReadByte(0x2D)), resources);
            }
            else
            {
                task = Next(Scale(task, 4096).WriteWord(0x1C, unchecked((ushort)(kind == 0 ? -512 : -136))).WriteWord(0x1E, unchecked((ushort)(kind == 0 ? 0 : -60))).WriteWord(0x20, kind == 0 ? (ushort)1728 : (ushort)576).WriteDword(0x4C, kind == 0 ? 0x15934CCu : 0x159346Cu));
                if (kind == 0)
                {
                    task = task.WriteWord(0x46, 1024);
                }
            }
        }
        else if (phase == 1)
        {
            if (kind == 0)
            {
                task = task.WriteWord(0x46, unchecked((ushort)(task.ReadWord(0x46) - 128)));
                if (S(task, 0x46) <= 0)
                {
                    task = Next(task.WriteWord(0x46, 0));
                }
            }
            else if (kind == 1)
            {
                for (ushort index = 1; index < 8; index++)
                {
                    frame = Write(frame, address, task);
                    (frame, uint child) = Child(frame, 0x25FD6E0, 0x7DE200, 0x6C, address, inputs);
                    frame = Write(frame, child, Read(frame, child).WriteWord(0x62, index).WriteWord(0x60, 1));
                    task = Read(frame, address);
                }
                task = Next(task);
            }
            else
            {
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (S(task, 0x66) < 0)
                {
                    task = Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) & ~4)));
                }
            }
        }
        else if (phase == 2)
        {
            if (kind == 2)
            {
                int scale = unchecked((int)task.ReadDword(0x30) + 768);
                if (scale > S(task, 0x64))
                {
                    scale = S(task, 0x64);
                }
                task = task.WriteDword(0x30, unchecked((uint)scale));
            }
            if (W(frame, Exit) == 1)
            {
                task = Next(task.WriteWord(0x26, (ushort)(task.ReadWord(0x26) | 1)));
            }
        }
        if (kind == 2)
        {
            task = Angle(task, 0x68, 256);
            task = task.WriteWord(0x50, unchecked((ushort)(unchecked((short)(NativeBattleCameraMath.Sine(task.ReadWord(0x68)) + 4096)) / 6)));
        }
        frame = Write(frame, address, task);
        return DrawMesh(frame, address, resources, inputs, kind == 0);
    }

    public static NativeBattleModulePoolTask InitializeStat(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleEffectArena actor, NativeBattleEffect68Resources resources)
    {
        int index = S(task, 0x62);
        uint table = checked((task.ReadWord(0x60) == 0 ? 0x15954D0u : 0x1595520u) + (uint)(index * 10));
        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
        if ((uint)index <= 7)
        {
            if (index <= 7)
            {
                uint[] sources = [0x1593554, 0x1593554, 0x15935DC, 0x1593664, 0x15936EC, 0x1593774, 0x15937FC, 0x1593884];
                int value;
                int maximum;
                if (index <= 1)
                {
                    value = Math.Min(999, BinaryPrimitives.ReadInt32LittleEndian(actor.Read(0x18, 4)));
                    maximum = 999;
                }
                else
                {
                    maximum = index == 7 ? 25 : 127;
                    value = Math.Min(maximum, actor.Read(index == 7 ? 0xC3 : 0xBD + index - 2, 1)[0]);
                }
                task = task.WriteDword(0x4C, sources[index]).WriteWord(0x64, unchecked((ushort)(unchecked(value << 14) / maximum)));
            }
        }
        task = task.WriteDword(0x30, 0).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x1C, unchecked((ushort)(Table(resources, table) / 2 + S(parent, 0x1C)))).WriteWord(0x1E, unchecked((ushort)(Table(resources, table + 2) / 2 + S(parent, 0x1E)))).WriteWord(0x20, parent.ReadWord(0x20)).WriteWord(0x48, unchecked((ushort)Table(resources, table + 4))).WriteWord(0x66, unchecked((ushort)Table(resources, table + 6))).WriteWord(0x68, unchecked((ushort)Table(resources, table + 8)));
        return Next(task);
    }
}
