namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTonberry90ParticleServices(NativeBattleActionPacketsState Packets, Func<NativeBattleTonberry90Frame, uint, byte> ReadSchedule, Func<NativeBattleTonberry90Frame, uint, NativeBattleTonberry90Frame> Draw);

public static partial class NativeBattleTonberry90Creature
{
    public static (NativeBattleTonberry90Frame Frame, bool Active) Spawn7671D0(NativeBattleTonberry90Frame frame, uint address, uint schedule, short index, short divisor, NativeBattleTonberry90ParticleServices services)
    {
        byte count = services.ReadSchedule(frame, unchecked(schedule + (uint)(int)index));
        if (count == 0x7F)
        {
            return (frame, false);
        }
        for (int particle = 0; particle < count; particle++)
        {
            var appended = NativeBattleTonberry90Director.AppendTask(frame, 0x259F098, 0x767290, 0x7C, address, services.Packets);
            frame = appended.Frame;
            uint childAddress = appended.Address;
            NativeCrtRandomState random = frame.Random.Draw(out ushort angle);
            random = random.Draw(out ushort radiusRandom);
            frame = frame with { Random = random };
            angle &= 0xFFF;
            int remainder = radiusRandom % divisor;
            NativeBattleModulePoolTask parent = Read(frame, address);
            short radius = unchecked((short)(parent.ReadWord(0x78) + remainder));
            NativeBattleModulePoolTask child = Read(frame, childAddress).WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
            int x = unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096;
            int z = unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096;
            child = child.WriteWord(0x1C, unchecked((ushort)(child.ReadWord(0x1C) + x))).WriteWord(0x20, unchecked((ushort)(child.ReadWord(0x20) + z)));
            frame = Write(frame, childAddress, child);
        }
        return (frame, true);
    }

    public static NativeBattleTonberry90TaskStep Advance767290(NativeBattleTonberry90Frame frame, uint address, NativeBattleTonberry90ParticleServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool moving = task.Callback == 0x767C70;
        if (task.Callback is not (0x767290 or 0x767C70))
        {
            throw new InvalidDataException("Native Tonberry sprite requires its selected current callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = Next(task.WriteDword(0x4C, moving ? 0x1547344u : 0x1547280u).WriteWord(0x56, 0).WriteWord(0x52, moving ? (ushort)9 : (ushort)6));
                if (moving)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = frame with { Random = frame.Random.Draw(out ushort random) };
                        int remainder = random % 768;
                        int velocity = axis switch { 0 => remainder - 384, 1 => -640 - remainder, _ => remainder + 640 };
                        task = task.WriteWord(0x6C + axis * 2, unchecked((ushort)velocity));
                    }
                }
                break;
            case 1:
                if (moving)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short velocity = unchecked((short)(Word(task, 0x6C + axis * 2) + (axis == 1 ? 96 : 0)));
                        velocity = unchecked((short)(velocity - velocity / 8));
                        task = task.WriteWord(0x6C + axis * 2, unchecked((ushort)velocity)).WriteWord(0x1C + axis * 2, unchecked((ushort)(task.ReadWord(0x1C + axis * 2) + velocity / 16)));
                    }
                }
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (Word(task, 0x50) > Word(task, 0x52))
                {
                    task = Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry sprite signed phase {unchecked((sbyte)task.ReadByte(0x29))} has no current callback.");
        }
        frame = services.Draw(Write(frame, address, task), address);
        task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        frame = Write(frame, address, task);
        return new(frame, (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }
}
