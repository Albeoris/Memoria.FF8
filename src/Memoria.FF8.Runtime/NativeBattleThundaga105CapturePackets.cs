using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleThundaga105CaptureMemory([property: JsonRequired] NativeBattleEffectArena Statics)
{
    public const string PacketBank = "thundaga-capture";
    public const int Length = 108;
    public static NativeBattleThundaga105CaptureMemory FromColdProcess() => new(NativeBattleEffectArena.Clear());

    public byte[] Read(int offset, int count)
    {
        Statics.Validate();
        if (offset < 0 || count < 0 || offset > Length - count || Statics.Regions.Any(region => region.Offset > Length - region.Data.Length) || Statics.UnknownRegions.Any(region => region.Offset > Length - region.Length))
        {
            throw new InvalidDataException("Native Thundaga capture statics leave their actual108-byte owner.");
        }
        return Statics.Read(offset, count);
    }

    public NativeBattleThundaga105CaptureMemory Write(int offset, ReadOnlySpan<byte> bytes)
    {
        _ = Read(offset, bytes.Length);
        return this with { Statics = Statics.Write(offset, bytes) };
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleThundaga105CaptureMemory? Thundaga105Capture { get; init; }
}

public sealed record NativeBattleThundaga105CaptureWrite(NativeBattleEffectMemory Memory, NativeBattleStageState Stage, NativeBattleLogicalOrdering Ordering, int Cursor, int GeometryCount);

/// <summary>6D5620's physical restore/geometry/area/offset insertion, including the unlinked black packet.</summary>
public static class NativeBattleThundaga105CapturePackets
{
    public static NativeBattleThundaga105CaptureWrite Append6D5620(NativeBattleEffectMemory memory, NativeBattleStageState stage, NativeBattleLogicalOrdering ordering, int cursor, ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleThundaga105CaptureTransform capture, NativeBattleSummonColors colors)
    {
        NativeBattleThundaga105CaptureMemory retained = memory.Thundaga105Capture ?? throw new NotSupportedException("Native Thundaga capture requires its retained static packet bytes.");
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Thundaga capture requires its actual primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native Thundaga capture requires its actual alternate draw environment selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native Thundaga capture requires one of two physical draw environment buffers.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native Thundaga capture requires its retained DRAWENV records.");
        ImmutableArray<byte> environment = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        retained = retained.Write(0, NativeBattleDrawEnvironmentPackets.Restore45C0F0(retained.Read(0, 64).ToImmutableArray(), environment).AsSpan());
        Link(0);

        NativeBattleThundaga105CaptureStage draw = NativeBattleThundaga105CaptureGeometry.DrawGroup6D5620(stage, geometry, capture, colors);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        foreach (NativeBattleGeometryPrimitive primitive in draw.Packets)
        {
            byte[] data = new byte[checked(primitive.Words.Length * 4)];
            for (int index = 0; index < primitive.Words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 4), primitive.Words[index]);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = primitive.PhysicalWriteMask ?? throw new NotSupportedException("Native captured stage geometry requires its established physical write mask.");
            attempts.Add(new(allocated, data.ToImmutableArray(), mask, primitive));
            allocated = checked(allocated + data.Length);
        }
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(memory, cursor, ordering, new NativeBattleGfGeometryBatch(attempts.ToImmutable(), allocated));
        memory = written.Memory;
        ordering = written.Ordering;
        cursor = written.Cursor;
        byte[] black = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(black, 0x05000000);
        BinaryPrimitives.WriteUInt32LittleEndian(black.AsSpan(4), 0x28000000);
        BinaryPrimitives.WriteUInt32LittleEndian(black.AsSpan(12), 128);
        BinaryPrimitives.WriteUInt32LittleEndian(black.AsSpan(16), 128u << 16);
        BinaryPrimitives.WriteUInt32LittleEndian(black.AsSpan(20), 128u << 16 | 128);
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native capture black packet requires its actual effect arena.");
        NativeBattleLogicalOrdering.Builder blackAlias = new(ordering);
        blackAlias.InvalidatePacketRange(new("20DFAB8", 0, cursor), black.Length);
        ordering = blackAlias.ToImmutable();
        memory = memory with { Arena = arena.Write(cursor, black) };
        cursor = checked(cursor + black.Length);

        retained = retained.Write(0x60, NativeBattleDrawEnvironmentPackets.Area45C940(retained.Read(0x60, 12).ToImmutableArray(), 704, 256, 128, 128).AsSpan());
        Link(0x60);
        const short x = 704;
        const short y = 256;
        byte[] offsets = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(offsets, x);
        BinaryPrimitives.WriteInt16LittleEndian(offsets.AsSpan(2), y);
        retained = retained.Write(0x58, offsets);
        retained = retained.Write(0x48, NativeBattleDrawEnvironmentPackets.Offset45C9B0(retained.Read(0x48, 8).ToImmutableArray(), x, y).AsSpan());
        Link(0x48);
        return new(memory with { Thundaga105Capture = retained }, draw.Stage, ordering, cursor, draw.Packets.Length);

        void Link(int offset)
        {
            byte[] header = retained.Read(offset, 4);
            int length = checked(4 + 4 * header[3]);
            byte[] data = retained.Read(offset, length);
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleLogicalOrdering.Builder links = new(ordering);
            links.InvalidatePacketRange(new(NativeBattleThundaga105CaptureMemory.PacketBank, 0, offset), length);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(17, new(NativeBattleThundaga105CaptureMemory.PacketBank, 0, offset), words, 0, [0, 0, 0, 0]);
            if (linked)
            {
                retained = retained.Write(offset, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink));
            }
            ordering = links.ToImmutable();
        }
    }
}
