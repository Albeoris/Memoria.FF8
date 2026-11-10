using System.Buffers.Binary;
using System.Text;

namespace Memoria.FF7.Formats;

public static class KernelTextArchive
{
    public static IReadOnlyList<ReadOnlyMemory<byte>> Read(ReadOnlySpan<byte> source, int maximumDecodedBytes = 4 * 1024 * 1024)
    {
        byte[] decoded = Lzss.DecodeLengthFramed(source, maximumDecodedBytes);
        BinaryCursor cursor = new(decoded);
        List<ReadOnlyMemory<byte>> sections = new(18);
        for (int index = 0; index < 18; index++)
        {
            uint length = cursor.U32();
            if (length > int.MaxValue)
            {
                throw new InvalidDataException("A kernel text section exceeds its supported length.");
            }
            sections.Add(cursor.Take((int)length).ToArray());
        }
        if (cursor.Remaining != 0)
        {
            throw new InvalidDataException("The kernel text archive has data after its eighteen sections.");
        }
        return sections.AsReadOnly();
    }

    public static IReadOnlyList<string> ReadPlainStrings(ReadOnlySpan<byte> source, int count, int maximumStringBytes = 65536)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumStringBytes);
        if (count > source.Length / 2)
        {
            throw new InvalidDataException("The kernel text offset directory is truncated.");
        }
        byte[] data = source.ToArray();
        List<string> strings = new(count);
        for (int index = 0; index < count; index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(index * 2));
            List<byte> expanded = new();
            if (!Expand(offset, data.Length, 0))
            {
                throw new InvalidDataException("A kernel text string has no native terminator.");
            }
            StringBuilder text = new();
            for (int position = 0; position < expanded.Count; position++)
            {
                byte code = expanded[position];
                if (code == 0xFF)
                {
                    break;
                }
                if (code < 0xE0)
                {
                    text.Append(FieldText.DecodeEnglishGlyph(code));
                }
                else if (code == 0xF8)
                {
                    if (++position >= expanded.Count)
                    {
                        throw new InvalidDataException("A kernel color token is truncated.");
                    }
                }
                else
                {
                    throw new NotSupportedException($"Kernel plaintext token {code:X2} requires its native dynamic substitution owner.");
                }
            }
            strings.Add(text.ToString());

            bool Expand(int start, int end, int depth)
            {
                if (start < count * 2 || start >= data.Length || end < start || end > data.Length || depth >= 64)
                {
                    throw new InvalidDataException("A kernel text dictionary reference is invalid or recursive.");
                }
                for (int position = start; position < end;)
                {
                    byte code = data[position++];
                    if (code == 0xF9)
                    {
                        if (position >= end)
                        {
                            throw new InvalidDataException("A kernel text dictionary token is truncated.");
                        }
                        byte packed = data[position++];
                        int length = ((packed >> 6) * 2) + 4;
                        int reference = position - (packed & 63) - 3;
                        if (Expand(reference, reference + length, depth + 1))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        Append(code);
                        if (code == 0xFF)
                        {
                            return true;
                        }
                        int operands = code is >= 0xEA and <= 0xF0 ? 2 : code == 0xF8 ? 1 : 0;
                        if (operands > end - position)
                        {
                            throw new InvalidDataException("A kernel text control token is truncated.");
                        }
                        for (int operand = 0; operand < operands; operand++)
                        {
                            Append(data[position++]);
                        }
                    }
                }
                return false;
            }

            void Append(byte value)
            {
                if (expanded.Count >= maximumStringBytes)
                {
                    throw new InvalidDataException("A kernel text string exceeds its expansion limit.");
                }
                expanded.Add(value);
            }
        }
        return strings.AsReadOnly();
    }
}
