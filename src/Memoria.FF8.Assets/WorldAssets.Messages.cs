using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed partial class WorldAssets
{
    private readonly Dictionary<(int Window, int Message), NativeWorldMessage> worldMessageBindings = [];
    private readonly Dictionary<byte, byte[]> fuelMessageBytes = [];

    public NativeWorldMessage ReadNativeFuelMessage(byte remaining)
    {
        return remaining == 0 ? ReadNativeWorldMessage(1, 43) : ReadNativeWorldMessage(1, 43) with { MessageId = 254, FuelRemaining = remaining };
    }

    public byte[] ReadFuelMessageBytes(byte remaining)
    {
        if (fuelMessageBytes.TryGetValue(remaining, out byte[]? cached))
        {
            return cached.ToArray();
        }
        if (remaining == 0)
        {
            byte[] empty = ExpandWorldItemNames(ReadWorldMessageBytes(43));
            fuelMessageBytes.Add(0, empty);
            return empty.ToArray();
        }
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        int rows = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 8 * 4));
        int names = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 39 * 4));
        int namesEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + 40 * 4));
        int name = checked(names + BinaryPrimitives.ReadUInt16LittleEndian(kernel.AsSpan(rows + (162 - 33) * 4)));
        int zero = name >= names && name < namesEnd && namesEnd <= kernel.Length ? Array.IndexOf(kernel, (byte)0, name, namesEnd - name) : -1;
        if (zero < 0)
        {
            throw new InvalidDataException("Fuel item name is outside its original kernel text section.");
        }
        List<byte> result = [];
        byte digitZero = ReadWorldMessageBytes(29)[0];
        byte[] count = remaining.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => checked((byte)(digitZero + digit - '0'))).ToArray();
        ReadOnlySpan<byte> section = wmset.AsSpan(sections[33], sections[34] - sections[33]);
        int position = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section));
        bool terminated = false;
        while (position < section.Length)
        {
            byte token = section[position++];
            if (token != 10)
            {
                result.AddRange(ReadWorldMessageBytes(token).AsSpan()[..^1].ToArray());
                continue;
            }
            if (position >= section.Length)
            {
                break;
            }
            byte parameter = section[position++];
            if (parameter == 255)
            {
                terminated = true;
                break;
            }
            if (parameter == 32)
            {
                result.AddRange(kernel.AsSpan(name, zero - name).ToArray());
            }
            else if (parameter == 33)
            {
                result.AddRange(count);
            }
        }
        if (!terminated)
        {
            throw new InvalidDataException("Fuel message template has no bounded terminator.");
        }
        result.Add(0);
        byte[] bytes = ExpandWorldItemNames(result.ToArray());
        fuelMessageBytes.Add(remaining, bytes);
        return bytes.ToArray();
    }

    public string ReadFuelMessage(byte remaining)
    {
        byte[] bytes = ReadFuelMessageBytes(remaining);
        return NativeTextEncoding.Create(resources.Source.Language).GetString(bytes.AsSpan(0, bytes.Length - 1));
    }

    private byte[] ExpandWorldItemNames(byte[] source)
    {
        if (!source.Contains((byte)13))
        {
            return source;
        }
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        List<byte> result = [];
        for (int position = 0; position < source.Length; position++)
        {
            byte token = source[position];
            if (token != 13)
            {
                result.Add(token);
                continue;
            }
            //4B8B30 expands item tokens before measurement and the channel text runner.
            if (++position >= source.Length - 1 || source[position] < 32)
            {
                throw new InvalidDataException("World item substitution requires its unsigned item selector.");
            }
            int item = source[position] - 32;
            int recordSection = item < 33 ? 7 : 8;
            int textSection = item < 33 ? 38 : 39;
            int recordSize = item < 33 ? 24 : 4;
            int row = item < 33 ? item : item - 33;
            int records = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + recordSection * 4));
            ushort relative = BinaryPrimitives.ReadUInt16LittleEndian(kernel.AsSpan(records + row * recordSize));
            if (relative == ushort.MaxValue)
            {
                continue;
            }
            int names = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + textSection * 4));
            int namesEnd = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(8 + textSection * 4));
            int name = checked(names + relative);
            int zero = name >= names && name < namesEnd && namesEnd <= kernel.Length ? Array.IndexOf(kernel, (byte)0, name, namesEnd - name) : -1;
            if (zero < 0)
            {
                throw new InvalidDataException("World item substitution lies outside its original kernel text section.");
            }
            result.AddRange(kernel.AsSpan(name, zero - name).ToArray());
        }
        return result.ToArray();
    }

    public NativeWorldMessage ReadNativeWorldMessage(int window, int message)
    {
        if (window is < 0 or > 12 || message is < 0 or > 255)
        {
            throw new NotSupportedException("World message binding requires an original window0..12 and unsigned message BYTE.");
        }
        if (worldMessageBindings.TryGetValue((window, message), out NativeWorldMessage? cached))
        {
            return cached;
        }
        _ = ReadWorldMessage(message);
        ReadOnlySpan<byte> record = NativeWorldMemory.Read(0xC761A0u + (uint)window * 16, 8);
        NativeWorldMessage result = new(window, (byte)message, record[2], record[1], record[3], BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]), Convert.ToHexString(SHA256.HashData(wmset)), false);
        result.Validate();
        worldMessageBindings.Add((window, message), result);
        return result;
    }

    public string ReadWorldMessage(int message)
    {
        byte[] bytes = ReadWorldMessageBytes(message);
        return NativeTextEncoding.Create(resources.Source.Language).GetString(bytes.AsSpan(0, bytes.Length - 1));
    }

    public short ReadWorldMessageSpeed(byte speed)
    {
        if (speed > 4)
        {
            throw new NotSupportedException("World message speed requires the original settings index0..4.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(NativeWorldMemory.Read(0xB8EE94u + (uint)speed * 2, 2));
    }

    public byte[] ReadWorldMessageBytes(int message)
    {
        //542DA0 binds20400C4 to zero-based section13;5438D0 reads its relative DWORD offsets.
        ReadOnlySpan<byte> section = wmset.AsSpan(sections[13], sections[14] - sections[13]);
        int tableBytes = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section));
        if (tableBytes < 4 || tableBytes % 4 != 0 || tableBytes > section.Length || message < 0 || message >= tableBytes / 4)
        {
            throw new InvalidDataException("Original world message index or pointer table is invalid.");
        }
        int start = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(section.Slice(message * 4, 4)));
        if (start < tableBytes || start >= section.Length)
        {
            throw new InvalidDataException("Original world message pointer is outside its text section.");
        }
        int end = section.Slice(start).IndexOf((byte)0);
        if (end < 0)
        {
            throw new InvalidDataException("Original world message has no bounded text terminator.");
        }
        return section.Slice(start, end + 1).ToArray();
    }
}
