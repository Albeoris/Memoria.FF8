using System.Numerics;
using System.Runtime.CompilerServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Rendering;

public sealed record CardMarkerVisual(int ObjectId, PixelImage Front, PixelImage Back, bool IsQuestLoss = false, bool UseHiddenInteractionAnchor = false);

public sealed partial class FieldRenderer
{
    private static readonly ConditionalWeakTable<PixelImage, PixelImage> QuestLossCardFaces = new();

    private void DrawCardMarkers(IReadOnlyList<ActorVisual> actors, IReadOnlyList<CardMarkerVisual> markers, TimeSpan elapsed)
    {
        foreach (CardMarkerVisual marker in markers)
        {
            ActorVisual? anchor = actors.Where(candidate => candidate.ObjectId == marker.ObjectId).Select(candidate => (ActorVisual?)candidate).FirstOrDefault();
            if (anchor is not { } actor || (!actor.Visible && !marker.UseHiddenInteractionAnchor) || (uint)actor.ModelId >= field.Models.Count)
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
            ProjectedPoint root = ProjectFieldWorld(new Vector3(actor.X, actor.Y, actor.Z));
            ProjectedPoint head = new(root.X, float.PositiveInfinity, root.Depth);
            for (int index = 0; index < pose.Length; index++)
            {
                if (actor.AdditionalControlDisabled && model.Vertices[index].Bone == model.ControlBones.Additional)
                {
                    continue;
                }
                ProjectedPoint vertex = ProjectFieldWorld(Vector3.Transform(pose[index], rotation) + new Vector3(actor.X, actor.Y, actor.Z));
                if (!OutsideWorldDepth(vertex.Depth) && vertex.Y < head.Y)
                {
                    head = vertex;
                }
            }
            if (!float.IsFinite(head.Y) || OutsideWorldDepth(root.Depth))
            {
                continue;
            }
            float angle = (float)(elapsed.TotalSeconds % 8 / 8 * Math.Tau);
            float cosine = MathF.Cos(angle);
            PixelImage texture = cosine >= 0 ? marker.Front : marker.Back;
            if (marker.IsQuestLoss)
            {
                texture = QuestLossCardFaces.GetValue(texture, AddQuestLossBorder);
            }
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

    private static PixelImage AddQuestLossBorder(PixelImage artwork)
    {
        PixelImage face = new(artwork.Width, artwork.Height);
        artwork.Pixels.AsSpan().CopyTo(face.Pixels);
        int borderWidth = Math.Max(1, face.Width / 8);
        int borderHeight = Math.Max(1, face.Height / 8);
        Rgba32 red = new(240, 32, 48);
        for (int y = 0; y < face.Height; y++)
        {
            Span<Rgba32> row = face.Pixels.AsSpan(y * face.Width, face.Width);
            if (y < borderHeight || y >= face.Height - borderHeight)
            {
                row.Fill(red);
            }
            else
            {
                row[..borderWidth].Fill(red);
                row[^borderWidth..].Fill(red);
            }
        }
        return face;
    }
}
