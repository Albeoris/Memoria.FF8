using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>A posed host skeleton exposes local semantic anchors and actual segment endpoints without an FFVIII resource layout.</summary>
public sealed record NativeBattlePosedActor(NativeBattleActionMatrix World, ImmutableArray<NativeBattleVector> Endpoints, Func<byte, short, NativeBattleVector> ReadLocalBone);

/// <summary>Temporary native placement relative to the host-owned base pose.</summary>
public static class NativeBattleForeignPlacement
{
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
        if (model.Rotation12.GetValueOrDefault() == 0 && model.Rotation14.GetValueOrDefault(actor.Facing) == actor.Facing && model.Rotation16.GetValueOrDefault() == 0)
        {
            return NativeBattleForeignModelGeometry.Identity with { X = transform.Position28.X - actor.Transform.Position28.X, Y = transform.Position28.Y + transform.Vertical34 - actor.Transform.Position28.Y - actor.Transform.Vertical34, Z = transform.Position28.Z - actor.Transform.Position28.Z };
        }
        NativeBattleActionMatrix original = World(actor.Transform, 0, actor.Facing, 0);
        NativeBattleActionMatrix current = World(transform, model.Rotation12 ?? 0, model.Rotation14 ?? actor.Facing, model.Rotation16 ?? 0);
        return NativeBattleGfMatrices.Compose56C2F0(current, Inverse(original));
    }
}
