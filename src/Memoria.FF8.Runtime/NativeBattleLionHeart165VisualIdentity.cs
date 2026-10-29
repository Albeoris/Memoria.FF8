using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleLionHeart165VisualIdentity
{
    private sealed record Birth(long TaskGeneration, long RowGeneration);
    private static readonly ConditionalWeakTable<NativeBattleTaskBankOwner, Dictionary<uint, Birth>> births = new();

    internal static void Allocated(NativeBattleModulePoolTask task, uint row)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(165, task);
        births.GetOrCreateValue(task.RequireOwner())[row] = new(owner.Generation, NativeBattlePilotVisualLifetimes.EpochFor(new object()));
    }

    internal static NativeBattlePilotVisualIdentity Particle(NativeBattleModulePoolTask task, uint row, uint resource)
    {
        NativeBattlePilotVisualIdentity owner = NativeBattlePilotVisualLifetimes.Identity(165, task, unchecked((int)(row * 397 + resource)));
        Dictionary<uint, Birth> rows = births.GetOrCreateValue(task.RequireOwner());
        if (!rows.TryGetValue(row, out Birth? birth) || birth.TaskGeneration != owner.Generation)
        {
            Allocated(task, row);
            birth = rows[row];
        }
        return owner with { ResourceGeneration = birth.RowGeneration };
    }

    internal static NativeBattleModulePacketBatch RebaseSprite(NativeBattleModulePacketBatch batch, NativeBattleActionMatrix parent, NativeBattleCameraProjection projection)
    {
        Vector3 CameraDirection(Vector3 point)
        {
            var r = parent.Rotation;
            return new((r[0] * point.X + r[1] * point.Y + r[2] * point.Z) / 4096f, (r[3] * point.X + r[4] * point.Y + r[5] * point.Z) / 4096f, (r[6] * point.X + r[7] * point.Y + r[8] * point.Z) / 4096f);
        }
        Vector3 World(Vector3 point) => NativeBattlePilotVisualGeometry.CameraToWorld(CameraDirection(point) + new Vector3(parent.X, parent.Y, parent.Z), projection);
        Vector3 Axis(Vector3 direction) => NativeBattlePilotVisualGeometry.CameraToWorldDirection(CameraDirection(direction), projection);
        NativeBattleGeometryPrimitive Convert(NativeBattleGeometryPrimitive primitive)
        {
            if (primitive.PilotGeometry is not { } geometry)
            {
                return primitive;
            }
            NativeBattlePilotSpriteGeometry? sprite = geometry.Sprite is { } source ? source with { Origin = World(source.Origin), AxisX = Axis(source.AxisX), AxisY = Axis(source.AxisY), AxisZ = Axis(source.AxisZ) } : null;
            return primitive with { PilotGeometry = geometry with { A = World(geometry.A), B = World(geometry.B), C = World(geometry.C), D = geometry.CornerCount == 4 ? World(geometry.D) : default, Sprite = sprite } };
        }
        return batch with
        {
            Primitives = batch.Primitives.Select(item => item with { Primitive = Convert(item.Primitive) }).ToImmutableArray(),
            CapturedWrites = batch.CapturedWrites is { } captured ? captured with { Attempts = captured.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = Convert(primitive) } : attempt).ToImmutableArray() } : null
        };
    }
}
