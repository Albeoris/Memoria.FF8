using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Tests;

/// <summary>Streamed reference for parity and allocation comparisons with retained FFVII terrain.</summary>
internal sealed class Ff7WorldTerrainReference(Ff7WorldTextureResources textures)
{
    private readonly Dictionary<PcTexture, PixelImage> images = new(ReferenceEqualityComparer.Instance);

    public int DrawNative(ITriangleRenderer renderer, PcWorldMesh mesh, Vector3 nativeOrigin, Ff7WorldView view, Func<int, int> textureFrame, int curvatureThreshold = 5000, Func<int, Vector3, Vector3>? deformVertex = null)
    {
        Vector3[] cameraPositions = new Vector3[mesh.Vertices.Count];
        Vector4[] colors = new Vector4[mesh.Vertices.Count];
        for (int index = 0; index < mesh.Vertices.Count; index++)
        {
            Vector3 source = mesh.Vertices[index];
            Vector3 position = (deformVertex is null ? source : deformVertex(index, source)) + nativeOrigin;
            position = Ff7WorldTerrainAdapter.Curve(position, view, curvatureThreshold);
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
}
