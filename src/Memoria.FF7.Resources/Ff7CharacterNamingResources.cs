using System.Buffers.Binary;
using System.Reflection.PortableExecutable;

namespace Memoria.FF7.Resources;

public sealed class Ff7CharacterNamingResources
{
    private const uint InitializationAddress = 0x718A7A;
    private const uint CopyAddress = 0x718ADA;
    private const uint CompletionAddress = 0x719B81;
    private readonly byte[][] templates;

    private Ff7CharacterNamingResources(byte[] resetIdentities, byte[] skipJoinIdentities, byte[] saveRecordIds, byte[][] templates)
    {
        ResetIdentities = Array.AsReadOnly(resetIdentities);
        SkipJoinIdentities = Array.AsReadOnly(skipJoinIdentities);
        SaveRecordIds = Array.AsReadOnly(saveRecordIds);
        this.templates = templates;
    }

    public IReadOnlyList<byte> ResetIdentities { get; }
    public IReadOnlyList<byte> SkipJoinIdentities { get; }
    public IReadOnlyList<byte> SaveRecordIds { get; }
    public int RecordSize => 132;

    public static Ff7CharacterNamingResources Load(Ff7Installation installation)
    {
        return Ff7PcExecutableData.LoadCharacterNamingResources(installation);
    }

    /// <summary>Reads the supported native naming instruction layout from a bounded mapped-image accessor.</summary>
    public static Ff7CharacterNamingResources Read(Func<uint, int, byte[]> readVirtual)
    {
        ArgumentNullException.ThrowIfNull(readVirtual);
        byte[] initialization = ReadExact(InitializationAddress, 38);
        byte[] copy = ReadExact(CopyAddress, 55);
        byte[] completion = ReadExact(CompletionAddress, 54);
        uint selection = U32(initialization, 2);
        byte[] resets = new byte[2];
        for (int index = 0; index < resets.Length; index++)
        {
            int offset = index * 19;
            Match(initialization, offset, [0x83, 0x3D]);
            Require(U32(initialization, offset + 2) == selection);
            resets[index] = initialization[offset + 6];
            Match(initialization, offset + 7, [0x75, 10, 0x6A]);
            Require(initialization[offset + 10] == resets[index]);
            Match(initialization, offset + 11, [0xE8]);
            Require(RelativeTarget(InitializationAddress + (uint)offset + 16, initialization, offset + 12) == CopyAddress);
            Match(initialization, offset + 16, [0x83, 0xC4, 4]);
        }
        Require(resets.Distinct().Count() == resets.Length);
        Match(copy, 0, [0x55, 0x8B, 0xEC, 0x51, 0x56, 0x57, 0xC7, 0x45, 0xFC]);
        uint templateAddress = U32(copy, 9);
        Match(copy, 13, [0x8B, 0x45, 8, 0x69, 0xC0]);
        uint stride = U32(copy, 18);
        Require(stride == 132 && U32(copy, 32) == stride && U32(copy, 43) == stride / 4);
        Match(copy, 22, [0x8B, 0x75, 0xFC, 3, 0xF0, 0x8B, 0x7D, 8, 0x69, 0xFF]);
        Match(copy, 36, [0x81, 0xC7]);
        uint saveAddress = U32(copy, 38);
        Match(copy, 42, [0xB9]);
        Match(copy, 47, [0xF3, 0xA5, 0x5F, 0x5E, 0x8B, 0xE5, 0x5D, 0xC3]);
        Match(completion, 0, [0x55, 0x8B, 0xEC, 0x83, 0x3D]);
        Match(completion, 10, [0x74]);
        Match(completion, 12, [0x83, 0x3D]);
        Match(completion, 19, [0x74]);
        Require(U32(completion, 5) == selection && U32(completion, 14) == selection && U32(completion, 22) == selection);
        Require(12 + (sbyte)completion[11] == 128 && 21 + (sbyte)completion[20] == 128);
        byte[] skips = [completion[9], completion[18]];
        Require(skips.Distinct().Count() == skips.Length && skips.All(identity => identity < 11));
        Match(completion, 21, [0xA1]);
        Match(completion, 26, [0x8B, 0x0C, 0x85]);
        uint mapAddress = U32(completion, 29);
        Match(completion, 33, [0x69, 0xC9]);
        Require(U32(completion, 35) == stride);
        Match(completion, 39, [0x81, 0xC1]);
        Require(U32(completion, 41) == saveAddress);
        Match(completion, 45, [0x51, 0xE8]);
        Require(RelativeTarget(CompletionAddress + 51, completion, 47) == 0x5C7336);
        Match(completion, 51, [0x83, 0xC4, 4]);
        byte[] identityTable = ReadExact(mapAddress, 11 * 4);
        byte[] records = new byte[11];
        for (int identity = 0; identity < records.Length; identity++)
        {
            uint record = U32(identityTable, identity * 4);
            Require(record < 9);
            records[identity] = (byte)record;
        }
        Require(records.Take(9).Distinct().Count() == 9 && resets.All(identity => identity < records.Length && records[identity] == identity));
        byte[] nativeTemplates = ReadExact(templateAddress, 11 * 132);
        byte[][] templates = new byte[11][];
        for (int identity = 0; identity < templates.Length; identity++)
        {
            byte[] template = nativeTemplates.AsSpan(identity * 132, 132).ToArray();
            Require(template[0] == identity && template[1] is >= 1 and <= 99);
            templates[identity] = template;
        }
        return new(resets, skips, records, templates);

        byte[] ReadExact(uint address, int length)
        {
            byte[]? data = readVirtual(address, length);
            if (data is null || data.Length != length)
            {
                throw new InvalidDataException("A native naming resource lies outside its mapped image storage.");
            }
            return data;
        }
    }

    public bool ResetsRecord(byte identity)
    {
        ValidateIdentity(identity);
        return ResetIdentities.Contains(identity);
    }

    public bool SkipsJoin(byte identity)
    {
        ValidateIdentity(identity);
        return SkipJoinIdentities.Contains(identity);
    }

    public byte RecordId(byte identity)
    {
        ValidateIdentity(identity);
        return SaveRecordIds[identity];
    }

    public byte[] CreateTemplate(byte identity)
    {
        ValidateIdentity(identity);
        return templates[identity].ToArray();
    }

    private static void ValidateIdentity(byte identity)
    {
        if (identity >= 11)
        {
            throw new ArgumentOutOfRangeException(nameof(identity));
        }
    }

    private static uint U32(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    }

    private static uint RelativeTarget(uint next, byte[] data, int offset)
    {
        return unchecked(next + (uint)BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4)));
    }

    private static void Match(byte[] data, int offset, ReadOnlySpan<byte> expected)
    {
        Require(data.AsSpan(offset, expected.Length).SequenceEqual(expected));
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidDataException("The native character naming code or resource layout is unsupported.");
        }
    }
}

public sealed partial class Ff7PcExecutableData
{
    internal static Ff7CharacterNamingResources LoadCharacterNamingResources(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        if (pe.PEHeaders.PEHeader?.Magic != PEMagic.PE32)
        {
            throw new InvalidDataException("Native character naming requires the supported 32-bit image profile.");
        }
        return Ff7CharacterNamingResources.Read((address, count) => ReadVirtual(stream, pe, address, count));
    }
}
