using System.Numerics;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed record CardMarkerVisual(int ObjectId, PixelImage Front, PixelImage Back);

public sealed partial class FieldRenderer
{
    private void DrawCardMarkers(IReadOnlyList<ActorVisual> actors, IReadOnlyList<CardMarkerVisual> markers, TimeSpan elapsed)
    {
        foreach (CardMarkerVisual marker in markers)
        {
            ActorVisual actor = actors.FirstOrDefault(actor => actor.ObjectId == marker.ObjectId);
            if (!actor.Visible || (uint)actor.ModelId >= field.Models.Count)
            {
                continue;
            }
            FieldModel model = field.Models[actor.ModelId];
            if (model.Bones.Count == 0 || model.Vertices.Count == 0)
            {
                continue;
            }
            Vector3[] pose = ActorPose(model, actor);
            Matrix4x4 rotation = Matrix4x4.CreateRotationZ((actor.Direction + 64) * MathF.Tau / 256);
            ProjectedPoint root = ToViewport(camera.Project(new Vector3(actor.X, actor.Y, actor.Z)));
            ProjectedPoint head = new(root.X, float.PositiveInfinity, root.Depth);
            for (int index = 0; index < pose.Length; index++)
            {
                if (actor.AdditionalControlDisabled && model.Vertices[index].Bone == model.ControlBones.Additional)
                {
                    continue;
                }
                ProjectedPoint vertex = ToViewport(camera.Project(Vector3.Transform(pose[index], rotation) + new Vector3(actor.X, actor.Y, actor.Z)));
                if (vertex.Depth > 1 && vertex.Y < head.Y)
                {
                    head = vertex;
                }
            }
            if (!float.IsFinite(head.Y) || root.Depth <= 1)
            {
                continue;
            }
            float angle = (float)(elapsed.TotalSeconds % 8 / 8 * Math.Tau);
            float cosine = MathF.Cos(angle);
            PixelImage texture = cosine >= 0 ? marker.Front : marker.Back;
            float height = 14 * scale;
            // Both faces occupy the same physical card. Preserve the front texture's aspect.
            float halfWidth = height * marker.Front.Width / marker.Front.Height / 2;
            float centreY = head.Y - 4 * scale - height / 2;
            float perspective = MathF.Sin(angle) * (cosine >= 0 ? 1 : -1) * halfWidth / (height * 6);
            float leftScale = 1 / (1 + perspective);
            float rightScale = 1 / (1 - perspective);
            float visibleWidth = halfWidth * Math.Abs(cosine);
            ProjectedPoint a = new(head.X - visibleWidth * leftScale, centreY - height / 2 * leftScale, head.Depth * (1 + perspective));
            ProjectedPoint b = new(head.X + visibleWidth * rightScale, centreY - height / 2 * rightScale, head.Depth * (1 - perspective));
            ProjectedPoint c = new(head.X - visibleWidth * leftScale, centreY + height / 2 * leftScale, a.Depth);
            ProjectedPoint d = new(head.X + visibleWidth * rightScale, centreY + height / 2 * rightScale, b.Depth);
            Vector2 uvA = Vector2.Zero;
            Vector2 uvB = new(texture.Width - 1, 0);
            Vector2 uvC = new(0, texture.Height - 1);
            Vector2 uvD = new(texture.Width - 1, texture.Height - 1);
            Triangle(a, b, c, uvA, uvB, uvC, texture, new Vector3(128), false);
            Triangle(b, d, c, uvB, uvD, uvC, texture, new Vector3(128), false);
        }
    }
}
