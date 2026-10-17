using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleGeometryMetadata
{
    public static ImmutableArray<NativeBattleRawMesh> FromMeshes(IReadOnlyList<BattleMesh> meshes)
    {
        return meshes.Select(mesh => new NativeBattleRawMesh(mesh.Vertices.Select(vertex => new NativeBattleRawVertex(vertex.NativePosition ?? throw new NotSupportedException("Native geometry DTO requires the original signed vertex WORDs."), checked((ushort)vertex.Bone))).ToImmutableArray(), mesh.Faces.Select(face => new NativeBattleRawPolygon(face.Vertices.Length == 4, face.NativePolygon)).ToImmutableArray()) { StageGroup = mesh.NativeStageGroup, BoneGroups = mesh.NativeBoneGroups }).ToImmutableArray();
    }
}
