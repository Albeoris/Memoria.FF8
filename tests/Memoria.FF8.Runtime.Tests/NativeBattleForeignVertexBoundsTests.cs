using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleForeignVertexBoundsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Foreign_vertex_bounds_match_native_raw_position_and_projection_with_actual_unreferenced_vertices(bool projected)
    {
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleActionMatrix world = NativeBattleForeignModelGeometry.Identity with { X = 1000, Y = 2000, Z = 3000 };
        NativeBattleGraphicsModel native = new(3) { Transform = new(default, new(10, 20, 30), 900, 0, 4096), GeometryWorldMatrix = world, SequenceState = new() { MeshMask124 = 1, DecodedPose = new(default, [new(default, new(1024, 1024, 1024))], false) } };
        native = NativeBattleBodyBoneMatrices.Publish508C90(native, source);
        ImmutableArray<NativeBattleVector> vertices = [new(0, -300, 10), new(0, 700, 10), new(0, 1700, 10)];
        NativeBattleRawMesh mesh = new(vertices.Select(vertex => new NativeBattleRawVertex(vertex, 0)).ToImmutableArray(), []);
        NativeBattleGraphicsModel foreign = native with { SequenceState = null, CurrentBodyBoneMatrices = null, GeometryWorldMatrix = null };
        NativeBattleForeignModelGeometry geometry = new(world, new(default, default, 0, new(default, default)), []) { Vertices = vertices };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown());
        List<NativeBattleVector> nativeInputs = [];
        List<NativeBattleVector> foreignInputs = [];
        NativeBattleCameraProjection projection = new(NativeBattleForeignModelGeometry.Identity, 256, 160, 108);
        (short Maximum, short Minimum) expected;
        (short Maximum, short Minimum) actual;
        if (projected)
        {
            NativeBattleDuel216BoundsStep original = NativeBattleDuel216Bounds.Read695F60(staging, native, source, [mesh], (matrix, vertex) => Project(matrix, vertex.Position, nativeInputs));
            NativeBattleDuel216BoundsStep bridged = NativeBattleDuel216Bounds.ReadForeign695F60(staging, foreign, geometry, (matrix, vertex) => Project(matrix, vertex, foreignInputs));
            expected = (original.Maximum, original.Minimum);
            actual = (bridged.Maximum, bridged.Minimum);
            Assert.Equal(vertices.ToArray(), nativeInputs.ToArray());
            Assert.Equal(nativeInputs.ToArray(), foreignInputs.ToArray());
            Assert.Same(foreign, bridged.Model);
            Assert.Same(staging, bridged.Staging);
        }
        else
        {
            NativeBattleDuel215BoundsStep original = NativeBattleDuel215Bounds.Read69D360(staging, native, source, [mesh]);
            NativeBattleDuel215BoundsStep bridged = NativeBattleDuel215Bounds.ReadForeign69D360(staging, foreign, geometry);
            expected = (original.Maximum, original.Minimum);
            actual = (bridged.Maximum, bridged.Minimum);
            Assert.Equal(((short)1720, (short)-280), actual);
            Assert.Same(foreign, bridged.Model);
            Assert.Same(staging, bridged.Staging);
        }
        Assert.Equal(expected, actual);
        Assert.Throws<NotSupportedException>(() => NativeBattleDuel215Bounds.ReadForeign69D360(staging, foreign, geometry with { Vertices = [] }));
        Assert.Throws<NotSupportedException>(() => NativeBattleDuel216Bounds.ReadForeign695F60(staging, foreign, geometry with { Vertices = [] }, (_, _) => 0));

        short Project(NativeBattleActionMatrix matrix, NativeBattleVector vertex, List<NativeBattleVector> inputs)
        {
            Assert.Equal(10, matrix.X);
            Assert.Equal(20, matrix.Y);
            Assert.Equal(30, matrix.Z);
            inputs.Add(vertex);
            return NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, vertex, projection, 0, 0).Ir2;
        }
    }
}
