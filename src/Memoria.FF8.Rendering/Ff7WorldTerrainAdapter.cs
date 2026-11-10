using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed record Ff7WorldTerrainBatch(WorldTerrainMesh Mesh, int TextureIndex, int StartVertex, int VertexCount);

/// <summary>Retains original terrain geometry and submits it to the common hardware terrain path.</summary>
public sealed class Ff7WorldTerrainAdapter(Ff7WorldTextureResources textures)
{
    private readonly Dictionary<(int Block, int Mesh), IReadOnlyList<Ff7WorldTerrainBatch>> meshes = new();
    private readonly Dictionary<PcTexture, PixelImage> images = new(ReferenceEqualityComparer.Instance);

    public int DrawNative(ITriangleRenderer renderer, PcWorldMesh mesh, Vector3 nativeOrigin, Ff7WorldView view, Func<int, int> textureFrame, int curvatureThreshold = 5000)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(textureFrame);
        if (mesh.Vertices.Count > 121 || mesh.Normals.Count != mesh.Vertices.Count)
        {
            throw new NotSupportedException("FFVII terrain requires original bounded vertex and normal channels.");
        }
        Vector3[] cameraPositions = new Vector3[mesh.Vertices.Count];
        Vector4[] colors = new Vector4[mesh.Vertices.Count];
        for (int index = 0; index < mesh.Vertices.Count; index++)
        {
            Vector3 position = mesh.Vertices[index] + nativeOrigin;
            position = Curve(position, view, curvatureThreshold);
            cameraPositions[index] = view.ToCamera(position);
            Vector3 normal = Vector3.TransformNormal(mesh.Normals[index] / 4096f, view.NativeRotation);
            colors[index] = Ff7WorldLighting.Terrain(normal);
        }
        int submitted = 0;
        foreach (PcWorldTriangle triangle in mesh.Triangles)
        {
            PcTexture texture = textures.Read(triangle.TextureIndex, textureFrame(triangle.TextureIndex));
            if (!images.TryGetValue(texture, out PixelImage? image))
            {
                image = Ff7FieldRenderer.DecodeTexture(texture);
                images.Add(texture, image);
            }
            Ff7WorldTextureDescriptor descriptor = textures.Descriptors[triangle.TextureIndex];
            CameraSpaceVertex Vertex(int vertex, Vector2 uv)
            {
                return new(cameraPositions[vertex], (uv - new Vector2(descriptor.UOffset, descriptor.VOffset)) / new Vector2(texture.Width, texture.Height), colors[vertex]);
            }
            TriangleMaterial material = new(image, TriangleBlend.Replace, TriangleDepthComparison.GreaterEqual, true, Sampler: TriangleSampler.PointWrap)
            {
                DepthMapping = TriangleDepthMapping.Positive,
                DepthScale = 1,
                PreserveTargetAlpha = false
            };
            submitted += CameraSpaceTriangleSubmission.Draw(renderer, Vertex(triangle.A, triangle.UvA), Vertex(triangle.B, triangle.UvB), Vertex(triangle.C, triangle.UvC), view.Width, view.Height, view.Focal, material, cull: true, clockwiseFront: true);
        }
        return submitted;
    }

    public static Vector3 Curve(Vector3 nativePosition, Ff7WorldView view, int threshold)
    {
        Vector3 camera = Vector3.Transform(nativePosition, view.NativeView);
        int distance = ((int)MathF.Round(camera.Z, MidpointRounding.ToEven) >> 2) - threshold;
        if (distance > 0 && camera.Z != 0)
        {
            int screenX = (int)MathF.Round(camera.X * (view.Focal * 240 / view.Height) / camera.Z, MidpointRounding.ToEven);
            float bend = distance * (1f / 64) + Math.Abs(screenX) * (1f / 32);
            nativePosition.Y -= bend * bend;
        }
        return nativePosition;
    }

    public IReadOnlyList<Ff7WorldTerrainBatch> Prepare(PcWorldMesh source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (meshes.TryGetValue((source.BlockIndex, source.MeshIndex), out IReadOnlyList<Ff7WorldTerrainBatch>? existing))
        {
            return existing;
        }
        WorldTerrainVertex[] vertices = new WorldTerrainVertex[checked(source.Triangles.Count * 3)];
        for (int index = 0; index < source.Triangles.Count; index++)
        {
            PcWorldTriangle triangle = source.Triangles[index];
            PcTexture texture = textures.Read(triangle.TextureIndex);
            Ff7WorldTextureDescriptor descriptor = textures.Descriptors[triangle.TextureIndex];
            WorldTerrainVertex Vertex(int vertex, Vector2 uv)
            {
                if ((uint)vertex >= source.Vertices.Count)
                {
                    throw new InvalidDataException("FFVII terrain refers to an absent vertex.");
                }
                return new(source.Vertices[vertex], (uv - new Vector2(descriptor.UOffset, descriptor.VOffset)) / new Vector2(texture.Width, texture.Height));
            }
            vertices[index * 3] = Vertex(triangle.A, triangle.UvA);
            vertices[index * 3 + 1] = Vertex(triangle.B, triangle.UvB);
            vertices[index * 3 + 2] = Vertex(triangle.C, triangle.UvC);
        }
        WorldTerrainMesh mesh = new(vertices);
        List<Ff7WorldTerrainBatch> batches = new();
        int first = 0;
        while (first < source.Triangles.Count)
        {
            int end = first + 1;
            int texture = source.Triangles[first].TextureIndex;
            while (end < source.Triangles.Count && source.Triangles[end].TextureIndex == texture)
            {
                end++;
            }
            batches.Add(new(mesh, texture, first * 3, (end - first) * 3));
            first = end;
        }
        IReadOnlyList<Ff7WorldTerrainBatch> result = batches.AsReadOnly();
        meshes.Add((source.BlockIndex, source.MeshIndex), result);
        return result;
    }

    public void Draw(IWorldTerrainRenderer renderer, PcWorldMesh mesh, in WorldTerrainView view, int animationFrame)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentOutOfRangeException.ThrowIfNegative(animationFrame);
        if (view.NativeCurvature)
        {
            throw new ArgumentException("FFVII terrain cannot use the FFVIII horizon transform.", nameof(view));
        }
        foreach (Ff7WorldTerrainBatch batch in Prepare(mesh))
        {
            PcTexture texture = textures.Read(batch.TextureIndex, animationFrame);
            if (!images.TryGetValue(texture, out PixelImage? image))
            {
                image = Ff7FieldRenderer.DecodeTexture(texture);
                images.Add(texture, image);
            }
            TriangleMaterial material = new(image, TriangleBlend.Replace, TriangleDepthComparison.GreaterEqual, true, Sampler: TriangleSampler.PointWrap)
            {
                DepthMapping = TriangleDepthMapping.Positive,
                DepthScale = 1,
                PreserveTargetAlpha = false
            };
            renderer.DrawTerrain(batch.Mesh, view, material, batch.StartVertex, batch.VertexCount);
        }
    }
}
