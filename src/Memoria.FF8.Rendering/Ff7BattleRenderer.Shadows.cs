using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF8.Rendering;

public sealed partial class Ff7BattleRenderer
{
    private readonly Ff7BattleShadowResources? shadow;
    private readonly Dictionary<string, (ushort X, ushort Z, short Offset)> shadowParameters = new(StringComparer.OrdinalIgnoreCase);

    public ImmutableArray<Vector3> CaptureActorShadow(Ff7BattleActorVisual actor)
    {
        if (shadow is null || actor.ShadowSuppressed || actor.ModelModifiers is { Visible: false } || actor.ModelModifiers is { Alpha: 0 })
        {
            return [];
        }
        if (!shadowParameters.TryGetValue(actor.ModelPrefix, out var parameters))
        {
            byte[] data = Read(archive, actor.ModelPrefix + "ab");
            if (data.Length < 12)
            {
                throw new InvalidDataException("FFVII actor shadow requires its original action header.");
            }
            parameters = (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(6)), BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8)), BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(10)));
            shadowParameters.Add(actor.ModelPrefix, parameters);
        }
        Matrix4x4 placement = Matrix4x4.CreateRotationY(actor.FacingRadians) * Matrix4x4.CreateTranslation(actor.Position);
        placement *= actor.PresentationTransform ?? Matrix4x4.Identity;
        ActorModel resource = LoadActor(actor.ModelPrefix);
        BattleClipFrame frame = Frame(resource.GetClip(actor.ClipIndex, false, actor.Limit), actor.ClipFrame);
        Vector3 ground = Vector3.Transform(Vector3.Zero, BattleRigidPose.FrameTransform(frame, actor.ModelModifiers?.PoseTransform) * placement);
        ground.Y = shadow.GroundHeight;
        placement.Translation = ground;
        Matrix4x4 shape = Matrix4x4.CreateScale(parameters.X / 4096f, 1, parameters.Z / 4096f) * Matrix4x4.CreateTranslation(0, 0, parameters.Offset == -1 ? 0 : parameters.Offset) * placement;
        return shadow.Vertices.Select(vertex => Vector3.Transform(vertex, shape)).ToImmutableArray();
    }

    private void DrawActorShadow(ITriangleRenderer renderer, Ff7BattleActorVisual actor, CameraView view)
    {
        ImmutableArray<Vector3> vertices = CaptureActorShadow(actor);
        if (vertices.IsEmpty)
        {
            return;
        }
        // The PC shadow callback replaces the material color with black and alpha 0x60.
        Vector4 color = new(0, 0, 0, (96 * (actor.ModelModifiers?.Alpha ?? 255) / 255) / 255f);
        foreach (Ff7BattleShadowFace face in shadow!.Faces)
        {
            BattleFloorShadowSubmission.Draw(renderer, vertices[face.A], vertices[face.B], vertices[face.C], color, color, color, view.Transform, view.Width, view.Height, view.Focal, TriangleBlend.Alpha);
        }
    }
}
