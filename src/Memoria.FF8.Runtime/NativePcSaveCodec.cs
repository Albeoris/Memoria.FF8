using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

/// <summary>The original PC size-prefixed LZSS envelope and duplicated save-map checksum.</summary>
public static class NativePcSaveCodec
{
    public static NativePcSaveData Decode(ReadOnlySpan<byte> file)
    {
        if (file.Length < 5 || BinaryPrimitives.ReadUInt32LittleEndian(file) != file.Length - 4)
        {
            throw new InvalidDataException("Original PC save compressed length is invalid.");
        }
        byte[] block = new byte[NativePcSaveData.BlockSize];
        byte[] window = new byte[4096];
        int source = 4;
        int target = 0;
        int cursor = 4078;
        while (source < file.Length && target < block.Length)
        {
            byte flags = file[source++];
            for (int bit = 0; bit < 8 && target < block.Length; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    if (source >= file.Length)
                    {
                        throw new InvalidDataException("Original PC save has a truncated LZSS literal.");
                    }
                    byte value = file[source++];
                    block[target++] = value;
                    window[cursor] = value;
                    cursor = (cursor + 1) & 4095;
                }
                else
                {
                    if (source + 2 > file.Length)
                    {
                        throw new InvalidDataException("Original PC save has a truncated LZSS reference.");
                    }
                    int offset = file[source] | ((file[source + 1] & 0xF0) << 4);
                    int count = (file[source + 1] & 15) + 3;
                    source += 2;
                    if (target + count > block.Length)
                    {
                        throw new InvalidDataException("Original PC save expands beyond its fixed save image.");
                    }
                    for (int index = 0; index < count; index++)
                    {
                        byte value = window[(offset + index) & 4095];
                        block[target++] = value;
                        window[cursor] = value;
                        cursor = (cursor + 1) & 4095;
                    }
                }
            }
        }
        if (source != file.Length || target != block.Length)
        {
            throw new InvalidDataException("Original PC save does not contain exactly one complete save image.");
        }
        NativePcSaveData result = new(block);
        result.Validate();
        ushort checksum = Checksum(block.AsSpan(NativePcSaveData.MainOffset, 4944));
        if (BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(384)) != checksum || BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(5408)) != checksum)
        {
            throw new InvalidDataException("Original PC save checksum does not match both stored copies.");
        }
        return result;
    }

    public static byte[] Encode(NativePcSaveData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.Validate();
        byte[] block = data.Block.ToArray();
        ushort checksum = Checksum(block.AsSpan(NativePcSaveData.MainOffset, 4944));
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(384), checksum);
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(5408), checksum);
        using MemoryStream compressed = new();
        compressed.Write(new byte[4]);
        int source = 0;
        while (source < block.Length)
        {
            long flagPosition = compressed.Position;
            compressed.WriteByte(0);
            byte flags = 0;
            for (int bit = 0; bit < 8 && source < block.Length; bit++)
            {
                (int offset, int length) = FindMatch(block, source);
                if (length >= 3)
                {
                    compressed.WriteByte((byte)offset);
                    compressed.WriteByte((byte)(((offset >> 4) & 0xF0) | (length - 3)));
                    source += length;
                }
                else
                {
                    flags |= (byte)(1 << bit);
                    compressed.WriteByte(block[source++]);
                }
            }
            long end = compressed.Position;
            compressed.Position = flagPosition;
            compressed.WriteByte(flags);
            compressed.Position = end;
        }
        byte[] file = compressed.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(file, (uint)(file.Length - 4));
        return file;
    }

    private static (int Offset, int Length) FindMatch(byte[] block, int source)
    {
        int bestLength = 2;
        int bestOffset = 0;
        int maximum = Math.Min(18, block.Length - source);
        for (int distance = 1; distance <= 4096 && bestLength < maximum; distance++)
        {
            int candidate = source - distance;
            int length = 0;
            // Negative positions are the decoder's zero-filled initial ring. For an
            // overlapping reference, earlier output in this token is already available.
            while (length < maximum && block[source + length] == (candidate + length < 0 ? (byte)0 : block[candidate + length]))
            {
                length++;
            }
            if (length > bestLength)
            {
                bestLength = length;
                bestOffset = (4078 + candidate) & 4095;
            }
        }
        return (bestOffset, bestLength);
    }

    public static ushort Checksum(ReadOnlySpan<byte> bytes)
    {
        ushort crc = ushort.MaxValue;
        foreach (byte value in bytes)
        {
            // The original lookup table's final entry is zero, unlike a generated CCITT table.
            if (((crc >> 8) ^ value) == 255)
            {
                crc = unchecked((ushort)(crc << 8));
                continue;
            }
            crc ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++)
            {
                crc = unchecked((ushort)((crc << 1) ^ ((crc & 0x8000) != 0 ? 0x1021 : 0)));
            }
        }
        return unchecked((ushort)~crc);
    }
}
