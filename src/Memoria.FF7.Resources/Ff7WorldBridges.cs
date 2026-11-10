using System.Numerics;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldBridgeDefinition(int Index, int ChunkX, int ChunkZ, int CenterX, int CenterZ, int Yaw, IReadOnlyList<int> Vertices);
public sealed record Ff7WorldBridgeDeformation(int Index, int Tilt);

/// <summary>Applies the same frozen native bridge vertex positions to terrain collision and presentation.</summary>
public sealed class Ff7WorldBridgeGeometry(IReadOnlyList<Ff7WorldBridgeDefinition> definitions, Ff7WorldMath math)
{
    public Vector3 Vertex(int chunkX, int chunkZ, int vertex, Vector3 source, Ff7WorldBridgeDeformation? deformation)
    {
        if (deformation is null)
        {
            return source;
        }
        if ((uint)deformation.Index >= definitions.Count)
        {
            throw new InvalidDataException("FFVII world bridge deformation has no native resource owner.");
        }
        Ff7WorldBridgeDefinition definition = definitions[deformation.Index];
        if (definition.ChunkX != chunkX || definition.ChunkZ != chunkZ || !definition.Vertices.Contains(vertex))
        {
            return source;
        }
        Vector3 rotated = math.TransformRotation(new(0, unchecked((short)((short)source.Y - 20000)), 0), 0, definition.Yaw, deformation.Tilt);
        return new(source.X + (int)rotated.X, 20000 + (int)rotated.Y, source.Z + (int)rotated.Z);
    }
}
