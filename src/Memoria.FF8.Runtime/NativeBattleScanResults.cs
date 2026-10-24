using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanResultMemory([property: JsonRequired] NativeBattleScanByteOwner Element8, [property: JsonRequired] NativeBattleScanByteOwner Pages32, [property: JsonRequired] uint Count, [property: JsonRequired] byte Empty, [property: JsonRequired] NativeBattleScanByteOwner Scratch48, [property: JsonRequired] byte Cursor, [property: JsonRequired] NativeBattleScanByteOwner Ring768)
{
    public static NativeBattleScanResultMemory FromColdProcess() => new(Known(8), Known(32), 0, 0, Known(48), 0, Known(768));

    public void Validate()
    {
        Element8.RequireLength(8);
        Pages32.RequireLength(32);
        Scratch48.RequireLength(48);
        Ring768.RequireLength(768);
    }

    private static NativeBattleScanByteOwner Known(int length) => new(new byte[length].ToImmutableArray(), Enumerable.Repeat(true, length).ToImmutableArray());
}

public sealed record NativeBattleScanResultServices(Func<int, NativeBattleScanByteOwner> ReadDescriptor208, Func<int, ImmutableArray<byte>> ReadMiscText, Func<int, byte> ReadEnemyInfoFlags, Func<int, NativeBattleScanByteOwner> ReadEnemyExtra71);

public static class NativeBattleScanResults
{
    public const uint ScratchAddress = 0x1D2A248;
    public const uint RingAddress = 0x1D29034;
    public const uint EmptyAddress = 0x279CC94;
    public const uint EnemyExtraAddress = 0x1D28F28;

    public static NativeBattleScanResultMemory BuildB68390(NativeBattleScanResultMemory memory, int actor, NativeBattleScanResultServices services, uint emptyAddress = EmptyAddress)
    {
        memory.Validate();
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(services.ReadDescriptor208);
        ArgumentNullException.ThrowIfNull(services.ReadMiscText);
        ArgumentNullException.ThrowIfNull(services.ReadEnemyInfoFlags);
        ArgumentNullException.ThrowIfNull(services.ReadEnemyExtra71);
        actor &= 0xFF;
        NativeBattleScanByteOwner descriptor = services.ReadDescriptor208(actor);
        descriptor.RequireLength(208);
        memory = memory with { Count = 0 };
        ImmutableArray<byte> numeric = services.ReadMiscText(0x0B);
        if (numeric.IsDefault || numeric.Length <= 0x12)
        {
            throw new NotSupportedException("Native Scan number formatting requires its complete original encoded separator and digit table.");
        }
        byte separator = numeric[0];
        byte slash = numeric[0x12];
        bool hidden = (descriptor.ReadDword(0x7C) & 0x2000) != 0;
        ImmutableArray<byte> current = services.ReadMiscText(0x71);
        ImmutableArray<byte> maximum = current;
        if (!hidden)
        {
            uint hp = descriptor.ReadDword(0x18);
            uint max = descriptor.ReadDword(0x1C);
            if (unchecked((int)hp) < 100000)
            {
                current = Number(hp);
            }
            if (unchecked((int)max) < 100000)
            {
                maximum = Number(max);
            }
        }
        Concat(services.ReadMiscText(0x21), separator, Number(descriptor.ReadByte(0xBC)));
        Concat(ReadString(ScratchAddress), separator, services.ReadMiscText(0x22));
        Concat(ReadString(ScratchAddress), separator, current);
        Concat(ReadString(ScratchAddress), slash, maximum);
        StorePage();
        if (actor >= 3)
        {
            byte flags = services.ReadEnemyInfoFlags(actor);
            if ((flags & 3) != 0)
            {
                List<ImmutableArray<byte>> slots = [];
                if ((flags & 1) != 0)
                {
                    slots.Add(services.ReadMiscText(0x72));
                }
                if ((flags & 2) != 0)
                {
                    slots.Add(services.ReadMiscText(0x63));
                }
                slots.Add(services.ReadMiscText(0x24));
                while (slots.Count < 4)
                {
                    slots.Add(ReadString(emptyAddress));
                }
                ConcatSlots(slots);
                StorePage();
            }
        }
        if (!hidden)
        {
            for (int group = 0; group < 5; group++)
            {
                int count = 0;
                for (int element = 0; element < 8; element++)
                {
                    int value = descriptor.ReadWord(0x44 + 2 * element);
                    bool matches = group switch
                    {
                        0 => value <= 600,
                        1 => value > 600 && value < 800,
                        2 => value > 800 && value < 900,
                        3 => value == 900,
                        4 => value > 900,
                        _ => false
                    };
                    if (matches)
                    {
                        memory = memory with { Element8 = memory.Element8.WriteByte(count++, (byte)element) };
                    }
                }
                if (count == 0)
                {
                    continue;
                }
                List<ImmutableArray<byte>> slots = [services.ReadMiscText(0x25 + group)];
                for (int index = 0; index < count; index++)
                {
                    slots.Add(services.ReadMiscText(0x65 + memory.Element8.ReadByte(index)));
                }
                while (slots.Count < 9)
                {
                    slots.Add(ReadString(emptyAddress));
                }
                ConcatSlots(slots);
                StorePage();
            }
        }
        if (actor >= 3)
        {
            NativeBattleScanByteOwner extra = services.ReadEnemyExtra71(actor);
            extra.RequireLength(71);
            if (extra.ReadByte(0) != 0)
            {
                PublishPage(checked(EnemyExtraAddress + (uint)(71 * (actor - 3))));
            }
        }
        return memory;

        ImmutableArray<byte> ReadString(uint pointer)
        {
            ImmutableArray<byte>.Builder bytes = ImmutableArray.CreateBuilder<byte>();
            while (true)
            {
                byte value = ReadByte(memory, pointer, _ => throw new NotSupportedException("Native Scan result string leaves its actual owned scratch, ring or empty byte."), emptyAddress);
                bytes.Add(value);
                pointer = unchecked(pointer + 1);
                if (value == 0)
                {
                    return bytes.ToImmutable();
                }
            }
        }

        ImmutableArray<byte> Number(uint value) => Format495330(value, digit => numeric[1 + digit]);
        void Concat(ImmutableArray<byte> first, byte delimiter, ImmutableArray<byte> second) => memory = Concat495210(memory, first, delimiter, second);
        void ConcatSlots(List<ImmutableArray<byte>> slots)
        {
            Concat(slots[0], separator, slots[1]);
            for (int index = 2; index < slots.Count; index++)
            {
                Concat(ReadString(ScratchAddress), separator, slots[index]);
            }
        }
        void StorePage()
        {
            (memory, uint pointer) = Store495280(memory, ReadString(ScratchAddress));
            PublishPage(pointer);
        }
        void PublishPage(uint pointer)
        {
            memory = memory with { Pages32 = memory.Pages32.WriteDword(checked((int)memory.Count * 4), pointer), Count = unchecked(memory.Count + 1) };
        }
    }

    public static ImmutableArray<byte> Format495330(uint value, Func<int, byte> readDigit)
    {
        ArgumentNullException.ThrowIfNull(readDigit);
        uint remaining = value % 100000;
        int divisor = 10000;
        while (divisor > 1 && remaining / divisor == 0)
        {
            divisor /= 10;
        }
        ImmutableArray<byte>.Builder bytes = ImmutableArray.CreateBuilder<byte>();
        do
        {
            bytes.Add(readDigit((int)(remaining / divisor)));
            remaining %= (uint)divisor;
            divisor /= 10;
        }
        while (divisor != 0);
        bytes.Add(0);
        return bytes.ToImmutable();
    }

    public static NativeBattleScanResultMemory Concat495210(NativeBattleScanResultMemory memory, ImmutableArray<byte> first, byte separator, ImmutableArray<byte> second)
    {
        memory.Validate();
        int cursor = 0;
        Copy(first);
        if (separator != 7)
        {
            memory = memory with { Scratch48 = memory.Scratch48.WriteByte(cursor++, separator) };
        }
        Copy(second);
        return memory;

        void Copy(ImmutableArray<byte> bytes)
        {
            if (bytes.IsDefault)
            {
                throw new NotSupportedException("Native encoded concatenation requires its actual terminated source bytes.");
            }
            foreach (byte value in bytes)
            {
                memory = memory with { Scratch48 = memory.Scratch48.WriteByte(cursor, value) };
                if (value == 0)
                {
                    return;
                }
                cursor++;
            }
            throw new InvalidDataException("Native encoded concatenation source has no bounded terminator.");
        }
    }

    public static (NativeBattleScanResultMemory Memory, uint Pointer) Store495280(NativeBattleScanResultMemory memory, ImmutableArray<byte> source)
    {
        memory.Validate();
        if (source.IsDefault)
        {
            throw new NotSupportedException("Native encoded ring copy requires its actual terminated source bytes.");
        }
        int slot = memory.Cursor & 15;
        memory = memory with { Cursor = (byte)slot };
        for (int index = 0; index < source.Length; index++)
        {
            memory = memory with { Ring768 = memory.Ring768.WriteByte(slot * 48 + index, source[index]) };
            if (source[index] == 0)
            {
                return (memory with { Cursor = (byte)(slot + 1) }, RingAddress + (uint)(48 * slot));
            }
        }
        throw new InvalidDataException("Native encoded ring copy source has no bounded terminator.");
    }

    public static uint PageB68810(NativeBattleScanResultMemory memory, int actor, int index) => memory.Pages32.ReadDword((index & 0xFF) * 4);

    public static byte ReadByte(NativeBattleScanResultMemory memory, uint pointer, Func<uint, byte> readOriginalByte, uint emptyAddress = EmptyAddress)
    {
        uint scratch = unchecked(pointer - ScratchAddress);
        if (scratch < 48)
        {
            return memory.Scratch48.ReadByte((int)scratch);
        }
        uint ring = unchecked(pointer - RingAddress);
        if (ring < 768)
        {
            return memory.Ring768.ReadByte((int)ring);
        }
        if (pointer == ScratchAddress + 48)
        {
            return memory.Cursor;
        }
        return pointer == emptyAddress ? memory.Empty : readOriginalByte(pointer);
    }
}
