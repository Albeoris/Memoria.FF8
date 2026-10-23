namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath129Controller
{
    public static NativeBattleBreath129Frame SpawnStream6B61B0(NativeBattleBreath129Frame frame, uint directory, int index, NativeBattleBreath129Resources resources)
    {
        short offset = BitConverter.ToInt16(resources.Read(directory + 2u + checked((uint)index * 2), 2).AsSpan());
        return SpawnCurve(frame, checked(directory + (uint)(offset * 2)), 0x140, resources);
    }

    public static NativeBattleBreath129Frame SpawnCurve(NativeBattleBreath129Frame frame, uint source, short width, NativeBattleBreath129Resources resources)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort first).Draw(out ushort second);
        frame = frame with { Random = random };
        short intensity = unchecked((short)((first & 0x3FF) + (second & 0x1FF) + 256));
        short decay = unchecked((short)-(intensity >> 3));
        short index = BitConverter.ToInt16(resources.Read(source, 2).AsSpan());
        (frame, uint point) = AllocatePoint(frame, index, width, intensity, decay);
        if (point == 0)
        {
            return frame;
        }
        (frame, uint taskAddress) = Append(frame, 4, 0x6B62D0);
        if (taskAddress == 0)
        {
            return frame;
        }
        return Write(frame, taskAddress, Read(frame, taskAddress).WriteBytes(12, new byte[20]).WriteWord(12, 1).WriteWord(14, unchecked((ushort)width)).WriteWord(16, unchecked((ushort)intensity)).WriteWord(18, unchecked((ushort)decay)).WriteDword(20, source + 2).WriteDword(24, point).WriteDword(28, point));
    }

    public static (NativeBattleBreath129Frame Frame, uint Address) AllocatePoint(NativeBattleBreath129Frame frame, short index, short width, short intensity, short decay)
    {
        uint first = Working(frame) + 0x19C0;
        for (int slot = 0; slot < 256; slot++)
        {
            uint address = first + (uint)(slot * 28);
            if (ReadSigned(frame, address) != -1)
            {
                continue;
            }
            frame = StoreWord(frame, address, unchecked((ushort)index));
            frame = StoreWord(frame, address + 2, unchecked((ushort)width));
            frame = StoreWord(frame, address + 20, unchecked((ushort)intensity));
            frame = StoreWord(frame, address + 22, unchecked((ushort)decay));
            return (StoreDword(frame, address + 24, 0), address);
        }
        return (frame, 0);
    }

    public static (NativeBattleBreath129Frame Frame, int Result) AdvanceCurve(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        for (int iteration = 0; iteration < 4; iteration++)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            uint source = task.ReadDword(20);
            if (source == 0)
            {
                continue;
            }
            short index = StreamWord(resources, source);
            source += 2;
            frame = Write(frame, address, task.WriteDword(20, source));
            while (index < 0)
            {
                frame = SpawnCurve(frame, source, Signed(Read(frame, address), 14), resources);
                source = checked(Read(frame, address).ReadDword(20) + (uint)((-1 - index) * 2));
                index = StreamWord(resources, source);
                source += 2;
                frame = Write(frame, address, Read(frame, address).WriteDword(20, source));
            }
            short next = StreamWord(resources, source);
            if (next < 0)
            {
                next = StreamWord(resources, checked(source + (uint)(-next * 2)));
            }
            if (next == 0x7FFF)
            {
                if (Signed(Read(frame, address), 12) <= 3)
                {
                    return (FreeCurve(frame, address), 2);
                }
                frame = Write(frame, address, Read(frame, address).WriteDword(20, 0));
            }
            task = Read(frame, address);
            NativeCrtRandomState random = frame.Random.Draw(out ushort sample);
            frame = frame with { Random = random };
            short intensity = unchecked((short)(Signed(task, 14) + (sample * Signed(task, 14) >> 15)));
            (frame, uint point) = AllocatePoint(frame, index, intensity, Signed(task, 16), Signed(task, 18));
            if (point == 0)
            {
                return (FreeCurve(frame, address), 2);
            }
            frame = StoreDword(frame, task.ReadDword(28) + 24, point);
            frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(Signed(task, 12) + 1))).WriteDword(28, point));
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        uint cursor = current.ReadDword(24);
        while (cursor != 0)
        {
            short intensity = ReadSigned(frame, cursor + 20);
            if (intensity > 0)
            {
                intensity = unchecked((short)(intensity + ReadSigned(frame, cursor + 22)));
                frame = StoreWord(frame, cursor + 20, intensity > 0 ? unchecked((ushort)intensity) : (ushort)0);
            }
            cursor = ReadDword(frame, cursor + 24);
        }
        if (Signed(current, 12) >= 2)
        {
            short phase = ReadSigned(frame, Working(frame) + 0x16BA);
            frame = NativeBattleBreath129Geometry.DrawLinked6B6450(frame, current.ReadDword(24), phase, Signed(current, 12), resources, inputs);
        }
        cursor = Read(frame, address).ReadDword(24);
        while (ReadSigned(frame, cursor + 20) <= 16)
        {
            uint next = ReadDword(frame, cursor + 24);
            if (next == 0)
            {
                throw new InvalidDataException("Breath curve retirement reaches an absent native next point.");
            }
            if (ReadSigned(frame, next + 20) > 16)
            {
                break;
            }
            frame = StoreWord(frame, cursor, 0xFFFF);
            current = Read(frame, address);
            short count = unchecked((short)(Signed(current, 12) - 1));
            frame = Write(frame, address, current.WriteWord(12, unchecked((ushort)count)).WriteDword(24, next));
            if (count < 2)
            {
                return (frame, 2);
            }
            cursor = next;
        }
        return (frame, 0);
    }

    private static NativeBattleBreath129Frame FreeCurve(NativeBattleBreath129Frame frame, uint taskAddress)
    {
        uint cursor = Read(frame, taskAddress).ReadDword(24);
        while (cursor != 0)
        {
            frame = StoreWord(frame, cursor, 0xFFFF);
            cursor = ReadDword(frame, cursor + 24);
        }
        return frame;
    }

    private static short StreamWord(NativeBattleBreath129Resources resources, uint address) => BitConverter.ToInt16(resources.Read(address, 2).AsSpan());
}
