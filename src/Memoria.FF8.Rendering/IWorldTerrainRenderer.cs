using System.Numerics;

namespace Memoria.FF8.Rendering;

public readonly record struct WorldTerrainVertex(Vector3 Position, Vector2 Uv, Vector3 Normal = default, float Deform = 0);

/// <summary>Immutable geometry in WMX/16 units, retained across camera changes.</summary>
public sealed class WorldTerrainMesh(WorldTerrainVertex[] vertices)
{
    public ReadOnlyMemory<WorldTerrainVertex> Vertices { get; } = vertices;
}

public readonly record struct WorldTerrainView(Matrix4x4 View, Vector3 Offset, float Focal, bool NativeCurvature)
{
    public Ff7WorldTerrainView? Ff7 { get; init; }
}

public readonly record struct Ff7WorldTerrainView(float CenterY, int CurvatureThreshold, Vector3? BridgeAxis, float OffsetX = 0);

/// <summary>Optional hardware transformation, horizon curvature and clipping for retained terrain geometry.</summary>
public interface IWorldTerrainRenderer
{
    void DrawTerrain(WorldTerrainMesh mesh, in WorldTerrainView view, in TriangleMaterial material, int startVertex = 0, int vertexCount = -1);
}
