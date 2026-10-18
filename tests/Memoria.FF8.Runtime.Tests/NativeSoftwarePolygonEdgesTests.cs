using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwarePolygonEdgesTests
{
    [Fact]
    public void FlatTopTriangleDeliversFinalRowsEvenWhenBothChainsConsumeTheLastBudget()
    {
        byte[] image = Image();
        NativeSoftwarePolygonDescriptor polygon = Polygon(0, [new(0, 0, 0, 0), new(4 << 16, 0, 0, 0), new(2 << 16, 2, 0, 0)]);
        NativeSoftwarePolygonRasterState state = Draw(image, State(), polygon);
        Assert.Same(polygon, state.PublishedDescriptor);
        Assert.Null(state.PublishedClippedDescriptor);
        Assert.Equal(0x001F001Fu, state.Color555Pair);
        Assert.Equal(0x001F001Fu, U32(state.Edges.Left.Current, 0x20));
        Assert.Equal(0, state.SegmentY);
        Assert.Equal(0u, U32(state.Edges.Right.Current, 0x20));
        Assert.Equal(new ushort[] { 31, 31, 31, 31, 0xA5A5 }, Enumerable.Range(0, 5).Select(index => Pixel(image, index, 0)));
        Assert.Equal(new ushort[] { 0xA5A5, 31, 31, 0xA5A5 }, Enumerable.Range(0, 4).Select(index => Pixel(image, index, 1)));
        Assert.Equal((ushort)0xA5A5, Pixel(image, 2, 2));
        Assert.Equal(2 << 16, I32(state.Edges.Left.Current, 0));
        Assert.Equal(2 << 16, I32(state.Edges.Right.Current, 0));
    }

    [Fact]
    public void FlatTopAndBottomHorizontalEdgesConsumeTheSharedBudgetAndRetainPhysicalSegmentAlias()
    {
        NativeSoftwarePolygonDescriptor polygon = Polygon(0, [new(0, 0, 0, 0), new(4 << 16, 0, 0, 0), new(4 << 16, 2, 0, 0), new(0, 2, 0, 0)], 255, 128, 64);
        byte[] image = Image();
        NativeSoftwarePolygonRasterState before = State();
        NativeSoftwarePolygonRasterState state = Draw(image, before, polygon);
        Assert.Equal(2, state.SegmentY);
        Assert.Equal(0x221F221Fu, state.Color555Pair);
        Assert.All(Enumerable.Range(0, 2), y => Assert.All(Enumerable.Range(0, 4), x => Assert.Equal((ushort)0x221F, Pixel(image, x, y))));
        Assert.Equal(before.Edges.Left.Current.AsSpan().Slice(4, 0x1C).ToArray(), state.Edges.Left.Current.AsSpan().Slice(4, 0x1C).ToArray());
        Assert.Equal(before.Edges.Right.Current.AsSpan().Slice(4, 0x1C).ToArray(), state.Edges.Right.Current.AsSpan().Slice(4, 0x1C).ToArray());
        Assert.Equal(before.Edges.Left.Step.AsSpan().Slice(4).ToArray(), state.Edges.Left.Step.AsSpan().Slice(4).ToArray());
    }

    [Fact]
    public void AllHorizontalEdgesStillRunSetupAndCountBelowThreeDoesNotPublishOrReadTheRestOfHeader()
    {
        byte[] image = Image();
        byte[] before = image.ToArray();
        NativeSoftwarePolygonRasterState state = Draw(image, State(), Polygon(0, [new(0, 0, 0, 0), new(1 << 16, 0, 0, 0), new(2 << 16, 0, 0, 0)]));
        Assert.Equal(before, image);
        Assert.Equal(1 << 16, I32(state.Edges.Left.Current, 0));
        Assert.Equal(0, I32(state.Edges.Left.Step, 0));
        foreach (int count in new[] { 0, 1, 2 })
        {
            byte[] countOnly = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(countOnly, count);
            NativeSoftwarePolygonRasterState unchanged = NativeSoftwarePolygonEdges.Rasterize463DF0([], state, new NativeSoftwarePolygonDescriptor(countOnly.ToImmutableArray()), new(default), -1, -1, -1);
            Assert.Same(state, unchanged);
            Assert.Same(state.PublishedDescriptor, unchanged.PublishedDescriptor);
        }
    }

    [Fact]
    public void TexturedPathComposesExistingUvEdgesAndOpaqueWriterAndRejectsOtherSelectedWriters()
    {
        NativeSoftwareRasterVertex[] vertices = [new(0, 0, 0, 0), new(4 << 16, 0, 4096, 0), new(0, 2, 0, 0)];
        byte[] image = Image();
        NativeSoftwarePolygonRasterState state = Draw(image, State(), Polygon(2, vertices));
        Assert.Equal(new ushort[] { 0x1111, 0x2222, 0x3333, 0x4444 }, Enumerable.Range(0, 4).Select(index => Pixel(image, index, 0)));
        Assert.Equal((ushort)0x1111, Pixel(image, 0, 1));
        Assert.Equal((ushort)0x2222, Pixel(image, 1, 1));
        Assert.Equal(0, state.SegmentY);
        Assert.Throws<NotSupportedException>(() => Draw(Image(), State(), Polygon(6, vertices, variant: 1)));
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonEdges.Rasterize463DF0(Image(), State(), Polygon(2, vertices), NativeSoftwareReciprocalTable.Initialize463280(), 0, 8192, 12288));
    }

    [Fact]
    public void BothClockwiseOrdersAreDeliveredWithoutReorderingOrBackfaceAdmissionAndFlatZeroIsOpaque()
    {
        NativeSoftwareRasterVertex[] first = [new(0, 0, 0, 0), new(4 << 16, 0, 0, 0), new(2 << 16, 2, 0, 0)];
        NativeSoftwareRasterVertex[] reverse = [first[1], first[0], first[2]];
        byte[] a = Image();
        byte[] b = Image();
        Draw(a, State(), Polygon(4, first, 0, 0, 0, 255));
        Draw(b, State(), Polygon(4, reverse, 0, 0, 0, 255));
        Assert.Equal(a, b);
        Assert.Equal((ushort)0, Pixel(a, 1, 0));
        Assert.Equal((ushort)0xA5A5, Pixel(a, 4, 0));
    }

    [Fact]
    public void MalformedRecordsOutOfDomainYAndBacktrackingHeightsFailWithoutAlternateGeometry()
    {
        byte[] header = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(header, 3);
        Assert.Throws<InvalidDataException>(() => Draw(Image(), State(), new(header.ToImmutableArray())));
        Assert.Throws<NotSupportedException>(() => Draw(Image(), State(), Polygon(0, [new(0, 9999999, 0, 0), new(1 << 16, 9999999, 0, 0), new(0, 9999999, 0, 0)])));
        Assert.Throws<NotSupportedException>(() => Draw(Image(), State(), Polygon(0, [new(0, 0, 0, 0), new(1 << 16, 2, 0, 0), new(2 << 16, 1, 0, 0), new(3 << 16, 3, 0, 0)])));
        NativeSoftwarePolygonRasterState shortOwner = State() with { Edges = new(new(new byte[20].ToImmutableArray(), new byte[20].ToImmutableArray()), State().Edges.Right) };
        Assert.Throws<InvalidDataException>(() => Draw(Image(), shortOwner, Polygon(0, [new(0, 0, 0, 0), new(1 << 16, 0, 0, 0), new(0, 1, 0, 0)])));
    }

    [Fact]
    public void ConsumedClipOverloadUsesTheSameChainsWithoutFabricatingUnknownRawVertexBytes()
    {
        NativeSoftwarePolygonDescriptor raw = Polygon(0, [new(0, 0, 0, 0), new(4 << 16, 0, 0, 0), new(2 << 16, 2, 0, 0)]);
        NativeSoftwareClippedPolygon consumed = new(raw.Data.Slice(4, 8), [new(0, 0, null, null, null, null, null), new(4 << 16, 0, null, null, null, null, null), new(2 << 16, 2, null, null, null, null, null)]);
        byte[] a = Image();
        byte[] b = Image();
        NativeSoftwarePolygonRasterState direct = Draw(a, State(), raw);
        NativeSoftwarePolygonRasterState clipped = NativeSoftwarePolygonEdges.Rasterize463DF0(b, State(), consumed, NativeSoftwareReciprocalTable.Initialize463280(), -1, -1, -1);
        Assert.Equal(a, b);
        Assert.Equal(JsonSerializer.Serialize(direct.Edges), JsonSerializer.Serialize(clipped.Edges));
        Assert.Null(clipped.PublishedDescriptor);
        Assert.Same(consumed, clipped.PublishedClippedDescriptor);
        Assert.Null(clipped.PublishedClippedDescriptor!.Vertices[0].U1024);
        Assert.DoesNotContain("\"Data\":", JsonSerializer.Serialize(clipped));
        NativeSoftwareClippedPolygon missingUv = consumed with { HeaderTail = consumed.HeaderTail.SetItem(0, 2) };
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonEdges.Rasterize463DF0(Image(), State(), missingUv, NativeSoftwareReciprocalTable.Initialize463280(), 1, 8192, 12288));
    }

    [Fact]
    public void IndependentWholeCallRestorePreservesPublishedOwnerPhysicalAliasesAndNextPolygon()
    {
        NativeSoftwarePolygonDescriptor first = Polygon(0, [new(0, 0, 0, 0), new(4 << 16, 0, 0, 0), new(4 << 16, 2, 0, 0), new(0, 2, 0, 0)]);
        byte[] image = Image();
        Saved saved = new(image, Draw(image, State(), first));
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved))!;
        Assert.Equal(saved.State.Color555Pair, restored.State.Color555Pair);
        Assert.Equal(saved.State.SegmentY, restored.State.SegmentY);
        NativeSoftwarePolygonDescriptor second = Polygon(2, [new(1 << 16, 2, 0, 0), new(5 << 16, 2, 4096, 0), new(3 << 16, 4, 2048, 0)]);
        NativeSoftwarePolygonRasterState a = Draw(saved.Image, saved.State, second);
        NativeSoftwarePolygonRasterState b = Draw(restored.Image, restored.State, second);
        Assert.Equal(saved.Image, restored.Image);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
        Assert.DoesNotContain("PublishedClippedDescriptor", JsonSerializer.Serialize(a));
    }

    private static NativeSoftwarePolygonRasterState Draw(byte[] image, NativeSoftwarePolygonRasterState state, NativeSoftwarePolygonDescriptor polygon) => NativeSoftwarePolygonEdges.Rasterize463DF0(image, state, polygon, NativeSoftwareReciprocalTable.Initialize463280(), 1, 8192, 12288);
    private static NativeSoftwarePolygonRasterState State()
    {
        NativeSoftwareEdgeBuffers edge = new(Enumerable.Repeat((byte)0xA5, 0x28).ToImmutableArray(), Enumerable.Repeat((byte)0xB6, 0x28).ToImmutableArray());
        return new(new(edge, edge), null);
    }
    private static NativeSoftwarePolygonDescriptor Polygon(byte flags, NativeSoftwareRasterVertex[] vertices, byte red = 255, byte green = 0, byte blue = 0, byte variant = 0)
    {
        byte[] data = Enumerable.Repeat((byte)0xC7, 12 + vertices.Length * 32).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(data, vertices.Length);
        data[4] = flags;
        data[8] = red;
        data[9] = green;
        data[10] = blue;
        data[11] = variant;
        for (int index = 0; index < vertices.Length; index++)
        {
            int offset = 12 + index * 32;
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), vertices[index].X16_16);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 4), vertices[index].Y);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0xC), vertices[index].U1024);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x10), vertices[index].V1024);
        }
        return new(data.ToImmutableArray());
    }
    private static byte[] Image()
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 16384).ToArray();
        for (int index = 0; index < 4; index++)
        {
            image[8192 + index] = (byte)(index + 1);
            BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(12288 + (index + 1) * 2), (ushort)((index + 1) * 0x1111));
        }
        return image;
    }
    private static ushort Pixel(byte[] image, int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(y * 2048 + x * 2, 2));
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static int I32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private sealed record Saved(byte[] Image, NativeSoftwarePolygonRasterState State);
}
