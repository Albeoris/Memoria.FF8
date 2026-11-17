using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

/// <summary>Face-corner adjacency preserves indexed parts and creases of at least sixty degrees; coincident unrelated vertices are never welded.</summary>
public sealed class FieldModelNormals
{
    private readonly FieldModel model;
    private readonly int[][][] neighbors;

    public FieldModelNormals(FieldModel model, IReadOnlyList<Vector3> referencePose)
    {
        this.model = model;
        Vector3[] faces = FaceNormals(referencePose);
        List<int>[] adjacency = Enumerable.Range(0, model.Vertices.Count).Select(_ => new List<int>()).ToArray();
        for (int index = 0; index < model.Faces.Count; index++)
        {
            foreach (int vertex in model.Faces[index].Vertices)
            {
                adjacency[vertex].Add(index);
            }
        }
        neighbors = model.Faces.Select((face, index) => face.Vertices.Select(vertex => adjacency[vertex].Where(other => Vector3.Dot(faces[index], faces[other]) >= 0.5f).ToArray()).ToArray()).ToArray();
    }

    public Vector3[][] Pose(IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3>? importedNormals = null)
    {
        Vector3[] faces = FaceNormals(positions);
        Vector3[][] result = new Vector3[model.Faces.Count][];
        for (int face = 0; face < result.Length; face++)
        {
            result[face] = new Vector3[model.Faces[face].Vertices.Length];
            for (int corner = 0; corner < result[face].Length; corner++)
            {
                Vector3 normal = importedNormals?[model.Faces[face].Vertices[corner]] ?? Vector3.Zero;
                if (normal.LengthSquared() < 0.000001f)
                {
                    foreach (int neighbor in neighbors[face][corner])
                    {
                        normal += faces[neighbor];
                    }
                }
                result[face][corner] = normal.LengthSquared() > 0.000001f ? Vector3.Normalize(normal) : faces[face];
            }
        }
        return result;
    }

    private Vector3[] FaceNormals(IReadOnlyList<Vector3> positions)
    {
        return model.Faces.Select(face =>
        {
            Vector3 normal = Vector3.Cross(positions[face.Vertices[1]] - positions[face.Vertices[0]], positions[face.Vertices[2]] - positions[face.Vertices[0]]);
            return normal.LengthSquared() > 0.000001f ? Vector3.Normalize(normal) : Vector3.Zero;
        }).ToArray();
    }
}
