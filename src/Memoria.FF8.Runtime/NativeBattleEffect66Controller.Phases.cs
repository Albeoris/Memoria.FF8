using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect66Controller
{
    public static (NativeBattleEffect66Frame Frame, uint Address) Spawn622CD0(NativeBattleEffect66Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect66 exhausted its mandatory child task bank.");
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, 0)), address);
    }

    public static NativeBattleEffect66Frame BirthEnvelope622CF0(NativeBattleEffect66Frame frame, short rise, short hold, short fall, short intensity)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, 0x622D30);
        if (address == 0)
        {
            throw new InvalidDataException("Effect66 exhausted its mandatory envelope task bank.");
        }
        return Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(0x10, unchecked((ushort)rise)).WriteWord(0x12, unchecked((ushort)hold)).WriteWord(0x14, unchecked((ushort)fall)).WriteWord(0x1C, unchecked((ushort)intensity)));
    }

    public static NativeBattleEffect66Step AdvanceEnvelope622D30(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 12);
        int rise = Signed(task, 0x10);
        int hold = Signed(task, 0x12);
        int fall = Signed(task, 0x14);
        int intensity = Signed(task, 0x1C);
        int color;
        if (age < rise)
        {
            color = intensity / rise * age;
        }
        else if (age - rise < hold)
        {
            color = intensity;
        }
        else if (age - rise - hold < fall)
        {
            color = intensity - intensity / fall * (age - rise - hold);
        }
        else
        {
            color = -1;
        }
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        if (color < 0)
        {
            return new(frame, 2);
        }
        return new(inputs.Execute(frame, new NativeBattleEffect66Rectangle571250(color, color, color)), 0);
    }

    public static NativeBattleEffect66Frame BirthWhite622DC0(NativeBattleEffect66Frame frame, int mode, short duration)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, mode != 0 ? 0x622E60u : 0x622E10u);
        if (address == 0)
        {
            throw new InvalidDataException("Effect66 exhausted its mandatory white task bank.");
        }
        return Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, unchecked((ushort)duration)));
    }

    public static NativeBattleEffect66Step AdvanceWhite(NativeBattleEffect66Frame frame, uint address, NativeBattleEffect66Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect66PcRender47CF50(0, 255, 255, 255, 63));
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = Write(frame, address, task.WriteWord(12, next));
        return new(frame, unchecked((short)next) >= Signed(task, 14) ? 2 : 0);
    }
}
