using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect83Controller
{
    public static NativeBattleEffect83Step AdvanceModel(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = NativeBattleEffect83Models.Select(frame, address, 0, resources);
                task = Read(frame, address).WriteDword(0x4C, frame.State.ReadDword(TargetAddress + 8)).WriteDword(0x50, frame.State.ReadDword(TargetAddress + 0xC)).WriteWord(0x4E, 0xFA80).WriteWord(0x30, (ushort)(Read(frame, address).ReadWord(0x30) | 0x20)).WriteWord(0x126, 0x20).WriteDword(0x114, 0x1000).WriteDword(0x118, 0x1000).WriteDword(0x11C, 0x1000).WriteWord(0x13A, 0x1000).WriteWord(0x13C, 0x1000);
                frame = UpdateModelColors(Write(frame, address, task), address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                short fadeOut = unchecked((short)(Signed(task, 0x13A) - 0x100));
                fadeOut = Math.Max((short)0, fadeOut);
                frame = Write(frame, address, task.WriteWord(0x13A, unchecked((ushort)fadeOut)).WriteWord(0x13C, unchecked((ushort)fadeOut)));
                frame = UpdateModelColors(frame, address, inputs);
                NativeBattleEffect83ModelStep first = NativeBattleEffect83Models.Advance(frame, address, false, resources);
                frame = first.Frame;
                if (first.Completed)
                {
                    frame = NativeBattleEffect83Models.Select(frame, address, 1, resources);
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x126, 0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                NativeBattleEffect83ModelStep second = NativeBattleEffect83Models.Advance(frame, address, false, resources);
                frame = second.Frame;
                if (second.Completed)
                {
                    frame = NativeBattleEffect83Models.Select(frame, address, 2, resources);
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x126, 0xFFE0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = NativeBattleEffect83Models.Advance(frame, address, true, resources).Frame;
                if (Signed(Read(frame, address), 0x136) >= 0x15)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 4:
                frame = NativeBattleEffect83Models.Advance(frame, address, true, resources).Frame;
                short fadeIn = unchecked((short)(Signed(Read(frame, address), 0x13A) + 0x100));
                task = Read(frame, address);
                if (fadeIn >= 0x1000)
                {
                    fadeIn = 0x1000;
                    frame = NextPhase(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5))), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x13A, unchecked((ushort)fadeIn)).WriteWord(0x13C, unchecked((ushort)fadeIn)));
                frame = UpdateModelColors(frame, address, inputs);
                break;
            case 5:
                break;
            default:
                throw new InvalidDataException("Effect83 owned model signed phase leaves its six actual handlers.");
        }
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(0x4C + axis * 2, unchecked((ushort)(task.ReadWord(0x4C + axis * 2) + task.ReadWord(0x124 + axis * 2))));
        }
        frame = Write(frame, address, task);
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            frame = NativeBattleEffect83Models.Draw79E780(frame, address, resources, inputs);
        }
        task = Read(frame, address);
        NativeBattleEffectArena matrix = TaskView(task, 0x70, 32);
        (byte[] bytes, bool[] known) = matrix.ReadSnapshot(0, 32);
        frame = frame with { Memory = frame.Memory.PublishEffectLight1D99AB0(new([.. bytes], [.. known])) };
        task = task.WriteDword(0x84, unchecked((uint)Signed(task, 0x4C))).WriteDword(0x88, unchecked((uint)Signed(task, 0x4E))).WriteDword(0x8C, unchecked((uint)Signed(task, 0x50)));
        return Finish(Write(frame, address, task), address);
    }

    public static NativeBattleEffect83Frame UpdateModelColors(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short transitionFactor = Signed(task, 0x13A);
        uint transition = inputs.ReadColor(frame, 0xB8B7D8);
        if (transitionFactor == 0)
        {
            task = task.WriteDword(0x58, transition).WriteByte(0x5B, 0).WriteByte(0x37, 0);
        }
        else
        {
            byte alpha = unchecked((byte)(255 - unchecked(255 * transitionFactor) / 4096));
            task = task.WriteByte(0x5B, 2).WriteByte(0x37, (byte)(alpha >> 2));
            for (int channel = 0; channel < 3; channel++)
            {
                int source = (byte)(transition >> (channel * 8));
                task = task.WriteByte(0x58 + channel, unchecked((byte)(source - unchecked(source * transitionFactor) / 4096)));
            }
        }
        uint normal = inputs.ReadColor(frame, 0xB8B9A8);
        short normalFactor = Signed(task, 0x13C);
        for (int channel = 0; channel < 3; channel++)
        {
            int source = (byte)(normal >> (channel * 8));
            task = task.WriteByte(0x5C + channel, unchecked((byte)(source - unchecked(source * normalFactor) / 4096)));
        }
        return Write(frame, address, task);
    }
}
