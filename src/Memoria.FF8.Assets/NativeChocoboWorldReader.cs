using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeChocoboWorldReader
{
    public static NativeChocoboWorldImport Read(ReadOnlySpan<byte> file)
    {
        string hash = Convert.ToHexString(SHA256.HashData(file));
        byte[] world;
        string format;
        int first;
        bool dual;
        if (file.Length == 514 && BinaryPrimitives.ReadUInt16LittleEndian(file) == 0x08FF)
        {
            world = file[2..].ToArray();
            format = "PC uncompressed";
            first = 0;
            dual = false;
        }
        else if (file.Length >= 5 && file.Length <= 1024 && BinaryPrimitives.ReadUInt32LittleEndian(file) == file.Length - 4)
        {
            byte[] decoded = Expand(file);
            if (BinaryPrimitives.ReadUInt16LittleEndian(decoded) != 0x08FF)
            {
                throw new InvalidDataException("Chocobo World save header is invalid or its write was interrupted.");
            }
            world = decoded.AsSpan(2).ToArray();
            format = "PC LZSS";
            first = 0;
            dual = false;
        }
        else if (file.Length == 0xE080 && IsDirectoryName(file.Slice(10, 20)))
        {
            world = file[128..].ToArray();
            format = "PlayStation MCS";
            first = 0x200;
            dual = true;
        }
        else if (file.Length == 0xE000 && IsWorldTitle(file))
        {
            world = file.ToArray();
            format = "PocketStation raw application";
            first = 0x200;
            dual = true;
        }
        else
        {
            throw new NotSupportedException("This Chocobo World container has not been identified. Use a native PC save or a Chocobo World MCS export.");
        }
        if (dual && (world[0] != (byte)'S' || world[1] != (byte)'C' || world[3] != 7))
        {
            throw new InvalidDataException("Chocobo World MCS application header is invalid.");
        }
        bool primaryValid = ValidState(world.AsSpan(first, 64));
        bool backupValid = dual && ValidState(world.AsSpan(first + 256, 64));
        if (!primaryValid && !backupValid)
        {
            throw new InvalidDataException("No structurally valid Chocobo World save bank is available.");
        }
        int bank = !primaryValid || (backupValid && BinaryPrimitives.ReadUInt32LittleEndian(world.AsSpan(first + 264)) > BinaryPrimitives.ReadUInt32LittleEndian(world.AsSpan(first + 8))) ? 1 : 0;
        ReadOnlySpan<byte> state = world.AsSpan(first + bank * 256, 64);
        int level = Bcd(state[1]);
        return new(format, hash, bank, BinaryPrimitives.ReadUInt32LittleEndian(state[8..]), BinaryPrimitives.ReadUInt32LittleEndian(state[0x28..]), Bcd(state[0x0D]) * 100 + Bcd(state[0x0C]), level == 0 ? 100 : level, Bcd(state[2]), Bcd(state[3]), state[0x2D], state[0], ImmutableArray.Create(Bcd(state[0x14]), Bcd(state[0x15]), Bcd(state[0x16]), Bcd(state[0x17])), ImmutableArray.CreateRange(state.ToArray()), ImmutableArray.CreateRange(world))
        {
            BackupRevision = dual ? BinaryPrimitives.ReadUInt32LittleEndian(world.AsSpan(first + (1 - bank) * 256 + 8)) : null,
            BackupStructurallyValid = dual && (bank == 0 ? backupValid : primaryValid)
        };
    }

    private static bool IsDirectoryName(ReadOnlySpan<byte> value)
    {
        string name = Encoding.ASCII.GetString(value).TrimEnd('\0');
        return name.Length == 17 && name.StartsWith("B", StringComparison.Ordinal) && name.EndsWith("C0426", StringComparison.Ordinal) && name[1..12].All(char.IsAsciiLetterOrDigit);
    }

    private static bool IsWorldTitle(ReadOnlySpan<byte> application)
    {
        ReadOnlySpan<byte> title = [0x82, 0x65, 0x82, 0x65, 0x82, 0x57, 0x81, 0x5E, 0x82, 0x62, 0x82, 0x88, 0x82, 0x8F, 0x82, 0x83, 0x82, 0x8F, 0x82, 0x82, 0x82, 0x8F, 0x81, 0x40, 0x82, 0x76, 0x82, 0x8F, 0x82, 0x92, 0x82, 0x8C, 0x82, 0x84];
        return application.Slice(4, title.Length).SequenceEqual(title);
    }

    private static bool ValidState(ReadOnlySpan<byte> state)
    {
        int[] bcdOffsets = [1, 2, 3, 4, 5, 0x0C, 0x14, 0x15, 0x16, 0x17];
        foreach (int offset in bcdOffsets)
        {
            if ((state[offset] & 15) > 9 || (state[offset] >> 4) > 9)
            {
                return false;
            }
        }
        return state[3] != 0 && Bcd(state[2]) <= Bcd(state[3]) && state[6] <= 6 && state[7] <= 5 && state[0x0D] <= 9 && state[0x2D] <= 3;
    }

    private static int Bcd(byte value) => (value >> 4) * 10 + (value & 15);

    private static byte[] Expand(ReadOnlySpan<byte> file)
    {
        byte[] output = new byte[514];
        byte[] window = new byte[4096];
        int input = 4;
        int position = 0;
        int cursor = 4078;
        while (input < file.Length && position < output.Length)
        {
            byte flags = file[input++];
            for (int bit = 0; bit < 8 && position < output.Length; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    if (input == file.Length)
                    {
                        throw new InvalidDataException("Chocobo World LZSS literal is truncated.");
                    }
                    byte value = file[input++];
                    output[position++] = value;
                    window[cursor] = value;
                    cursor = (cursor + 1) & 4095;
                }
                else
                {
                    if (input + 2 > file.Length)
                    {
                        throw new InvalidDataException("Chocobo World LZSS reference is truncated.");
                    }
                    int offset = file[input] | ((file[input + 1] & 0xF0) << 4);
                    int length = (file[input + 1] & 15) + 3;
                    input += 2;
                    if (position + length > output.Length)
                    {
                        throw new InvalidDataException("Chocobo World LZSS reference exceeds the world image.");
                    }
                    for (int index = 0; index < length; index++)
                    {
                        byte value = window[(offset + index) & 4095];
                        output[position++] = value;
                        window[cursor] = value;
                        cursor = (cursor + 1) & 4095;
                    }
                }
            }
        }
        if (input != file.Length || position != output.Length)
        {
            throw new InvalidDataException("Chocobo World LZSS must contain exactly one complete world image.");
        }
        return output;
    }
}
