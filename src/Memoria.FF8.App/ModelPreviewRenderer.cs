using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

/// <summary>Fits the selected model to a stable orbit preview without advancing the game.</summary>
internal static class ModelPreviewRenderer
{
    public static void Render(ITriangleRenderer renderer, EditableModel model, int width, int height, float yaw, float pitch, float zoom, float seconds, int animation = 0, bool loop = true)
    {
        IReadOnlyList<Vector3[]> rest = ModelExchange.Pose(model, 0, 0);
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (Vector3 point in rest.SelectMany(mesh => mesh))
        {
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }
        Vector3 center = (minimum + maximum) * 0.5f;
        float radius = Vector3.Distance(minimum, maximum) * 0.5f;
        if (!float.IsFinite(radius) || radius <= 0)
        {
            throw new InvalidDataException("The model has no finite visible geometry.");
        }
        float size = Math.Min(width, height) * 0.43f * zoom / radius;
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(yaw) * Matrix4x4.CreateRotationX(pitch);
        float duration = (uint)animation < model.Animations.Count ? model.Animations[animation].Duration : 0;
        IReadOnlyList<Vector3[]> pose = ModelExchange.Pose(model, animation, duration > 0 ? loop ? seconds % duration : Math.Clamp(seconds, 0, duration) : 0);
        renderer.Begin(width, height, new Vector4(0.055f, 0.065f, 0.09f, 1));
        try
        {
            for (int meshIndex = 0; meshIndex < model.Meshes.Count; meshIndex++)
            {
                EditableMesh mesh = model.Meshes[meshIndex];
                TriangleMaterial material = new(mesh.Texture >= 0 ? model.Textures[mesh.Texture] : null, TriangleBlend.Alpha, Sampler: TriangleSampler.LinearWrap);
                for (int index = 0; index < mesh.Indices.Count; index += 3)
                {
                    TriangleVertex Vertex(int vertexIndex)
                    {
                        EditableVertex vertex = mesh.Vertices[vertexIndex];
                        Vector3 position = Vector3.Transform(pose[meshIndex][vertexIndex] - center, rotation);
                        float depth = Math.Clamp(0.5f - position.Z / (radius * 8), 0, 1);
                        return new(new Vector2(width * 0.5f + position.X * size, height * 0.5f - position.Y * size), depth, 1, vertex.Uv, vertex.Color);
                    }
                    renderer.Draw(Vertex(mesh.Indices[index]), Vertex(mesh.Indices[index + 1]), Vertex(mesh.Indices[index + 2]), material);
                }
            }
        }
        finally
        {
            renderer.End();
        }
    }
}
