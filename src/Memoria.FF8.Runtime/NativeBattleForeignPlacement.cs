using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A posed host skeleton exposes local semantic anchors and actual segment endpoints without an FFVIII resource layout.</summary>
public sealed record NativeBattlePosedActor(NativeBattleActionMatrix World, ImmutableArray<NativeBattleVector> Endpoints, Func<byte, short, NativeBattleVector> ReadLocalBone)
{
    public ImmutableArray<int> BoneParents { get; init; } = [];
    public ImmutableArray<NativeBattleActionMatrix> BoneMatrices { get; init; } = [];

    public NativeBattleActionMatrix RequireBoneMatrix(int selector)
    {
        if (BoneMatrices.IsDefaultOrEmpty || BoneMatrices.Length != BoneParents.Length || (uint)selector >= (uint)BoneMatrices.Length)
        {
            throw new NotSupportedException("The host pose cannot supply the selected actual bone orientation.");
        }
        return NativeBattleGfMatrices.Compose56C2F0(World, BoneMatrices[selector]);
    }

    public int RequireJointCount()
    {
        if (BoneParents.IsDefaultOrEmpty || Endpoints.Length != BoneParents.Length * 2 || BoneParents.Where((parent, index) => parent < -1 || parent >= index).Any() || !BoneParents.Any(parent => parent >= 0))
        {
            throw new NotSupportedException("The host pose requires its actual bone hierarchy and at least one nonroot joint.");
        }
        return BoneParents.Length;
    }

    public NativeBattleVector? Joint(int index)
    {
        int count = RequireJointCount();
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
        return BoneParents[index] < 0 ? null : NativeBattleGfMatrices.Transform460840(World, ReadLocalBone(checked((byte)index), 0));
    }
}

/// <summary>Temporary native placement relative to the host-owned base pose.</summary>
public static class NativeBattleForeignPlacement
{
    /// <summary>The unscaled host pose basis; presented host geometry already owns its original body scale.</summary>
    public static NativeBattleActionMatrix World(NativeBattleModelTransform transform, short x, short y, short z)
    {
        return NativeBattleGfMatrices.Euler56CE30(new(x, y, z)) with { X = transform.Position28.X, Y = transform.Position28.Y + transform.Vertical34, Z = transform.Position28.Z };
    }

    public static NativeBattleActionMatrix Inverse(NativeBattleActionMatrix matrix)
    {
        ImmutableArray<short> r = matrix.Rotation;
        NativeBattleActionMatrix inverse = new([r[0], r[3], r[6], r[1], r[4], r[7], r[2], r[5], r[8]], 0, 0, 0, 4096);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(inverse, -matrix.X, -matrix.Y, -matrix.Z);
        return inverse with { X = x, Y = y, Z = z };
    }

    public static NativeBattleActionMatrix Delta(NativeBattleForeignActor actor, NativeBattleGraphicsModel model)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Temporary actor placement requires its current transform.");
        NativeBattleModelScaleVector originalScale = Scale(actor.Transform);
        NativeBattleModelScaleVector currentScale = Scale(transform);
        bool sameScale = originalScale == currentScale;
        if (sameScale && model.Rotation12.GetValueOrDefault() == 0 && model.Rotation14.GetValueOrDefault(actor.Facing) == actor.Facing && model.Rotation16.GetValueOrDefault() == 0)
        {
            return NativeBattleForeignModelGeometry.Identity with { X = transform.Position28.X - actor.Transform.Position28.X, Y = transform.Position28.Y + transform.Vertical34 - actor.Transform.Position28.Y - actor.Transform.Vertical34, Z = transform.Position28.Z - actor.Transform.Position28.Z };
        }
        NativeBattleActionMatrix original = World(actor.Transform, 0, actor.Facing, 0);
        NativeBattleActionMatrix current = World(transform, model.Rotation12 ?? 0, model.Rotation14 ?? actor.Facing, model.Rotation16 ?? 0);
        if (!sameScale)
        {
            NativeBattlePosedActor pose = actor.Pose ?? throw new NotSupportedException("Temporary actor scaling requires its actual posed root pivot.");
            NativeBattleVector pivot = !pose.Endpoints.IsDefaultOrEmpty ? pose.Endpoints[0] : throw new NotSupportedException("Temporary actor scaling requires its actual root-zero joint point.");
            (int beforeX, _, int beforeZ) = NativeBattleGfMatrices.Rotate56C3D0(current, pivot);
            current = NativeBattlePoseMatrices.Scale56BEF0(current, Relative(currentScale.X, originalScale.X), Relative(currentScale.Y, originalScale.Y), Relative(currentScale.Z, originalScale.Z));
            (int afterX, _, int afterZ) = NativeBattleGfMatrices.Rotate56C3D0(current, pivot);
            current = current with { X = unchecked(current.X + beforeX - afterX), Z = unchecked(current.Z + beforeZ - afterZ) };
        }
        return NativeBattleGfMatrices.Compose56C2F0(current, Inverse(original));
    }

    private static NativeBattleModelScaleVector Scale(NativeBattleModelTransform transform) => new(unchecked(transform.Scale154 * transform.Scale148.X) >> 12, unchecked(transform.Scale154 * transform.Scale148.Y) >> 12, unchecked(transform.Scale154 * transform.Scale148.Z) >> 12);

    private static int Relative(int current, int original)
    {
        if (current == original)
        {
            return 4096;
        }
        if (original == 0)
        {
            throw new NotSupportedException("Temporary actor scaling cannot recover a collapsed original scale axis.");
        }
        return unchecked(current * 4096) / original;
    }
}
