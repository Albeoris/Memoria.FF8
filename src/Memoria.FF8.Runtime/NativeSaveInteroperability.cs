using System.Buffers.Binary;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A dormant native position, independent of the currently selected world return and its angle owner.</summary>
public sealed record NativeSaveDormantWorldPosition(int X, int RawZ, short Height);

/// <summary>Native file display, metadata and reserved storage that has no live gameplay authority.</summary>
public sealed record NativeSaveInteroperability
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? MemoryCardHeader { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ushort SaveCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint RecencySerial { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? HeaderAlignment { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? MainReserved3672 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? MainReserved3764 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? MainReserved3772 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? MainReserved3805 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? WorldReserved5188 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? Terrain109Baseline { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte WorldReserved5199 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? WorldReserved5214 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? CardAlignment5338 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte[]? Tail { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public NativeSaveDormantWorldPosition? DormantFootPosition { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public NativeSaveDormantWorldPosition? AlternateWorldPosition { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public NativeSaveDormantWorldPosition? ReservedWorldPosition6 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public NativeSaveDormantWorldPosition? ReservedWorldPosition7 { get; init; }

    public static byte[] CreateDefaultMemoryCardHeader()
    {
        byte[] header = new byte[384];
        header[0] = (byte)'S';
        header[1] = (byte)'C';
        header[2] = 0x11;
        header[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(80), 1);
        "CRD0"u8.CopyTo(header.AsSpan(82, 4));
        return header;
    }

    public static NativeSaveInteroperability Capture(NativePcSaveData original)
    {
        ArgumentNullException.ThrowIfNull(original);
        original.Validate();
        byte[] block = original.Block;
        byte[]? Lane(int offset, int length)
        {
            ReadOnlySpan<byte> lane = block.AsSpan(offset, length);
            int end = lane.Length;
            while (end != 0 && lane[end - 1] == 0)
            {
                end--;
            }
            return end == 0 ? null : lane[..end].ToArray();
        }
        NativeSaveDormantWorldPosition? Position(int offset)
        {
            int x = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset));
            int z = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(offset + 4));
            short height = BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(offset + 8));
            return x == 0 && z == 0 && height == 0 ? null : new(x, z, height);
        }
        return new()
        {
            MemoryCardHeader = block.AsSpan(0, 384).SequenceEqual(CreateDefaultMemoryCardHeader()) ? null : Lane(0, 384),
            SaveCount = BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(394)),
            RecencySerial = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(460)),
            HeaderAlignment = Lane(457, 3),
            MainReserved3672 = Lane(3672, 4),
            MainReserved3764 = Lane(3764, 4),
            MainReserved3772 = Lane(3772, 4),
            MainReserved3805 = Lane(3805, 3),
            WorldReserved5188 = Lane(5188, 4),
            Terrain109Baseline = block[5197],
            WorldReserved5199 = block[5199],
            WorldReserved5214 = Lane(5214, 2),
            CardAlignment5338 = Lane(5338, 2),
            Tail = Lane(5410, 2782),
            DormantFootPosition = Position(5088),
            AlternateWorldPosition = Position(5100),
            ReservedWorldPosition6 = Position(5160),
            ReservedWorldPosition7 = Position(5172)
        };
    }

    /// <summary>Writes only native interoperability lanes; current typed gameplay values are overlaid afterward.</summary>
    public void ApplyTo(byte[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Length != NativePcSaveData.BlockSize)
        {
            throw new InvalidDataException("Native interoperability requires one decoded native save image.");
        }
        Validate();
        void Lane(int offset, int length, byte[]? value)
        {
            block.AsSpan(offset, length).Clear();
            value?.CopyTo(block, offset);
        }
        void Position(int offset, NativeSaveDormantWorldPosition? value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(offset), value?.X ?? 0);
            BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(offset + 4), value?.RawZ ?? 0);
            BinaryPrimitives.WriteInt16LittleEndian(block.AsSpan(offset + 8), value?.Height ?? 0);
        }
        if (MemoryCardHeader is not null)
        {
            Lane(0, 384, MemoryCardHeader);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(394), SaveCount);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(460), RecencySerial);
        Lane(457, 3, HeaderAlignment);
        Lane(3672, 4, MainReserved3672);
        Lane(3764, 4, MainReserved3764);
        Lane(3772, 4, MainReserved3772);
        Lane(3805, 3, MainReserved3805);
        Lane(5188, 4, WorldReserved5188);
        if (Terrain109Baseline is { } terrain)
        {
            block[5197] = terrain;
        }
        block[5199] = WorldReserved5199;
        Lane(5214, 2, WorldReserved5214);
        Lane(5338, 2, CardAlignment5338);
        Lane(5410, 2782, Tail);
        Position(5088, DormantFootPosition);
        Position(5100, AlternateWorldPosition);
        Position(5160, ReservedWorldPosition6);
        Position(5172, ReservedWorldPosition7);
    }

    public void Validate()
    {
        static bool Invalid(byte[]? value, int maximum) => value is not null && (value.Length == 0 || value.Length > maximum || value[^1] == 0);
        if (Invalid(MemoryCardHeader, 384) || Invalid(HeaderAlignment, 3) || Invalid(MainReserved3672, 4) || Invalid(MainReserved3764, 4) || Invalid(MainReserved3772, 4) || Invalid(MainReserved3805, 3) || Invalid(WorldReserved5188, 4) || Invalid(WorldReserved5214, 2) || Invalid(CardAlignment5338, 2) || Invalid(Tail, 2782))
        {
            throw new InvalidDataException("Native interoperability contains an invalid named storage lane.");
        }
        if (MemoryCardHeader is { } header && (header.Length < 2 || header[0] != 'S' || header[1] != 'C'))
        {
            throw new InvalidDataException("Native interoperability contains an invalid memory card header.");
        }
    }
}
