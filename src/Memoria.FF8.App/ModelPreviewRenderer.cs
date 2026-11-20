using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal readonly record struct ModelPreviewBounds(Vector3 Minimum, Vector3 Maximum)
{
    public ModelPreviewBounds Union(ModelPreviewBounds other)
    {
        return new(Vector3.Min(Minimum, other.Minimum), Vector3.Max(Maximum, other.Maximum));
    }
}

/// <summary>Fits the selected model to a stable orbit preview without advancing the game.</summary>
internal static class ModelPreviewRenderer
{
    private static readonly Matrix4x4 FieldToView = Matrix4x4.CreateRotationX(-MathF.PI / 2);

    public static PixelImage RenderFieldModel(FieldModel model, ModelPreviewCamera camera, int animation, float seconds, ModelPreviewBounds framing, int width, int height)
    {
        // Fixed camera distance keeps the posed mesh in front of the near plane while zoom changes the viewport scale.
        float radius = Vector3.Distance(framing.Minimum, framing.Maximum) * 0.5f;
        if (!float.IsFinite(radius) || radius <= 0)
        {
            throw new InvalidDataException("The model has no finite visible geometry.");
        }
        Matrix4x4 rotation = FieldToView * Matrix4x4.CreateFromQuaternion(camera.Orientation);
        Vector3 x = new(rotation.M11, rotation.M21, rotation.M31);
        // The field presentation stretches a 224-line drawing area to the 240-line viewport.
        Vector3 y = -new Vector3(rotation.M12, rotation.M22, rotation.M32) * (224f / FieldRenderer.Height);
        Vector3 z = -new Vector3(rotation.M13, rotation.M23, rotation.M33);
        Vector3 center = Vector3.Transform((framing.Minimum + framing.Maximum) * 0.5f, Matrix4x4.Transpose(FieldToView));
        float distance = Math.Max(2, radius * 8);
        float focal = FieldRenderer.Height * 0.43f * distance * camera.Zoom / radius;
        // Offset the optical center through the same native viewport mapping as the mesh.
        Vector2 pan = camera.Pan * new Vector2(FieldRenderer.Height, 224);
        Vector3 translation = new(-Vector3.Dot(x, center), -Vector3.Dot(y, center), distance - Vector3.Dot(z, center));
        FieldCamera view = new(x, y, z, translation, pan, focal);
        FieldAssets field = new(0, model.Name, [], [], [], view, new FieldWalkmesh([]), [model]);
        ActorVisual actor = new(0, 0, 0, 0, 0, -64, animation, FieldFrame(model, animation, seconds), true, PolygonColor: new Vector3(128), GroundShadowEnabled: false);
        FieldRenderer renderer = new(field, width, height) { ClearColor = new(14, 17, 23, 255) };
        byte[] rgba = renderer.Render(new Dictionary<int, int>(), [actor], TimeSpan.Zero);
        return new(width, height, MemoryMarshal.Cast<byte, Rgba32>(rgba).ToArray());
    }

    private static float FieldFrame(FieldModel model, int animation, float seconds)
    {
        if (model.EditableOverride is { } edited)
        {
            float duration = (uint)animation < edited.Animations.Count ? edited.Animations[animation].Duration : 0;
            float frames = duration * edited.NativeFramesPerSecond + 1;
            return seconds * edited.NativeFramesPerSecond % frames;
        }
        return seconds * ModelExchange.FramesPerSecond % model.Animations[animation].Frames.Count;
    }

    public static ModelPreviewBounds Measure(FieldModel model, int animation)
    {
        return Measure(model.Pose(animation, 0).Select(point => Vector3.Transform(point, FieldToView)));
    }

    public static void Render(ITriangleRenderer renderer, EditableModel model, int width, int height, float yaw, float pitch, float zoom, float seconds, int animation = 0, bool loop = true, ModelPreviewBounds? framing = null)
    {
        ModelPreviewCamera camera = new();
        camera.Rotate(yaw, pitch);
        camera.ZoomBy(zoom);
        Render(renderer, model, width, height, camera, seconds, animation, loop, framing);
    }

    public static void Render(ITriangleRenderer renderer, EditableModel model, int width, int height, ModelPreviewCamera camera, float seconds, int animation = 0, bool loop = true, ModelPreviewBounds? framing = null)
    {
        ModelPreviewBounds bounds = framing ?? Measure(model);
        Vector3 minimum = bounds.Minimum;
        Vector3 maximum = bounds.Maximum;
        Vector3 center = (minimum + maximum) * 0.5f;
        float radius = Vector3.Distance(minimum, maximum) * 0.5f;
        if (!float.IsFinite(radius) || radius <= 0)
        {
            throw new InvalidDataException("The model has no finite visible geometry.");
        }
        float size = Math.Min(width, height) * 0.43f * camera.Zoom / radius;
        Matrix4x4 rotation = Matrix4x4.CreateFromQuaternion(camera.Orientation);
        float duration = (uint)animation < model.Animations.Count ? model.Animations[animation].Duration : 0;
        IReadOnlyList<Vector3[]> pose = ModelExchange.ToAuthoringSpace(model, ModelExchange.Pose(model, animation, duration > 0 ? loop ? seconds % (duration + 1 / model.NativeFramesPerSecond) : Math.Clamp(seconds, 0, duration) : 0));
        renderer.Begin(width, height, new Vector4(0.055f, 0.065f, 0.09f, 1));
        try
        {
            for (int meshIndex = 0; meshIndex < model.Meshes.Count; meshIndex++)
            {
                EditableMesh mesh = model.Meshes[meshIndex];
                float? cutoff = ModelExchange.GetAlphaCutoff(mesh);
                TriangleMaterial material = new(mesh.Texture >= 0 ? model.Textures[mesh.Texture] : null, cutoff.HasValue ? TriangleBlend.Replace : TriangleBlend.Alpha, MinimumAlpha: cutoff ?? 1f / 255, Sampler: TriangleSampler.LinearWrap);
                for (int index = 0; index < mesh.Indices.Count; index += 3)
                {
                    TriangleVertex Vertex(int vertexIndex)
                    {
                        EditableVertex vertex = mesh.Vertices[vertexIndex];
                        Vector3 position = Vector3.Transform(pose[meshIndex][vertexIndex] - center, rotation);
                        float depth = Math.Clamp(0.5f - position.Z / (radius * 8), 0, 1);
                        return new(new Vector2(width * 0.5f + position.X * size + camera.Pan.X * Math.Min(width, height), height * 0.5f - position.Y * size + camera.Pan.Y * Math.Min(width, height)), depth, 1, vertex.Uv, vertex.Color);
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
    public static ModelPreviewBounds Measure(EditableModel model)
    {
        IReadOnlyList<Vector3[]> rest = ModelExchange.ToAuthoringSpace(model, ModelExchange.Pose(model, 0, 0));
        return Measure(rest.SelectMany(mesh => mesh));
    }

    private static ModelPreviewBounds Measure(IEnumerable<Vector3> points)
    {
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        foreach (Vector3 point in points)
        {
            minimum = Vector3.Min(minimum, point);
            maximum = Vector3.Max(maximum, point);
        }
        return new(minimum, maximum);
    }
}
