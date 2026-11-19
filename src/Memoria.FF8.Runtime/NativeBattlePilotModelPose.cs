using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>Complete posed model surfaces, independently of the polygons admitted to the native ordering table.</summary>
public sealed class NativeBattlePilotModelPose
{
    private readonly ImmutableArray<Surface> surfaces;

    private NativeBattlePilotModelPose(ImmutableArray<Surface> surfaces)
    {
        this.surfaces = surfaces;
    }

    internal static NativeBattlePilotModelPose Capture(ImmutableArray<NativeBattleRawMesh> meshes, IReadOnlyList<NativeBattleActionMatrix> matrices, NativeBattleCameraProjection projection, NativeBattlePilotVisualIdentity identity, int meshFactor, bool resourceEpoch, bool continuous)
    {
        return new(meshes.Select((mesh, index) =>
        {
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * meshFactor + index), ResourceGeneration = resourceEpoch ? NativeBattlePilotVisualLifetimes.EpochFor(mesh) : identity.ResourceGeneration };
            ImmutableArray<Vector3> positions = mesh.Vertices.Select(vertex => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrices[vertex.Bone], vertex.Position), projection)).ToImmutableArray();
            return new Surface(selected, mesh.Polygons, positions, continuous);
        }).ToImmutableArray());
    }

    public IEnumerable<NativeBattlePilotGeometryReference> Geometry()
    {
        foreach (Surface surface in surfaces)
        {
            for (int polygon = 0; polygon < surface.Polygons.Length; polygon++)
            {
                yield return new(surface, polygon);
            }
        }
    }

    internal ImmutableArray<Vector3> Positions(int mesh) => surfaces[mesh].Positions;

    internal sealed record Surface(NativeBattlePilotVisualIdentity Identity, ImmutableArray<NativeBattleRawPolygon> Polygons, ImmutableArray<Vector3> Positions, bool Continuous)
    {
        internal Vector3 Corner(int polygon, int corner)
        {
            int selector = BinaryPrimitives.ReadUInt16LittleEndian(Polygons[polygon].Bytes.AsSpan()[(corner * 2)..]);
            return Positions[corner == 0 ? selector & 0xFFF : selector];
        }
    }
}
