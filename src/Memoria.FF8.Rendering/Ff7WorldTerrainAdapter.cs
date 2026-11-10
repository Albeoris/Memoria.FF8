using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

/// <summary>Retains original terrain geometry and submits it to the common hardware terrain path.</summary>
public sealed class Ff7WorldTerrainAdapter(Ff7WorldTextureResources textures)
{
    private readonly Dictionary<PcTexture, PixelImage> images = new(ReferenceEqualityComparer.Instance);

    public int DrawNative(ITriangleRenderer renderer, PcWorldMesh mesh, Vector3 nativeOrigin, Ff7WorldView view, Func<int, int> textureFrame, int curvatureThreshold = 5000, Func<int, Vector3, Vector3>? deformVertex = null)
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
            Vector3 source = mesh.Vertices[index];
            Vector3 position = (deformVertex is null ? source : deformVertex(index, source)) + nativeOrigin;
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

}
