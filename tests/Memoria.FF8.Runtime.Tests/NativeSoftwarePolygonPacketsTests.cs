using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwarePolygonPacketsTests
{
    [Theory]
    [InlineData(0x20, new int[] { 1, 2, 3 })]
    [InlineData(0x28, new int[] { 1, 2, 4, 3 })]
    [InlineData(0x24, new int[] { 1, 2, 3 })]
    [InlineData(0x2C, new int[] { 1, 2, 4, 3 })]
    [InlineData(0x34, new int[] { 1, 2, 3 })]
    [InlineData(0x3C, new int[] { 1, 2, 4, 3 })]
    public void SixLiteralLayoutsPreservePerimeterOrderUnsignedUvAndOnlyKnownConsumedAttributes(byte opcode, int[] order)
    {
        NativeSoftwarePolygonPacketResult result = Adapt(opcode, Packet(opcode));
        NativeSoftwareClippedPolygon polygon = result.Polygon!;
        Assert.Equal(order, polygon.Vertices.Select(vertex => vertex.X16_16 >> 16));
        Assert.Equal(order.Select(index => -index), polygon.Vertices.Select(vertex => vertex.Y));
        Assert.Equal(new byte[] { polygon.Flags, 0, 0, 0, 255, 0, 0, 0 }, polygon.HeaderTail.ToArray());
        bool textured = (opcode & 4) != 0;
        Assert.Equal(textured ? (byte)2 : (byte)0, polygon.Flags);
        Assert.All(polygon.Vertices, vertex => Assert.Null(vertex.Red));
        if (textured)
        {
            Assert.Equal(order.Select(index => (10 + index) << 10), polygon.Vertices.Select(vertex => vertex.U1024!.Value));
            Assert.Equal(order.Select(index => (250 + index) << 10), polygon.Vertices.Select(vertex => vertex.V1024!.Value));
        }
        else
        {
            Assert.All(polygon.Vertices, vertex => Assert.Null(vertex.U1024));
        }
    }

    [Fact]
    public void ProxyShiftAndSignedWordOffsetsUseMaskedShiftAndUncheckedDwordArithmetic()
    {
        Assert.Equal(13, NativeSoftwarePolygonPackets.CoordinateShift45D080(0));
        Assert.Equal(16, NativeSoftwarePolygonPackets.CoordinateShift45D080(1));
        byte[] packet = Packet(0x20);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(8), -3);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(10), -9);
        NativeSoftwareClippedPolygon small = NativeSoftwarePolygonPackets.Adapt(0x20, packet, 13, 7, 65535, Cache(), Table(), 16384).Polygon!;
        Assert.Equal((-3 << 13) + 7, small.Vertices[0].X16_16);
        Assert.Equal(-1, small.Vertices[0].Y);
        NativeSoftwareClippedPolygon wrapped = NativeSoftwarePolygonPackets.Adapt(0x20, packet, 48, int.MinValue, int.MaxValue, Cache(), Table(), 16384).Polygon!;
        Assert.Equal(unchecked((-3 << 16) + int.MinValue), wrapped.Vertices[0].X16_16);
        Assert.Equal(unchecked((-9 << 16) + int.MaxValue) >> 16, wrapped.Vertices[0].Y);
    }

    [Fact]
    public void PagePreparationPrecedesClutAndCacheHitRetainsAllDerivedOwnersRatherThanReconstructingThem()
    {
        NativeSoftwareTextureState captured = Cache() with { LastTPage = 0x80, TextureX = 333, TextureY = 444, TextureByteOffset = 555, Depth = 2, Abr = 3, PageId = 7, TexturePointerIsNull = false };
        NativeSoftwarePolygonPacketResult result = NativeSoftwarePolygonPackets.Adapt(0x24, Packet(0x24), 16, 0, 0, captured, Table(), 16384);
        Assert.Equal<int?>(555, result.Texture.TextureByteOffset);
        Assert.Equal<int?>(2, result.Texture.Depth);
        Assert.Equal((byte)3, result.Polygon!.Variant);
        Assert.Equal<int?>(12288, result.Texture.PaletteByteOffset);
        Assert.False(result.Texture.PalettePointerIsNull);
        Assert.False(result.Depth3Diagnostic);
        byte[] changed = Packet(0x24);
        BinaryPrimitives.WriteUInt16LittleEndian(changed.AsSpan(0x16), 0xF1);
        NativeSoftwarePolygonPacketResult refreshed = NativeSoftwarePolygonPackets.Adapt(0x24, changed, 16, 0, 0, captured, Table(), 1024 * 1024);
        Assert.Equal<int?>(64, refreshed.Texture.TextureX);
        Assert.Equal<int?>(256, refreshed.Texture.TextureY);
        Assert.Equal<int?>(524416, refreshed.Texture.TextureByteOffset);
        Assert.Equal((byte)3, refreshed.Polygon!.Variant);
        Assert.Equal<int?>(17, refreshed.Texture.PageId);
    }

    [Fact]
    public void KnownNativeNullUnknownImportAndRealVramOffsetZeroRemainDifferentPointerStates()
    {
        NativeSoftwareTextureState startup = NativeSoftwareTextureState.FromColdProcess();
        NativeSoftwareTexturePagePreparation hit = NativeSoftwareTexturePreparation.PrepareTPage45B960(startup, 0);
        Assert.Same(startup, hit.State);
        Assert.True(hit.State.TexturePointerIsNull);
        Assert.True(hit.State.PalettePointerIsNull);
        Assert.Equal<ushort?>(0, hit.State.EncodedClut);
        Assert.Equal<int?>(0, hit.State.Depth);
        Assert.Equal<byte?>(0, hit.State.Abr);
        Assert.Equal<int?>(0, hit.State.TextureX);
        Assert.Equal<int?>(0, hit.State.TextureY);
        Assert.Equal<int?>(0, hit.State.PageId);
        Assert.Null(hit.State.PaletteByteOffset);
        Assert.Null(hit.State.TextureByteOffset);
        NativeSoftwareTexturePagePreparation prepared = NativeSoftwareTexturePreparation.PrepareTPage45B960(startup, 0x80);
        Assert.False(prepared.State.TexturePointerIsNull);
        Assert.Equal<int?>(0, prepared.State.TextureByteOffset);
        Assert.Null(NativeSoftwareTextureState.Unknown().TexturePointerIsNull);
        Assert.Equal(JsonSerializer.Serialize(startup), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeSoftwareTextureState>(JsonSerializer.Serialize(startup))));
        NativeSoftwareTextureState unknownHit = Cache() with { LastTPage = 0x80 };
        byte[] packet = VisibleTriangle(0x24);
        Assert.Throws<NotSupportedException>(() => Draw(Image(), unknownHit, 0x24, packet));
    }

    [Fact]
    public void UnknownCachedPageCannotBeAssumedToMissEvenWithRetainedDerivedFields()
    {
        NativeSoftwareTextureState unknown = NativeSoftwareTextureState.Unknown() with { TextureX = 0, TextureY = 0, TextureByteOffset = 0, TexturePointerIsNull = false, Depth = 1, Abr = 0, PageId = 0 };
        string json = JsonSerializer.Serialize(unknown);
        NativeSoftwareTextureState restored = JsonSerializer.Deserialize<NativeSoftwareTextureState>(json)!;
        Assert.Throws<NotSupportedException>(() => NativeSoftwareTexturePreparation.PrepareTPage45B960(restored, 0));
        Assert.Throws<NotSupportedException>(() => NativeSoftwareTexturePreparation.PrepareTPage45B960(restored, 0x80));
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeSoftwareTextureState knownComparison = NativeSoftwareTextureState.Unknown() with { LastTPage = 0x80 };
        Assert.Same(knownComparison, NativeSoftwareTexturePreparation.PrepareTPage45B960(knownComparison, 0x80).State);
        Assert.Null(knownComparison.TexturePointerIsNull);
    }

    [Fact]
    public void Depth3DiagnosticOccursOnlyOnChangedPageAndClutUsesMaskedWordAndCompleteActualOwner()
    {
        NativeSoftwareTexturePagePreparation prepared = NativeSoftwareTexturePreparation.PrepareTPage45B960(Cache(), 0x180);
        Assert.True(prepared.Depth3Diagnostic);
        Assert.Equal<int?>(1, prepared.State.Depth);
        Assert.False(NativeSoftwareTexturePreparation.PrepareTPage45B960(prepared.State, 0x180).Depth3Diagnostic);
        NativeSoftwareTextureState clut = NativeSoftwareTexturePreparation.PrepareClut45B920(prepared.State, 0x8001, 1024);
        Assert.Equal<ushort?>(1, clut.EncodedClut);
        Assert.Equal<int?>(32, clut.PaletteByteOffset);
        Assert.Throws<NotSupportedException>(() => NativeSoftwareTexturePreparation.PrepareClut45B920(clut, 0x7FFF, 1024 * 1024));
    }

    [Theory]
    [InlineData(0x34, 0x10, 0x1C, -1)]
    [InlineData(0x3C, 0x10, 0x1C, 0x28)]
    public void GouraudAdditionalColorsAndTheirOpcodeBytesAreNeverRead(byte opcode, int second, int third, int fourth)
    {
        byte[] original = Packet(opcode);
        byte[] changed = original.ToArray();
        foreach (int offset in new[] { second, third, fourth }.Where(offset => offset >= 0))
        {
            BinaryPrimitives.WriteUInt32LittleEndian(changed.AsSpan(offset), 0xFEDCBA98);
        }
        Assert.Equal(JsonSerializer.Serialize(Adapt(opcode, original)), JsonSerializer.Serialize(Adapt(opcode, changed)));
    }

    [Fact]
    public void RawTextureBitIsIgnoredAndPalettePixelsAreWrittenWithoutColorModulation()
    {
        byte[] a = VisibleTriangle(0x24);
        byte[] b = a.ToArray();
        b[7] = 0x25;
        Assert.Equal(JsonSerializer.Serialize(Adapt(0x24, a)), JsonSerializer.Serialize(Adapt(0x25, b)));
        byte[] first = Image();
        byte[] second = Image();
        Draw(first, Cache(), 0x24, a);
        b[4] = 0;
        b[5] = 255;
        b[6] = 0;
        Draw(second, Cache(), 0x25, b);
        Assert.Equal(first, second);
        Assert.Equal((ushort)0x801F, BinaryPrimitives.ReadUInt16LittleEndian(first.AsSpan(4 * 2048)));
    }

    [Fact]
    public void UvBytesUseUnsigned255AndUntexturedSemiFlagStillUsesTheOpaqueFlatFill()
    {
        byte[] texture = Packet(0x24);
        texture[0xC] = 255;
        texture[0xD] = 255;
        NativeSoftwareClipVertex vertex = Adapt(0x24, texture).Polygon!.Vertices[0];
        Assert.Equal<int?>(255 << 10, vertex.U1024);
        Assert.Equal<int?>(255 << 10, vertex.V1024);
        byte[] flat = Packet(0x22);
        int[] positions = [8, 0xC, 0x10];
        (short x, short y)[] points = [(0, 4), (4, 4), (0, 6)];
        for (int index = 0; index < positions.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(flat.AsSpan(positions[index]), points[index].x);
            BinaryPrimitives.WriteInt16LittleEndian(flat.AsSpan(positions[index] + 2), points[index].y);
        }
        byte[] image = Image();
        NativeSoftwarePolygonPacketDraw result = Draw(image, Cache() with { Abr = 1 }, 0x22, flat);
        Assert.Equal((byte)4, result.Polygon!.Flags);
        Assert.Equal((ushort)31, BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(4 * 2048)));
    }

    [Fact]
    public void SemiVariantSelectsTheNativeWriterAndAllOutsideStillUpdatesTextureMetadata()
    {
        byte[] packet = VisibleTriangle(0x26);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(0x16), 0xE0);
        Assert.Equal((byte)6, Draw(Image(), Cache(), 0x26, packet).Polygon!.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(0x16), 0xA0);
        Assert.Equal<byte?>(1, Draw(Image(), Cache(), 0x26, packet).Texture.Abr);
        byte[] outside = Packet(0x26);
        BinaryPrimitives.WriteUInt16LittleEndian(outside.AsSpan(0x16), 0xA0);
        NativeSoftwarePolygonPacketDraw rejected = Draw(Image(), Cache(), 0x26, outside);
        Assert.Equal<byte?>(1, rejected.Texture.Abr);
        Assert.Equal<int?>(12288, rejected.Texture.PaletteByteOffset);
        Assert.Null(rejected.Raster.PublishedClippedDescriptor);
    }

    [Fact]
    public void LiteralGouraudNoopsReadNoPacketAndDoNotTouchImageCacheOrPublishedOwner()
    {
        foreach (byte opcode in new byte[] { 0x30, 0x31, 0x32, 0x33, 0x38, 0x39, 0x3A, 0x3B })
        {
            byte[] image = Image();
            byte[] before = image.ToArray();
            NativeSoftwareTextureState cache = NativeSoftwareTextureState.Unknown();
            NativeSoftwarePolygonRasterState raster = Raster();
            NativeSoftwarePolygonPacketDraw result = NativeSoftwarePolygonPackets.Draw(image, raster, cache, opcode, [], int.MinValue, int.MaxValue, int.MinValue, null!, Table(), new(default));
            Assert.Same(cache, result.Texture);
            Assert.Same(raster, result.Raster);
            Assert.Null(result.Polygon);
            Assert.Equal(before, image);
        }
    }

    [Fact]
    public void WrongIdentityTruncatedPacketAndActualDifferentOrUnknownHandlerFailWithoutFallback()
    {
        Assert.Throws<InvalidDataException>(() => Adapt(0x24, []));
        Assert.Throws<InvalidDataException>(() => Adapt(0x24, Packet(0x25)));
        NativeSoftwarePrimitiveTable crossed = Table() with { Handlers = Table().Handlers.SetItem(0x24, 0x466B10) };
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonPackets.Adapt(0x24, Packet(0x24), 16, 0, 0, Cache(), crossed, 16384));
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonPackets.Adapt(0xE1, [], 16, 0, 0, Cache(), Table(), 16384));
        Assert.Throws<InvalidDataException>(() => NativeSoftwarePolygonPackets.Adapt(0x24, Packet(0x24), 16, 0, 0, Cache(), new NativeSoftwarePrimitiveTable(default(ImmutableArray<uint>)), 16384));
    }

    [Fact]
    public void IndependentJsonCacheAndRasterRestorePreservesNextFullPacketDrawing()
    {
        byte[] image = Image();
        NativeSoftwarePolygonPacketDraw first = Draw(image, Cache(), 0x24, VisibleTriangle(0x24));
        Saved saved = new(image, first.Raster, first.Texture);
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved))!;
        NativeSoftwarePolygonPacketDraw next = Draw(saved.Image, saved.Texture, 0x24, VisibleTriangle(0x24), saved.Raster);
        NativeSoftwarePolygonPacketDraw cold = Draw(restored.Image, restored.Texture, 0x24, VisibleTriangle(0x24), restored.Raster);
        Assert.Equal(saved.Image, restored.Image);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold));
    }

    private static NativeSoftwarePolygonPacketResult Adapt(byte opcode, byte[] packet) => NativeSoftwarePolygonPackets.Adapt(opcode, packet, 16, 0, 0, Cache(), Table(), 16384);
    private static NativeSoftwarePolygonPacketDraw Draw(byte[] image, NativeSoftwareTextureState texture, byte opcode, byte[] packet, NativeSoftwarePolygonRasterState? raster = null) => NativeSoftwarePolygonPackets.Draw(image, raster ?? Raster(), texture, opcode, packet, 16, 0, 0, new(0, 0, 15 << 16, 15), Table(), NativeSoftwareReciprocalTable.Initialize463280());
    private static NativeSoftwareTextureState Cache() => NativeSoftwareTextureState.FromColdProcess();
    private static NativeSoftwarePolygonRasterState Raster()
    {
        NativeSoftwareEdgeBuffers edge = new(new byte[40].ToImmutableArray(), new byte[40].ToImmutableArray());
        return new(new(edge, edge), null);
    }
    internal static NativeSoftwarePrimitiveTable Table()
    {
        uint[] handlers = new uint[256];
        (int first, uint address)[] groups = [(0x20, 0x4667C0), (0x24, 0x4669F0), (0x28, 0x4668F0), (0x2C, 0x466B10), (0x30, 0x4669D0), (0x34, 0x466C70), (0x38, 0x4669E0), (0x3C, 0x466D90)];
        foreach (var group in groups)
        {
            for (int variant = 0; variant < 4; variant++)
            {
                handlers[group.first + variant] = group.address;
            }
        }
        return new(handlers.ToImmutableArray());
    }
    private static byte[] Packet(byte opcode)
    {
        (int[] xy, int[] uv, int page, int length) layout = (opcode & 0xFC) switch
        {
            0x20 => ([8, 0xC, 0x10], [], -1, 20),
            0x28 => ([8, 0xC, 0x10, 0x14], [], -1, 24),
            0x24 => ([8, 0x10, 0x18], [0xC, 0x14, 0x1C], 0x16, 32),
            0x2C => ([8, 0x10, 0x18, 0x20], [0xC, 0x14, 0x1C, 0x24], 0x16, 40),
            0x34 => ([8, 0x14, 0x20], [0xC, 0x18, 0x24], 0x1A, 40),
            0x3C => ([8, 0x14, 0x20, 0x2C], [0xC, 0x18, 0x24, 0x30], 0x1A, 52),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
        (int[] xy, int[] uv, int page, int length) = layout;
        byte[] packet = new byte[length];
        packet[4] = 255;
        packet[7] = opcode;
        for (int index = 0; index < xy.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(xy[index]), (short)(index + 1));
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(xy[index] + 2), (short)(-index - 1));
            if (uv.Length > 0)
            {
                packet[uv[index]] = (byte)(11 + index);
                packet[uv[index] + 1] = (byte)(251 + index);
            }
        }
        if (page >= 0)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(page), 0x80);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(0xE), 0x180);
        }
        return packet;
    }
    private static byte[] VisibleTriangle(byte opcode)
    {
        byte[] packet = Packet(opcode);
        int[] xy = [8, 0x10, 0x18];
        int[] uv = [0xC, 0x14, 0x1C];
        (short x, short y)[] points = [(0, 4), (4, 4), (0, 6)];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(xy[index]), points[index].x);
            BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(xy[index] + 2), points[index].y);
            packet[uv[index]] = 0;
            packet[uv[index] + 1] = 0;
        }
        return packet;
    }
    private static byte[] Image()
    {
        byte[] image = new byte[16384];
        image[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(12290), 0x801F);
        return image;
    }
    private sealed record Saved(byte[] Image, NativeSoftwarePolygonRasterState Raster, NativeSoftwareTextureState Texture);
}
