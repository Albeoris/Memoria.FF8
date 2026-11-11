using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF8.Rendering;

public sealed record Ff7BattleActorFace(Vector3 A, Vector3 B, Vector3 C, int Part, int Group, int Polygon, PcRenderState Material);
public sealed record Ff7BattleActorGeometry(ImmutableArray<Ff7BattleActorFace> Faces, ImmutableArray<Vector3> SkeletonPoints);

public sealed partial class Ff7BattleRenderer
{
    public Ff7BattleActorGeometry CaptureActorGeometry(Ff7BattleActorVisual actor)
    {
        ImmutableArray<Ff7BattleActorFace>.Builder faces = ImmutableArray.CreateBuilder<Ff7BattleActorFace>();
        int part = 0;
        ImmutableArray<Vector3> skeleton = [];
        VisitActorGeometry(actor, (model, textures, transform) =>
        {
            if (model.Groups.Count != model.RenderStates.Count)
            {
                throw new InvalidDataException("FFVII battle P groups require their original material records.");
            }
            for (int groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
            {
                PcModelGroup group = model.Groups[groupIndex];
                PcRenderState material = PcRenderState.Read(model.RenderStates[groupIndex].Span);
                for (int polygonIndex = group.PolygonStart; polygonIndex < group.PolygonStart + group.PolygonCount; polygonIndex++)
                {
                    PcModelPolygon polygon = model.Polygons[polygonIndex];
                    faces.Add(new(Vertex(polygon.A), Vertex(polygon.B), Vertex(polygon.C), part, groupIndex, polygonIndex, material));
                }
                Vector3 Vertex(int local) => Vector3.Transform(model.Vertices[group.VertexStart + local], transform);
            }
            part++;
        }, points => skeleton = points);
        return new(faces.ToImmutable(), skeleton);
    }

    private void VisitActorGeometry(Ff7BattleActorVisual actor, Action<PcPolygonModel, IReadOnlyList<PcTexture>, Matrix4x4> visit, Action<ImmutableArray<Vector3>>? captureSkeleton = null)
    {
        ActorModel resource = LoadActor(actor.ModelPrefix);
        Matrix4x4 world = Matrix4x4.CreateRotationY(actor.FacingRadians) * Matrix4x4.CreateTranslation(actor.Position);
        BattleClipFrame frame = Frame(resource.GetClip(actor.ClipIndex, false, actor.Limit), actor.ClipFrame);
        Span<Matrix4x4> transforms = stackalloc Matrix4x4[resource.Data.Bones.Count];
        BattleRigidPose.WriteTransforms(resource.Data, frame, world, transforms, actor.ModelModifiers?.PoseTransform);
        if (captureSkeleton is not null)
        {
            ImmutableArray<Vector3>.Builder points = ImmutableArray.CreateBuilder<Vector3>();
            points.Add(Vector3.Transform(Vector3.Zero, BattleRigidPose.FrameTransform(frame, actor.ModelModifiers?.PoseTransform) * world));
            for (int bone = 0; bone < transforms.Length; bone++)
            {
                points.Add(Vector3.Transform(Vector3.Zero, transforms[bone]));
                points.Add(Vector3.Transform(new(0, 0, resource.Data.Bones[bone].Length), transforms[bone]));
            }
            captureSkeleton(points.ToImmutable());
        }
        for (int index = 0; index < transforms.Length; index++)
        {
            if (resource.Parts[index] is PcPolygonModel model)
            {
                visit(model, resource.Textures, transforms[index]);
            }
        }
        if (actor.WeaponIndex is int weaponIndex && resource.HasWeaponClip(actor.ClipIndex, actor.Limit))
        {
            BattleClipFrame weaponFrame = Frame(resource.GetClip(actor.ClipIndex, true, actor.Limit), actor.ClipFrame);
            if (weaponFrame.Rotations.Count != 1)
            {
                throw new NotSupportedException($"FFVII battle model '{actor.ModelPrefix}' weapon requires a separate multi-joint skeleton.");
            }
            visit(resource.GetWeapon(archive, actor.ModelPrefix, weaponIndex), resource.Textures, BattleRigidPose.FrameTransform(weaponFrame, actor.ModelModifiers?.PoseTransform) * world);
        }
    }
}
