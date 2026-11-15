using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Produces model polygons ready for packet submission with their loaded material WORDs.</summary>
internal static class NativeBattleActorMaterialGeometry
{
    internal static ImmutableArray<NativeBattleRawMesh> PrepareBody(NativeBattleGraphicsResource resource, NativeBattleGraphicsModel model)
    {
        ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new NotSupportedException("Native actor drawing requires its original body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = RequireLinked(resource);
        if (linked.IsEmpty || !(resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native actor drawing requires its original linked skeleton layout.")))
        {
            return resource.GeometryRelocated507550 ? body : PrepareAuthored(body, model.BodyTextureSlots);
        }
        if (body.Length < 2 || linked.Length != 1)
        {
            throw new InvalidDataException("Native shared weapon requires its original body directory slot and single weapon object.");
        }

        var prepared = ImmutableArray.CreateBuilder<NativeBattleRawMesh>(body.Length);
        for (int index = 0; index < body.Length; index++)
        {
            if (index == 1)
            {
                prepared.Add(PrepareAuthored(linked[0], RequireBinding(model.LinkedTextureSlots)));
            }
            else
            {
                prepared.Add(resource.GeometryRelocated507550 ? body[index] : PrepareAuthored(body[index], RequireBinding(model.BodyTextureSlots)));
            }
        }
        return prepared.MoveToImmutable();
    }

    internal static ImmutableArray<NativeBattleRawMesh> PrepareSeparateLinked(NativeBattleGraphicsResource resource, NativeBattleGraphicsModel model)
    {
        ImmutableArray<NativeBattleRawMesh> linked = RequireLinked(resource);
        if (linked.IsEmpty || (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native actor drawing requires its original linked skeleton layout.")))
        {
            return [];
        }
        // The loaded CPU view replaces the body directory. A separate weapon is still its own authored resource.
        return PrepareAuthored(linked, model.LinkedTextureSlots);
    }

    private static ImmutableArray<NativeBattleRawMesh> RequireLinked(NativeBattleGraphicsResource resource)
    {
        return resource.LinkedGeometry ?? throw new NotSupportedException("Native actor drawing requires its actual linked geometry presence.");
    }

    private static ImmutableArray<NativeBattleRawMesh> PrepareAuthored(ImmutableArray<NativeBattleRawMesh> meshes, NativeBattleModelTextureBinding? binding)
    {
        NativeBattleModelTextureBinding actual = RequireBinding(binding);
        return meshes.Select(mesh => PrepareAuthored(mesh, actual)).ToImmutableArray();
    }

    private static NativeBattleRawMesh PrepareAuthored(NativeBattleRawMesh mesh, NativeBattleModelTextureBinding binding)
    {
        return mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, binding) }).ToImmutableArray() };
    }

    private static NativeBattleModelTextureBinding RequireBinding(NativeBattleModelTextureBinding? binding)
    {
        return binding ?? throw new NotSupportedException("Native actor drawing requires its executed original texture relocation.");
    }
}
