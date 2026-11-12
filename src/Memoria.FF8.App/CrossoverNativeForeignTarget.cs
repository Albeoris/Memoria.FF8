using System.Numerics;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>The host's posed actor geometry in the current source effect's coordinate space.</summary>
internal sealed record CrossoverNativeForeignTarget(int HostSlot, NativeBattleModelTransform Transform, NativeBattleActionMatrix World, NativeBattleEffectActorGeometry Anchors, Ff7BattleActorGeometry Geometry, Matrix4x4 HostToOrigin)
{
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
        return new(slot, new(position, position, 0, position.Y, 4096), world, new(first, second, position.Y, bounds), geometry, inverse);
    }

    public NativeBattleVector LocalVertex(Vector3 point) => Local(point, HostToOrigin, Transform.Position28);

    private static NativeBattleVector Local(Vector3 point, Matrix4x4 inverse, NativeBattleVector position)
    {
        return Word(Vector3.Transform(point, inverse) - new Vector3(position.X, position.Y, position.Z));
    }

    private static NativeBattleVector Word(Vector3 point) => new(Word(point.X), Word(point.Y), Word(point.Z));
    private static short Word(float value) => checked((short)NativePrecision53.RoundInteger(value));
}
