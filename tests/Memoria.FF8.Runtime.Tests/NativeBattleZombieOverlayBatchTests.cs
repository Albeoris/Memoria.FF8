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
        NativeBattleRawMesh mesh = new([new(new(1, 2, 1000), 0), new(new(1, 2, 0), 0), new(new(3, 4, -1), 0), new(new(5, 6, 1000), 0), new(new(0, 0, 0), 0)], []);
        ImmutableArray<short> wave = new short[4096].ToImmutableArray();
        string original = JsonSerializer.Serialize(source);
        NativeBattleFileStaging expected = source;
        for (int index = 0; index < mesh.Vertices.Length; index++)
        {
            NativeBattleModuleDepthCueVertex native = NativeBattleModuleProjection.ProjectWithCue45FAA0(Identity, mesh.Vertices[index].Position, Projection, 0, 0);
            NativeBattleProjectedVertex projected = NativeBattleZombieOverlay.Project(native, 0, wave, 0, 0);
            byte[] bytes = new byte[8];
            if (native.Projected.Depth == 0)
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
        NativeBattleZombieOverlayDraw actual = NativeBattleZombieOverlay.Draw([mesh], [Identity], Projection, 0, 1, null, workspace, 40, source, wave, 0, 0, 0, 0, 0);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual.Staging));
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void ProjectionCopiesTheRetainedStagingRegionOncePerContiguousMesh()
    {
        uint workspace = NativeBattleFileStaging.Address + 64;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(NativeBattleFileStaging.Address, new byte[2 * 1024 * 1024]);
        NativeBattleRawMesh mesh = new(Enumerable.Range(0, 400).Select(index => new NativeBattleRawVertex(new((short)index, 0, 1000), 0)).ToImmutableArray(), []);
        ImmutableArray<short> wave = new short[4096].ToImmutableArray();
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleZombieOverlayDraw actual = NativeBattleZombieOverlay.Draw([mesh], [Identity], Projection, 0, 1, null, workspace, 3200, staging, wave, 0, 0, 0, 0, 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 16 * 1024 * 1024, $"Projection allocated {allocated} bytes for one 2 MiB staging region.");
        Assert.Equal(3200, actual.Staging.Read(workspace, 3200).Length);
        Assert.All(staging.Read(workspace, 3200), value => Assert.Equal(0, value));
    }
}

