using System.Numerics;
using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>The host's posed actor geometry in the current source effect's coordinate space.</summary>
internal sealed record CrossoverNativeForeignTarget(int HostSlot, NativeBattleModelTransform Transform, NativeBattleActionMatrix World, NativeBattleEffectActorGeometry Anchors, Ff7BattleActorGeometry Geometry, Matrix4x4 HostToOrigin)
{
    public short Facing { get; init; }

    public NativeBattleForeignActor ToActor(int index)
    {
        ImmutableArray<NativeBattleForeignModelFace> faces;
        if (!Geometry.GuestFaces.IsDefaultOrEmpty)
        {
            if (Geometry.GuestSurfaces.IsDefault || Geometry.GuestSurfaces.Length != Geometry.GuestFaces.Length)
            {
                throw new NotSupportedException("The host guest geometry has no original material provider.");
            }
            faces = Geometry.GuestFaces.Select((face, index) => new NativeBattleForeignModelFace(LocalVertex(face.A), LocalVertex(face.B), LocalVertex(face.C), Geometry.GuestSurfaces[index])).ToImmutableArray();
        }
        else
        {
            faces = Geometry.Faces.Select(face => new NativeBattleForeignModelFace(LocalVertex(face.A), LocalVertex(face.B), LocalVertex(face.C), face.Surface ?? throw new NotSupportedException("The host posed geometry has no original material provider."))).ToImmutableArray();
        }
        if (faces.IsEmpty)
        {
            throw new NotSupportedException("The host posed geometry has no material-bearing triangle provider.");
        }
        NativeBattleVector anchor = Anchors.Anchor241;
        return new(index, Transform, new(anchor.X, anchor.Y, anchor.Z, checked((short)(Anchors.LocalBounds.Maximum.Y - Anchors.LocalBounds.Minimum.Y))), ReadBone)
        {
            Facing = Facing,
            BoneCount = Geometry.Bones.Length,
            Geometry = new(World, Anchors, faces)
        };

        NativeBattleVector ReadBone(byte selector, short fraction)
        {
            if (selector is 240 or 241)
            {
                return ReadSemanticBone(selector, fraction, anchor, () => Anchors.Anchor240);
            }
            if (selector >= Geometry.Bones.Length || fraction is < 0 or > 4096)
            {
                throw new NotSupportedException("The host skeleton cannot supply the selected posed bone segment.");
            }
            (Vector3 start, Vector3 end) = Geometry.Bones[selector];
            NativeBattleVector first = Word(Vector3.Transform(start, HostToOrigin));
            NativeBattleVector second = Word(Vector3.Transform(end, HostToOrigin));
            return new(Interpolate(first.X, second.X), Interpolate(first.Y, second.Y), Interpolate(first.Z, second.Z));

            short Interpolate(short from, short to) => checked((short)(from + ((to - from) * fraction >> 12)));
        }
    }
    /// <summary>Foreign F0 is the posed root-to-impact segment; F1 is the host's canonical impact point.</summary>
    public static NativeBattleVector ReadSemanticBone(byte selector, short fraction, NativeBattleVector impact, Func<NativeBattleVector> readRoot)
    {
        if (selector == 241 && fraction == 0 || selector == 240 && fraction == 4096)
        {
            return impact;
        }
        if (selector != 240 || fraction is < 0 or > 4096)
        {
            throw new NotSupportedException($"The host target cannot supply native bone selector {selector:X2} at fraction {fraction}.");
        }
        NativeBattleVector root = readRoot();
        return new(Interpolate(root.X, impact.X), Interpolate(root.Y, impact.Y), Interpolate(root.Z, impact.Z));

        short Interpolate(short start, short end) => checked((short)(start + ((end - start) * fraction >> 12)));
    }

    public static CrossoverNativeForeignTarget Read(Ff7BattleSession host, int slot, Ff7BattleActorGeometry geometry, Matrix4x4 originToHost)
    {
        if (slot is < 0 or 3 or > 9 || geometry.SkeletonPoints.IsDefaultOrEmpty || geometry.Faces.IsDefaultOrEmpty && geometry.GuestFaces.IsDefaultOrEmpty || !Matrix4x4.Invert(originToHost, out Matrix4x4 inverse))
        {
            throw new InvalidDataException("The native effect requires the actual posed host actor geometry.");
        }
        NativeBattleVector position = Word(Vector3.Transform(geometry.GuestRoot ?? host.RenderActorPlacement(slot).Position, inverse));
        NativeBattleVector first = Word(Vector3.Transform(geometry.SkeletonPoints[0], inverse));
        NativeBattleVector second = Word(Vector3.Transform(geometry.GuestEffectAnchor ?? host.GuestEffectTargetAnchor(slot), inverse));
        NativeBattleVector[] joints = geometry.SkeletonPoints.Select(point => Local(point, inverse, position)).ToArray();
        NativeBattleEffectLocalBounds bounds = new(new(joints.Min(point => point.X), joints.Min(point => point.Y), joints.Min(point => point.Z)), new(joints.Max(point => point.X), joints.Max(point => point.Y), joints.Max(point => point.Z)));
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], position.X, position.Y, position.Z, 4096);
        float yaw = host.RenderActorPlacement(slot).Yaw * MathF.Tau / 4096;
        Vector3 facing = Vector3.TransformNormal(new(MathF.Sin(yaw), 0, MathF.Cos(yaw)), inverse);
        return new(slot, new(position, position, 0, position.Y, 4096), world, new(first, second, position.Y, bounds), geometry, inverse) { Facing = unchecked((short)NativePrecision53.RoundInteger(Math.Atan2(facing.X, facing.Z) * 4096 / (2 * Math.PI))) };
    }

    public NativeBattleVector LocalVertex(Vector3 point) => Local(point, HostToOrigin, Transform.Position28);

    private static NativeBattleVector Local(Vector3 point, Matrix4x4 inverse, NativeBattleVector position)
    {
        return Word(Vector3.Transform(point, inverse) - new Vector3(position.X, position.Y, position.Z));
    }

    private static NativeBattleVector Word(Vector3 point) => new(Word(point.X), Word(point.Y), Word(point.Z));
    private static short Word(float value) => checked((short)NativePrecision53.RoundInteger(value));
}
