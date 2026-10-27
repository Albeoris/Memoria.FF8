using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBodyProjectionScratchTests
{
    [Fact]
    public void Each_visible_object_publishes_projection_even_without_polygons_and_retains_the_previous_longer_tail()
    {
        NativeBattleActionMatrix identity = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 0, 0, 0, 4096);
        NativeBattleRawMesh first = new([new(new(100, -50, 1024), 0), new(new(-100, 50, 1024), 0)], []);
        NativeBattleRawMesh second = new([new(new(0, 0, 128), 0)], []);
        NativeBattleRawMesh hidden = new([new(new(0, 0, 0), 99)], []);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        List<int> writes = [];
        ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleRawGeometryPackets.Emit5099D0([first, second, hidden], [identity], new(identity, 512, 160, 108), 0, 3, null, 17, 4, null, publishProjectedScratch: vertices =>
        {
            writes.Add(vertices.Count);
            staging = NativeBattleBodyProjectionScratch.Publish(staging, 3, vertices);
        });
        Assert.Empty(packets);
        Assert.Equal(new[] { 2, 1 }, writes);
        uint address = 0x196B018 - 24;
        byte[] bytes = staging.Read(address, 16);
        Assert.Equal(0x00020080u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(880, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(1064, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(10)));
        Assert.Equal(1024u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)));
        Assert.Throws<NotSupportedException>(() => staging.Read(address + 16, 8));
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging))!;
        Assert.Equal(bytes, restored.Read(address, 16));
        Assert.Throws<NotSupportedException>(() => restored.Read(address + 16, 8));
    }

    [Fact]
    public void Projection_refuses_an_overrun_and_an_empty_object_does_not_fabricate_a_writer()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        Assert.Same(staging, NativeBattleBodyProjectionScratch.Publish(staging, 1, []));
        Assert.Throws<InvalidDataException>(() => NativeBattleBodyProjectionScratch.Publish(staging, 1, new NativeBattleProjectedVertex[2]));
        Assert.Empty(NativeBattleStorageSnapshots.Capture(staging).KnownRanges);
    }
}
