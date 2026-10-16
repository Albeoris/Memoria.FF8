using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRawVertex(NativeBattleVector Position, ushort Bone);
public sealed record NativeBattleRawPolygon(bool Quad, ImmutableArray<byte> Bytes);
public sealed record NativeBattleRawMesh(ImmutableArray<NativeBattleRawVertex> Vertices, ImmutableArray<NativeBattleRawPolygon> Polygons)
{
    public int? StageGroup { get; init; }

    public void Validate()
    {
        if (Vertices.IsDefault || Polygons.IsDefault || Vertices.Any(vertex => vertex is null) || Polygons.Any(polygon => polygon is null || polygon.Bytes.IsDefault || polygon.Bytes.Length != (polygon.Quad ? 20 : 16) && polygon.Bytes.Length != (polygon.Quad ? 24 : 20)))
        {
            throw new InvalidDataException("Native geometry requires bounded original vertex groups and all original polygon bytes.");
        }
    }
}

public sealed partial record NativeBattleGraphicsResource
{
    public ImmutableArray<NativeBattleRawMesh>? Geometry { get; init; }
    public ImmutableArray<NativeBattleRawMesh>? LinkedGeometry { get; init; }
    public bool? LinkedGeometryUsesBodySkeleton { get; init; }
    public NativeBattleEffectHeapAllocation? LinkedTexturePack { get; init; }
}

public sealed record NativeBattleLinkedModelTransform(NativeBattleVector Rotation, NativeBattleVector Position);

public sealed partial record NativeBattleGraphicsModel
{
    public NativeBattleModelTextureBinding? LinkedTextureSlots { get; init; }
    public int? LinkedTextureResourceIndex { get; init; }
    public NativeBattleLinkedModelTransform? LinkedTransform { get; init; }
    public NativeBattleActionMatrix? GeometryWorldMatrix { get; init; }
}

public sealed partial record NativeBattleStageSource
{
    public ImmutableArray<NativeBattleRawMesh>? Geometry { get; init; }
}
