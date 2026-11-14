using System.Numerics;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

/// <summary>Retains original terrain geometry and submits it to the common hardware terrain path.</summary>
public sealed class Ff7WorldTerrainAdapter(Ff7WorldTextureResources textures, bool underwater = false)
{
    private readonly Dictionary<PcTexture, PixelImage> images = new(ReferenceEqualityComparer.Instance);
    private readonly ConditionalWeakTable<PcWorldMesh, TerrainGeometry> geometry = new();

    public void DrawRetained(IWorldTerrainRenderer renderer, PcWorldMesh mesh, Vector3 nativeOrigin, Ff7WorldView view, Func<int, int> textureFrame, int curvatureThreshold, Ff7WorldBridgeDefinition? bridge = null, Vector3? bridgeAxis = null)
    {
        if (!geometry.TryGetValue(mesh, out TerrainGeometry? retained))
        {
            retained = CreateGeometry(mesh, bridge);
            geometry.Add(mesh, retained);
        }
        WorldTerrainView camera = new(view.NativeView, nativeOrigin, view.Focal, false) { Ff7 = new(view.CenterY, curvatureThreshold, bridgeAxis, view.OffsetX, underwater) };
        foreach (TerrainRun run in retained.Runs)
        {
            PcTexture texture = textures.Read(run.TextureIndex, textureFrame(run.TextureIndex));
            renderer.DrawTerrain(retained.Mesh, camera, Material(texture), run.Start, run.Count);
        }
    }

    private TerrainGeometry CreateGeometry(PcWorldMesh mesh, Ff7WorldBridgeDefinition? bridge)
    {
        Validate(mesh);
        WorldTerrainVertex[] vertices = new WorldTerrainVertex[mesh.Triangles.Count * 3];
        List<TerrainRun> runs = [];
        int index = 0;
        int start = 0;
        int last = -1;
        foreach (PcWorldTriangle triangle in mesh.Triangles)
        {
            if (triangle.TextureIndex != last)
            {
                if (index != start)
                {
                    runs.Add(new(last, start, index - start));
                }
                last = triangle.TextureIndex;
                start = index;
            }
            Ff7WorldTextureDescriptor descriptor = textures.Descriptors[triangle.TextureIndex];
            Vector2 offset = new(descriptor.UOffset, descriptor.VOffset);
            WorldTerrainVertex Vertex(int vertex, Vector2 uv)
            {
                return new(mesh.Vertices[vertex], uv - offset, mesh.Normals[vertex] / 4096f, bridge?.Vertices.Contains(vertex) == true ? 1 : 0);
            }
            vertices[index++] = Vertex(triangle.A, triangle.UvA);
            vertices[index++] = Vertex(triangle.B, triangle.UvB);
            vertices[index++] = Vertex(triangle.C, triangle.UvC);
        }
        if (index != start)
        {
            runs.Add(new(last, start, index - start));
        }
        Ff7WorldTriangleMetadata[] metadata = underwater ? new Ff7WorldTriangleMetadata[vertices.Length] : [];
        for (int vertex = 0; vertex < metadata.Length; vertex++)
        {
            int startVertex = vertex / 3 * 3;
            int corner = vertex % 3;
            metadata[vertex] = new(vertices[startVertex + (corner + 1) % 3].Position, vertices[startVertex + (corner + 2) % 3].Position, corner);
        }
        return new(new(vertices) { Ff7Triangles = metadata }, runs);
    }

    private TriangleMaterial Material(PcTexture texture)
    {
        if (!images.TryGetValue(texture, out PixelImage? image))
        {
            image = Ff7FieldRenderer.DecodeTexture(texture);
            images.Add(texture, image);
        }
        return new(image, TriangleBlend.Replace, TriangleDepthComparison.GreaterEqual, true, Sampler: TriangleSampler.PointWrap)
        {
            DepthMapping = TriangleDepthMapping.Positive,
            DepthScale = 1,
            PreserveTargetAlpha = false
        };
    }

    private static void Validate(PcWorldMesh mesh)
    {
        if (mesh.Vertices.Count > 121 || mesh.Normals.Count != mesh.Vertices.Count)
        {
            throw new NotSupportedException("FFVII terrain requires original bounded vertex and normal channels.");
        }
    }

    private sealed record TerrainRun(int TextureIndex, int Start, int Count);
    private sealed record TerrainGeometry(WorldTerrainMesh Mesh, IReadOnlyList<TerrainRun> Runs);

    public static Vector3 Curve(Vector3 nativePosition, Ff7WorldView view, int threshold)
    {
        return Ff7WorldCameraMath.Curve(nativePosition, view.NativeView, view.Focal * 240 / view.Height, threshold);
    }

}
