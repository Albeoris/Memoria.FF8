using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Memoria.FF8.Assets.Remastered;

/// <summary>Reads explicitly serialized initial guest bytes from a Remastered DAT image.</summary>
public sealed class RemasteredGuestImage
{
    private readonly List<Section> sections = [];
    private readonly byte[] coff;
    private readonly byte[] optional;
    private readonly byte[] importData;

    public RemasteredGuestImage(string path) : this(File.OpenRead(path), leaveOpen: false)
    {
    }

    internal RemasteredGuestImage(Stream file, bool leaveOpen, RemasteredGuestImage? authenticated = null)
    {
        using BinaryReader reader = new(file, Encoding.ASCII, leaveOpen);
        coff = Take(reader, 20);
        ushort sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(2));
        if (BinaryPrimitives.ReadUInt16LittleEndian(coff) != 0x14C || BinaryPrimitives.ReadUInt16LittleEndian(coff.AsSpan(16)) != 224)
        {
            throw new InvalidDataException("Unsupported Remastered DAT COFF header.");
        }
        optional = Take(reader, 224);
        if (BinaryPrimitives.ReadUInt16LittleEndian(optional) != 0x10B)
        {
            throw new InvalidDataException("Unsupported Remastered DAT optional header.");
        }
        ImageBase = BinaryPrimitives.ReadUInt32LittleEndian(optional.AsSpan(28));
        SizeOfImage = BinaryPrimitives.ReadUInt32LittleEndian(optional.AsSpan(56));
        if (SizeOfImage == 0 || (ulong)ImageBase + SizeOfImage > (ulong)uint.MaxValue + 1)
        {
            throw new InvalidDataException("Remastered guest image exceeds the 32-bit address space.");
        }
        uint serializedSectionCount = ReadUInt32(reader);
        if (serializedSectionCount != sectionCount || sectionCount == 0 || sectionCount > (file.Length - file.Position) / 52)
        {
            throw new InvalidDataException("Remastered DAT section counts disagree or exceed the file bounds.");
        }
        if (authenticated is not null && (!coff.AsSpan().SequenceEqual(authenticated.coff) || !optional.AsSpan().SequenceEqual(authenticated.optional)))
        {
            throw new InvalidDataException("Remastered DAT headers differ from the authenticated image.");
        }
        for (int index = 0; index < sectionCount; index++)
        {
            byte[] header = Take(reader, 40);
            string headerName = DecodeAscii(header.AsSpan(0, 8).TrimEnd((byte)0));
            uint virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
            uint rva = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
            uint expandedSize = ReadUInt32(reader);
            uint compressedSize = ReadUInt32(reader);
            if (authenticated is not null && (!header.AsSpan().SequenceEqual(authenticated.sections[index].Header) || expandedSize != authenticated.sections[index].Data.Length))
            {
                throw new InvalidDataException($"Remastered DAT section {headerName} differs from the authenticated section layout.");
            }
            uint extent = Math.Max(virtualSize, expandedSize);
            if (expandedSize > Array.MaxLength || rva > SizeOfImage || extent > SizeOfImage - rva || compressedSize > file.Length - file.Position)
            {
                throw new InvalidDataException("Remastered DAT section exceeds its serialized or guest image bounds.");
            }
            ulong end = (ulong)rva + extent;
            if (extent != 0 && sections.Any(section => section.Extent != 0 && rva < (ulong)section.Rva + section.Extent && section.Rva < end))
            {
                throw new InvalidDataException("Remastered DAT guest sections overlap.");
            }
            long compressedStart = file.Position;
            Section? stockSection = authenticated?.sections[index];
            byte[] data = ExpandSection(file, compressedSize, (int)expandedSize, stockSection?.SerializedCompression);
            byte[]? serializedCompression = null;
            if (authenticated is null)
            {
                file.Position = compressedStart;
                serializedCompression = Take(reader, checked((int)compressedSize));
            }
            string name = ReadText(reader);
            if (name != headerName)
            {
                throw new InvalidDataException("Remastered DAT serialized section names disagree.");
            }
            uint characteristics = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(36));
            sections.Add(new Section(rva, extent, virtualSize, characteristics, data, header, serializedCompression));
        }
        long importStart = file.Position;
        if (authenticated is not null && file.Length - importStart != authenticated.importData.Length)
        {
            throw new InvalidDataException("Remastered DAT import data length differs from the authenticated image.");
        }
        uint libraryCount = ReadCount(reader, 32);
        List<uint> iatAddresses = [];
        for (uint index = 0; index < libraryCount; index++)
        {
            Take(reader, 20);
            ReadText(reader);
            uint lookupCount = ReadCount(reader, 8);
            ReadSymbols(reader, lookupCount, null);
            uint iatCount = ReadCount(reader, 8);
            if (iatCount != lookupCount)
            {
                throw new InvalidDataException("Remastered DAT import vector counts disagree.");
            }
            ReadSymbols(reader, iatCount, iatAddresses);
        }
        uint finalCount = ReadCount(reader, 4);
        if (finalCount != iatAddresses.Count)
        {
            throw new InvalidDataException("Remastered DAT final IAT count disagrees with the import vectors.");
        }
        for (int index = 0; index < iatAddresses.Count; index++)
        {
            if (ReadUInt32(reader) != iatAddresses[index])
            {
                throw new InvalidDataException("Remastered DAT final IAT addresses disagree with the import vectors.");
            }
        }
        if (file.Position != file.Length)
        {
            throw new InvalidDataException("Unexpected trailing bytes in the Remastered DAT image.");
        }
        file.Position = importStart;
        importData = Take(reader, checked((int)(file.Length - importStart)));
        if (authenticated is not null && !importData.AsSpan().SequenceEqual(authenticated.importData))
        {
            throw new InvalidDataException("Remastered DAT import data differs from the authenticated image.");
        }
    }

    public uint ImageBase { get; }
    public uint SizeOfImage { get; }

    public byte[] Read(uint guestAddress, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (guestAddress < ImageBase || (ulong)guestAddress + (uint)count > (ulong)ImageBase + SizeOfImage)
        {
            throw new InvalidDataException("Guest read exceeds the Remastered image bounds.");
        }
        byte[] result = new byte[count];
        int copied = 0;
        while (copied < count)
        {
            ulong rva = (ulong)guestAddress + (uint)copied - ImageBase;
            Section section = sections.FirstOrDefault(section => section.Rva <= rva && rva < (ulong)section.Rva + (uint)section.Data.Length) ?? throw new InvalidDataException($"No explicit Remastered DAT bytes at guest address 0x{(ulong)guestAddress + (uint)copied:X8}.");
            int offset = (int)(rva - section.Rva);
            int length = Math.Min(count - copied, section.Data.Length - offset);
            section.Data.AsSpan(offset, length).CopyTo(result.AsSpan(copied));
            copied += length;
        }
        return result;
    }

    public byte[] ReadLoader(uint guestAddress, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (guestAddress < ImageBase || (ulong)guestAddress + (uint)count > (ulong)ImageBase + SizeOfImage)
        {
            throw new InvalidDataException("Guest loader read exceeds the Remastered image bounds.");
        }
        byte[] result = new byte[count];
        int copied = 0;
        while (copied < count)
        {
            ulong rva = (ulong)guestAddress + (uint)copied - ImageBase;
            Section section = sections.FirstOrDefault(section => section.Rva <= rva && rva < (ulong)section.Rva + section.Extent) ?? throw new InvalidDataException($"No Remastered loader section at guest address 0x{(ulong)guestAddress + (uint)copied:X8}.");
            int offset = checked((int)(rva - section.Rva));
            int length = checked((int)Math.Min((uint)(count - copied), section.Extent - (uint)offset));
            int mapped = Math.Clamp(section.Data.Length - offset, 0, length);
            if (mapped != 0)
            {
                section.Data.AsSpan(offset, mapped).CopyTo(result.AsSpan(copied));
            }
            copied += length;
        }
        return result;
    }

    private void ReadSymbols(BinaryReader reader, uint count, List<uint>? addresses)
    {
        for (uint index = 0; index < count; index++)
        {
            uint address = ReadUInt32(reader);
            ReadText(reader);
            if (addresses is not null)
            {
                if (address < ImageBase || (ulong)address + 4 > (ulong)ImageBase + SizeOfImage)
                {
                    throw new InvalidDataException("Remastered DAT IAT address exceeds the guest image bounds.");
                }
                addresses.Add(address);
            }
        }
    }

    private static byte[] ExpandSection(Stream file, uint compressedSize, int expandedSize, byte[]? stockCompression)
    {
        using ExactCompressedStream compressed = new(file, compressedSize);
        using ZLibStream inflater = new(compressed, CompressionMode.Decompress, true);
        byte[] data = new byte[expandedSize];
        int position = 0;
        while (position < data.Length)
        {
            int read = inflater.Read(data.AsSpan(position));
            if (read == 0)
            {
                throw new InvalidDataException("Remastered DAT zlib section is shorter than its declared expanded size.");
            }
            position += read;
        }
        if (inflater.ReadByte() != -1 || compressed.ReadPastEnd)
        {
            throw new InvalidDataException("Remastered DAT zlib section has an invalid expanded size, truncated stream or trailing data.");
        }
        if (compressed.Remaining != 0)
        {
            if (stockCompression is null || compressedSize != stockCompression.Length)
            {
                throw new InvalidDataException("Remastered DAT zlib section has an unexpected compressed suffix.");
            }
            // In-place recompression can leave the original compressed bytes beyond the new stream.
            ReadOnlySpan<byte> suffix = stockCompression.AsSpan(stockCompression.Length - (int)compressed.Remaining);
            Span<byte> buffer = stackalloc byte[4096];
            while (!suffix.IsEmpty)
            {
                int length = Math.Min(suffix.Length, buffer.Length);
                file.ReadExactly(buffer[..length]);
                if (!buffer[..length].SequenceEqual(suffix[..length]))
                {
                    throw new InvalidDataException("Remastered DAT zlib suffix differs from the authenticated stock bytes.");
                }
                suffix = suffix[length..];
            }
        }
        return data;
    }

    private static uint ReadUInt32(BinaryReader reader)
    {
        RequireRemaining(reader.BaseStream, 4);
        return reader.ReadUInt32();
    }

    private static uint ReadCount(BinaryReader reader, int minimumRecordSize)
    {
        uint count = ReadUInt32(reader);
        if (count > int.MaxValue || count > (ulong)(reader.BaseStream.Length - reader.BaseStream.Position) / (uint)minimumRecordSize)
        {
            throw new InvalidDataException("Remastered DAT record count exceeds the file bounds.");
        }
        return count;
    }

    private static string ReadText(BinaryReader reader)
    {
        uint length = ReadUInt32(reader);
        if (length > 32767)
        {
            throw new InvalidDataException("Remastered DAT string exceeds the supported serialized name length.");
        }
        return DecodeAscii(Take(reader, (int)length));
    }

    private static string DecodeAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
        {
            if (value == 0 || value > 127)
            {
                throw new InvalidDataException("Remastered DAT names must be ASCII without embedded NUL characters.");
            }
        }
        return Encoding.ASCII.GetString(bytes);
    }

    private static byte[] Take(BinaryReader reader, int count)
    {
        RequireRemaining(reader.BaseStream, count);
        return reader.ReadBytes(count);
    }

    private static void RequireRemaining(Stream stream, long count)
    {
        if (count > stream.Length - stream.Position)
        {
            throw new InvalidDataException("Truncated Remastered DAT image.");
        }
    }

    public bool IsExecutableAddress(uint address)
    {
        long rva = address - (long)ImageBase;
        return sections.Any(section => (section.Characteristics & 0x20000000) != 0 && rva >= section.Rva && rva < (long)section.Rva + section.Data.Length);
    }

    public bool IsWritableRange(uint address, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        long rva = address - (long)ImageBase;
        return sections.Any(section => (section.Characteristics & 0x80000000) != 0 && rva >= section.Rva && rva + count <= (long)section.Rva + section.VirtualSize);
    }

    internal void ValidateDataChanges(RemasteredGuestImage authenticated, IReadOnlyList<(uint Address, int Length)> allowed)
    {
        int unknownCount = 0;
        uint firstUnknown = 0;
        for (int index = 0; index < sections.Count; index++)
        {
            Section section = sections[index];
            ReadOnlySpan<byte> original = authenticated.sections[index].Data;
            ReadOnlySpan<byte> candidate = section.Data;
            uint sectionAddress = ImageBase + section.Rva;
            int compared = 0;
            foreach ((uint address, int length) in allowed)
            {
                if (address < sectionAddress || (ulong)address + (uint)length > (ulong)sectionAddress + (uint)candidate.Length)
                {
                    continue;
                }
                int offset = checked((int)(address - sectionAddress));
                CountUnknown(original, candidate, sectionAddress, compared, offset);
                compared = offset + length;
            }
            CountUnknown(original, candidate, sectionAddress, compared, candidate.Length);
        }
        if (unknownCount != 0)
        {
            throw new InvalidDataException($"Remastered DAT contains {unknownCount} changed bytes outside supported tables, starting at guest address 0x{firstUnknown:X8}.");
        }

        void CountUnknown(ReadOnlySpan<byte> original, ReadOnlySpan<byte> candidate, uint sectionAddress, int start, int end)
        {
            if (original[start..end].SequenceEqual(candidate[start..end]))
            {
                return;
            }
            for (int offset = start; offset < end; offset++)
            {
                if (original[offset] == candidate[offset])
                {
                    continue;
                }
                if (unknownCount == 0)
                {
                    firstUnknown = sectionAddress + (uint)offset;
                }
                unknownCount++;
            }
        }
    }

    private sealed record Section(uint Rva, uint Extent, uint VirtualSize, uint Characteristics, byte[] Data, byte[] Header, byte[]? SerializedCompression);

    // Single-byte input prevents inflater read-ahead from concealing a serialized zlib suffix.
    private sealed class ExactCompressedStream(Stream source, long length) : Stream
    {
        public long Remaining { get; private set; } = length;
        public bool ReadPastEnd { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }
            if (Remaining == 0)
            {
                ReadPastEnd = true;
                return 0;
            }
            int read = source.Read(buffer[..1]);
            if (read == 0)
            {
                throw new InvalidDataException("Truncated Remastered DAT compressed section.");
            }
            Remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
