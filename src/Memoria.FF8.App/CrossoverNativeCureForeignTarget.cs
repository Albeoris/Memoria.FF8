using System.Numerics;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>The host's posed actor geometry in the current source effect's coordinate space.</summary>
internal sealed record CrossoverNativeCureForeignTarget(int HostSlot, NativeBattleModelTransform Transform, NativeBattleActionMatrix World, NativeBattleEffectActorGeometry Anchors, Ff7BattleActorGeometry Geometry, Matrix4x4 HostToOrigin)
{
    public static CrossoverNativeCureForeignTarget Read(Ff7BattleSession host, int slot, Ff7BattleActorGeometry geometry, Matrix4x4 originToHost)
    {
        if (slot is < 0 or >= 3 || geometry.SkeletonPoints.IsDefaultOrEmpty || geometry.Faces.IsDefaultOrEmpty || !Matrix4x4.Invert(originToHost, out Matrix4x4 inverse))
        {
            throw new InvalidDataException("Mixed Cure requires the actual posed host ally geometry.");
        }
        NativeBattleVector position = Word(Vector3.Transform(host.RenderActorPlacement(slot).Position, inverse));
        NativeBattleVector first = Word(Vector3.Transform(geometry.SkeletonPoints[0], inverse));
        NativeBattleVector second = Word(Vector3.Transform(host.GuestEffectTargetAnchor(slot), inverse));
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
