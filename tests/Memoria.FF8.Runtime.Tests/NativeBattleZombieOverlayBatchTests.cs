using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleZombieOverlayBatchTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static readonly NativeBattleCameraProjection Projection = new(null, 256, 160, 104);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BatchedStoresPreserveRejectedVertexBytesUnknownGapsAndPointers(bool known)
    {
        uint workspace = NativeBattleFileStaging.Address + 64;
        NativeBattleFileStaging source = NativeBattleFileStaging.Unknown();
        if (known)
        {
            source = source.Write(workspace - 8, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray());
        }
        source = source.WriteResourcePointer(workspace + 8, new(new(0, 0), null));
        NativeBattleRawMesh mesh = new([new(new(1, 2, 1000), 0), new(new(1, 2, 0), 0), new(new(3, 4, -1), 0), new(new(5, 6, 1000), 0), new(new(0, 0, 0), 0), new(new(1, 2, 1), 0), new(new(3, 4, 3), 0)], []);
        ImmutableArray<short> wave = new short[4096].ToImmutableArray();
        string original = NativeBattleTestSnapshots.Json(source);
        NativeBattleFileStaging preserved = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleFileStaging expected = NativeBattleTestSnapshots.Independent(source);
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleModuleDepthCueVertex native = NativeBattleModuleProjection.ProjectWithCue45FAA0(Identity, mesh.Vertices[index].Position, Projection, 0, 0);
            NativeBattleProjectedVertex projected = NativeBattleZombieOverlay.Project(native, 0, wave, 0, 0);
            byte[] bytes = new byte[8];
            if (native.Projected.ReadDepth45E250() == 0)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, 0x10);
                expected = expected.Write(workspace + (uint)index * 8 + 6, bytes.AsSpan(0, 2));
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, projected.Screen);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), projected.DepthAndClip);
                expected = expected.Write(workspace + (uint)index * 8, bytes);
            }
        }
        NativeBattleZombieOverlayDraw actual = NativeBattleZombieOverlay.Draw([mesh], [Identity], Projection, 0, 1, null, workspace, 56, source, wave, 0, 0, 0, 0, 0);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual.Staging));
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Fact]
    public void Far_quad_uses_the_projected_quarter_depth_before_AVSZ4()
    {
        uint workspace = NativeBattleFileStaging.Address + 64;
        byte[] face = new byte[20];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(index * 2), (ushort)index);
        }
        NativeBattleRawMesh mesh = new([new(new(-100, -100, 18748), 0), new(new(100, -100, 18748), 0), new(new(-100, 100, 18748), 0), new(new(100, 100, 18748), 0)], [new(true, face.ToImmutableArray())]);
        ImmutableArray<short> wave = new short[4096].ToImmutableArray();
        NativeBattleZombieOverlayDraw actual = NativeBattleZombieOverlay.Draw([mesh], [Identity], Projection, 0, 1, null, workspace, 32, NativeBattleFileStaging.Unknown(), wave, 0, 0, 0, 0, 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(actual.Packets.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(40, actual.Packets.AllocationBytes);
        Assert.Equal(10, primitive.Words.Length);
        Assert.Equal(1188, primitive.Bucket);
        Assert.Equal(new[] { 4687, 4687, 4687, 4687 }, primitive.VertexPayload4);
        for (int index = 0; index < 4; index++)
        {
            Assert.Equal(4687, BinaryPrimitives.ReadUInt16LittleEndian(actual.Staging.Read(workspace + (uint)index * 8 + 4, 2).AsSpan()));
        }
    }

    [Fact]
    public void ProjectionCopiesTheRetainedStagingRegionOncePerContiguousMesh()
    {
        uint workspace = NativeBattleFileStaging.Address + 64;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, new byte[2 * 1024 * 1024]);
        NativeBattleRawMesh mesh = new(Enumerable.Range(0, 400).Select(index => new NativeBattleRawVertex(new((short)index, 0, 1000), 0)).ToImmutableArray(), []);
        ImmutableArray<short> wave = new short[4096].ToImmutableArray();
        NativeBattleFileStaging preserved = NativeBattleStorageSnapshots.Capture(staging);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleZombieOverlayDraw actual = NativeBattleZombieOverlay.Draw([mesh], [Identity], Projection, 0, 1, null, workspace, 3200, staging, wave, 0, 0, 0, 0, 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16 * 1024 * 1024, $"Projection allocated {allocated} bytes for one 2 MiB staging region.");
        Assert.Equal(3200, actual.Staging.Read(workspace, 3200).Length);
        Assert.All(preserved.Read(workspace, 3200), value => Assert.Equal(0, value));
    }
}
