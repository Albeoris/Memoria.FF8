using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>The original host primitive identity, independent of packet allocation and presentation time.</summary>
public readonly record struct NativeBattleArenaPrimitiveId(int Part, int Group, int Polygon);

public sealed record NativeBattleArenaFace(NativeBattleArenaPrimitiveId Id, Vector3 A, Vector3 B, Vector3 C, NativeBattleForeignSurface Surface);

/// <summary>Host arena triangles in the same world coordinates as the native effect actor bridge.</summary>
public sealed record NativeBattleArenaGeometry(ImmutableArray<NativeBattleArenaFace> Faces);

/// <summary>A bounded deferred surface image. The renderer resolves this request from the original host materials.</summary>
public sealed record NativeBattleArenaCaptureMaterial(NativeBattleArenaGeometry Geometry, NativeBattleCameraProjection Projection, int Width, int Height) : INativeBattleForeignMaterial
{
    public int NativeX { get; init; }
    public int NativeY { get; init; }
    public void Validate()
    {
        if (Geometry.Faces.IsDefaultOrEmpty || Projection.View is null || Projection.FocalLength is null || Projection.OffsetX is null || Projection.OffsetY is null || Width is < 1 or > 1024 || Height is < 1 or > 512 || NativeX < 0 || NativeY < 0 || NativeX > 1024 - Width || NativeY > 512 - Height)
        {
            throw new NotSupportedException("Arena capture requires actual surface triangles, a complete camera and a bounded image.");
        }
        Projection.View.Validate();
        foreach (NativeBattleArenaFace face in Geometry.Faces)
        {
            if (!Finite(face.A) || !Finite(face.B) || !Finite(face.C) || face.Surface.Material is null || face.Surface.Uv.IsDefault || face.Surface.Uv.Length != 3 || face.Surface.Colors.IsDefault || face.Surface.Colors.Length != 3 || face.Surface.Uv.Any(uv => !float.IsFinite(uv.X) || !float.IsFinite(uv.Y)) || face.Surface.Colors.Any(color => !float.IsFinite(color.X) || !float.IsFinite(color.Y) || !float.IsFinite(color.Z) || !float.IsFinite(color.W)))
            {
                throw new NotSupportedException("Arena capture requires finite original triangles and complete material corner attributes.");
            }
        }
    }

    private static bool Finite(Vector3 point) => float.IsFinite(point.X) && float.IsFinite(point.Y) && float.IsFinite(point.Z);
}

/// <summary>A temporary replacement owns complete original primitives until its presentation task releases them.</summary>
public sealed record NativeBattleArenaReplacement(NativeBattlePilotVisualIdentity Owner, NativeBattleArenaGeometry Geometry);

public sealed class NativeBattleArenaHost(NativeBattleArenaGeometry geometry)
{
    private readonly HashSet<NativeBattleArenaPrimitiveId> primitiveIds = geometry.Faces.Select(face => face.Id).ToHashSet();
    private readonly Dictionary<NativeBattlePilotVisualIdentity, NativeBattleArenaReplacement> replacements = [];
    private readonly Dictionary<NativeBattlePilotVisualIdentity, NativeBattleArenaCaptureMaterial> captures = [];
    private readonly Dictionary<NativeBattlePilotVisualIdentity, NativeBattleArenaGeometry> deformationSurfaces = [];

    public NativeBattleArenaGeometry Geometry { get; } = geometry;
    public ImmutableArray<NativeBattleArenaReplacement> Replacements => replacements.Values.ToImmutableArray();

    public NativeBattleArenaCaptureMaterial Capture(NativeBattlePilotVisualIdentity owner, NativeBattleArenaGeometry surface, NativeBattleCameraProjection projection, int width, int height, int nativeX = 0, int nativeY = 0)
    {
        NativeBattleArenaCaptureMaterial material = new(surface, projection, width, height) { NativeX = nativeX, NativeY = nativeY };
        material.Validate();
        captures[owner] = material;
        deformationSurfaces.Remove(owner);
        return material;
    }

    public NativeBattleArenaCaptureMaterial ReadCapture(NativeBattlePilotVisualIdentity owner)
    {
        return captures.TryGetValue(owner, out NativeBattleArenaCaptureMaterial? material) ? material : throw new NotSupportedException("The arena surface image has no retained capture owner.");
    }

    public void RefineSurface(NativeBattlePilotVisualIdentity owner, ImmutableArray<(Vector3 A, Vector3 B, Vector3 C)> controlTriangles)
    {
        deformationSurfaces[owner] = NativeBattleArenaSurfaceQueries.Refine(ReadCapture(owner).Geometry, controlTriangles);
    }

    public NativeBattleArenaGeometry ReadSurface(NativeBattlePilotVisualIdentity owner)
    {
        return deformationSurfaces.TryGetValue(owner, out NativeBattleArenaGeometry? surface) ? surface : ReadCapture(owner).Geometry;
    }

    public void Replace(NativeBattlePilotVisualIdentity owner, NativeBattleArenaGeometry surface)
    {
        if (surface.Faces.IsDefaultOrEmpty || surface.Faces.Any(face => !primitiveIds.Contains(face.Id)) || surface.Faces.Select(face => face.Id).Distinct().Count() != surface.Faces.Length)
        {
            throw new InvalidDataException("Arena replacement requires original host primitive identities.");
        }
        HashSet<NativeBattleArenaPrimitiveId> selected = surface.Faces.Select(face => face.Id).ToHashSet();
        if (replacements.Any(replacement => replacement.Key != owner && replacement.Value.Geometry.Faces.Any(face => selected.Contains(face.Id))))
        {
            throw new NotSupportedException("Two presentation tasks cannot replace the same arena primitive concurrently.");
        }
        replacements[owner] = new(owner, surface);
    }

    public void Release(NativeBattlePilotVisualIdentity owner)
    {
        replacements.Remove(owner);
    }
}
