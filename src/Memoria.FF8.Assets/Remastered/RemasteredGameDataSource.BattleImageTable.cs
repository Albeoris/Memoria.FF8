using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Memoria.FF8.Assets.Remastered;

/// <summary>One native battle image selector, preserving both model and effect records.</summary>
public sealed record RemasteredBattleImageRecord(string Name, int Identity, int Member, int Kind, int X, int Y, int WidthWords, int Height, int Mode);

public static class RemasteredBattleImageTableReader
{
    public static ImmutableArray<RemasteredBattleImageRecord> Read(PEReader image)
    {
        if (image.PEHeaders.PEHeader is not { Magic: PEMagic.PE32 } || image.PEHeaders.CoffHeader.Machine != Machine.I386)
        {
            throw new InvalidDataException("Native battle image table requires its 32-bit x86 runtime profile.");
        }
        const uint address = 0x117733D8;
        const int recordCount = 122;
        byte[] records = ReadRange(image, address, recordCount * 32);
        ImmutableArray<RemasteredBattleImageRecord>.Builder result = ImmutableArray.CreateBuilder<RemasteredBattleImageRecord>(recordCount - 1);
        HashSet<(int Identity, int Member, int X, int Y, int WidthWords, int Height, int Mode)> selectors = [];
        for (int index = 0; index < recordCount; index++)
        {
            ReadOnlySpan<byte> record = records.AsSpan(index * 32, 32);
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(record);
            if (pointer == 0)
            {
                if (index != recordCount - 1)
                {
                    throw new InvalidDataException("Native battle image table terminates unexpectedly.");
                }
                return result.MoveToImmutable();
            }
            byte[] encoded = ReadRange(image, pointer, 32);
            int end = Array.IndexOf(encoded, (byte)0);
            if (end <= 0 || encoded.AsSpan(0, end).IndexOfAnyExcept("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_"u8) >= 0)
            {
                throw new InvalidDataException("Native battle image table has an invalid resource name.");
            }
            RemasteredBattleImageRecord entry = new(Encoding.ASCII.GetString(encoded, 0, end), BinaryPrimitives.ReadInt32LittleEndian(record[8..]), BinaryPrimitives.ReadInt32LittleEndian(record[12..]), BinaryPrimitives.ReadInt32LittleEndian(record[16..]), BinaryPrimitives.ReadUInt16LittleEndian(record[20..]), BinaryPrimitives.ReadUInt16LittleEndian(record[22..]), BinaryPrimitives.ReadUInt16LittleEndian(record[24..]), BinaryPrimitives.ReadUInt16LittleEndian(record[26..]), BinaryPrimitives.ReadInt32LittleEndian(record[28..]));
            if (!selectors.Add((entry.Identity, entry.Member, entry.X, entry.Y, entry.WidthWords, entry.Height, entry.Mode)))
            {
                throw new InvalidDataException("Native battle image table has a repeated selector.");
            }
            result.Add(entry);
        }
        throw new InvalidDataException("Native battle image table has no terminating record.");
    }

    private static byte[] ReadRange(PEReader image, uint address, int length)
    {
        PEHeader header = image.PEHeaders.PEHeader ?? throw new InvalidDataException("Native battle image table requires a PE image.");
        long rva = (long)address - checked((long)header.ImageBase);
        if (rva < 0 || rva > int.MaxValue || !image.PEHeaders.SectionHeaders.Any(section => rva >= section.VirtualAddress && rva + length <= (long)section.VirtualAddress + section.VirtualSize && rva + length <= (long)section.VirtualAddress + section.SizeOfRawData))
        {
            throw new InvalidDataException("Native battle image table leaves its stored PE section.");
        }
        try
        {
            return image.GetSectionData((int)rva).GetContent(0, length).ToArray();
        }
        catch (Exception exception) when (exception is BadImageFormatException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException("Native battle image table has a truncated PE range.", exception);
        }
    }
}

public sealed partial class RemasteredGameDataSource
{
    private ImmutableArray<RemasteredBattleImageRecord> battleImageTable;

    public ImmutableArray<RemasteredBattleImageRecord> ReadBattleImageTable()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (battleImageTable.IsDefault)
        {
            using FileStream file = OpenAuthenticatedRuntimeCode();
            using PEReader image = new(file);
            battleImageTable = RemasteredBattleImageTableReader.Read(image);
        }
        return battleImageTable;
    }
}
