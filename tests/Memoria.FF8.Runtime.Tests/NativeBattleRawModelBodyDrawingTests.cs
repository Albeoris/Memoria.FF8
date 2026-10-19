using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRawModelBodyDrawingTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleSummonColors Colors = new(0, 0, 1f / 4096, 6755401588539392d);

    [Fact]
    public void Explicit_embedded_viewport_clips_208_without_changing_existing_default_216()
    {
        NativeBattleRawMesh mesh = new([new(new(0, 210, 1000), 0)], []);
        NativeBattleCameraProjection projection = new(null, 1000, 0, 0);
        NativeBattleRawModelBodyDraw old = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0, 1, new byte[8].ToImmutableArray(), Colors);
        NativeBattleRawModelBodyDraw embedded = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0, 1, new byte[8].ToImmutableArray(), Colors, viewport: new(0, 0, 320, 208));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(old.ProjectedScratch.AsSpan().Slice(4)) & 0x2000000);
        Assert.Equal(0x2000000u, BinaryPrimitives.ReadUInt32LittleEndian(embedded.ProjectedScratch.AsSpan().Slice(4)) & 0x2000000);
        Assert.Equal(old.ProjectedScratch.AsSpan().Slice(0, 4).ToArray(), embedded.ProjectedScratch.AsSpan().Slice(0, 4).ToArray());
        Assert.Equal(8, embedded.ProjectedBytesWritten);
    }

    [Fact]
    public void Actual_ordering_table_base_is_forwarded_without_rewriting_packet_bytes()
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        NativeBattleRawMesh mesh = new([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0)], [new(false, polygon.ToImmutableArray())]);
        NativeBattleCameraProjection projection = new(null, 1000, 160, 108);
        NativeBattleRawModelBodyDraw old = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0x808080, 1, new byte[24].ToImmutableArray(), Colors);
        NativeBattleRawModelBodyDraw embedded = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, [Identity], projection, 0x808080, 1, new byte[24].ToImmutableArray(), Colors, viewport: new(0, 0, 320, 208), tableBaseBucket: 51, depthCue: -17);
        NativeBattleGeometryPrimitive original = Assert.Single(old.Packets.Attempts).Primitive!;
        NativeBattleGeometryPrimitive actual = Assert.Single(embedded.Packets.Attempts).Primitive!;
        Assert.Equal(original.Bucket + 34, actual.Bucket);
        Assert.Equal(original.Words, actual.Words);
        Assert.Equal(old.ProjectedScratch, embedded.ProjectedScratch);
    }
}
