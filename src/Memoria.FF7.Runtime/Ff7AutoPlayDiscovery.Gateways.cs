using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayDiscovery
{
    private bool CanOfferGateway(PcFieldGateway gateway, IReadOnlyList<Ff7AutoPlayTarget> targets)
    {
        Ff7FieldProcess process = session.Host.Process;
        Ff7Actor? player = process.Actors.SingleOrDefault(actor => actor.PlayerControlled);
        if (session.AutoPlayState?.Active is { Accepted: true } || player is null || session.Host.Movement is null || process.Join is not null || player.Ladder is not null || player.Jump is not null || player.Offset is not null || process.Motions.Any(motion => motion.Entity == player.Entity && !motion.Completed) || session.Host.Movement.HasNativeCollisionOverride(player))
        {
            return true;
        }
        int speed = Math.Max(unchecked((ushort)session.GetPlayerMovementSpeed(true)), unchecked((ushort)session.GetPlayerMovementSpeed(false)));
        return CanOfferGatewayWithComponentRoute(ReadGatewayCrossingAvailability(session.Field.Walkmesh, process.BlockedTriangles, player, gateway, Ff7NativeMovement.ReadMaximumPlanarStep(speed)), session.Field.Walkmesh, process.BlockedTriangles, player, gateway, targets, process.Lines);
    }

    internal static bool CanOfferGatewayWithComponentRoute(bool? directAvailability, IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blocked, Ff7Actor player, PcFieldGateway gateway, IReadOnlyList<Ff7AutoPlayTarget> targets, IReadOnlyList<Ff7Line> lines)
    {
        return directAvailability != false || targets.Any(target => Ff7AutoPlayPolicy.CanEstablishFieldGatewayComponentRoute(mesh, blocked, player, gateway, target, targets, lines));
    }

    internal static bool? ReadGatewayCrossingAvailability(IReadOnlyList<PcWalkmeshTriangle> mesh, byte[] blocked, Ff7Actor player, PcFieldGateway gateway, int maximumStep)
    {
        if (maximumStep < 0 || (uint)player.Triangle >= mesh.Count || blocked.Length < (mesh.Count + 7) / 8 || player.CollisionRadius is <= 0 or > 46340)
        {
            return null;
        }
        IReadOnlySet<int> reachable = Ff7AutoplayDriver.ReachableTriangles(mesh, blocked, player.Triangle);
        Vector3[] vertices = reachable.SelectMany(index => new[] { mesh[index].A, mesh[index].B, mesh[index].C }).ToArray();
        Vector3[] gate = [gateway.A, gateway.B];
        if (vertices.Length == 0 || vertices.Concat(gate).Any(point => point.X != (short)point.X || point.Y != (short)point.Y || point.Z != (short)point.Z))
        {
            return null;
        }
        float floor = vertices[0].Z;
        if (gateway.A.Z != gateway.B.Z || vertices.Any(point => point.Z != floor) || player.Z4096 != (int)floor << 12 || !Ff7AutoplayDriver.Contains(mesh[player.Triangle], new(player.X4096 / 4096f, player.Y4096 / 4096f, floor), out _))
        {
            return null;
        }
        long minX = (long)vertices.Min(point => point.X) - 1;
        long maxX = (long)vertices.Max(point => point.X);
        long minY = (long)vertices.Min(point => point.Y) - 1;
        long maxY = (long)vertices.Max(point => point.Y);
        int actualX = player.X4096 >> 12;
        int actualY = player.Y4096 >> 12;
        if (actualX < minX || actualX > maxX || actualY < minY || actualY > maxY)
        {
            return null;
        }
        foreach (int index in reachable)
        {
            PcWalkmeshTriangle face = mesh[index];
            Vector3[] points = [face.A, face.B, face.C];
            long ux = (long)face.B.X - (long)face.A.X;
            long uy = (long)face.B.Y - (long)face.A.Y;
            long vx = (long)face.C.X - (long)face.B.X;
            long vy = (long)face.C.Y - (long)face.B.Y;
            long area = -ux * vy + vx * uy;
            if (Math.Abs(ux * vy) + Math.Abs(vx * uy) > int.MaxValue || Math.Abs(area) < 256 || Math.Abs(area * (long)floor) > int.MaxValue)
            {
                return null;
            }
            for (int edge = 0; edge < 3; edge++)
            {
                Vector3 a = points[edge];
                Vector3 b = points[(edge + 1) % 3];
                long extentX = Math.Max(Math.Abs(minX - maximumStep - (long)a.X), Math.Abs(maxX + maximumStep + 1 - (long)a.X));
                long extentY = Math.Max(Math.Abs(minY - maximumStep - (long)a.Y), Math.Abs(maxY + maximumStep + 1 - (long)a.Y));
                if (extentX * Math.Abs((long)b.Y - (long)a.Y) + extentY * Math.Abs((long)b.X - (long)a.X) > int.MaxValue)
                {
                    return null;
                }
            }
        }
        long dx = (long)gateway.B.X - (long)gateway.A.X;
        long dy = (long)gateway.B.Y - (long)gateway.A.Y;
        long dz = (long)gateway.B.Z - (long)gateway.A.Z;
        long length = dx * dx + dy * dy + dz * dz;
        long distanceX = Math.Max(Math.Abs(minX - (long)gateway.A.X), Math.Abs(maxX - (long)gateway.A.X));
        long distanceY = Math.Max(Math.Abs(minY - (long)gateway.A.Y), Math.Abs(maxY - (long)gateway.A.Y));
        long distanceZ = Math.Abs((long)floor - (long)gateway.A.Z);
        long dot = distanceX * Math.Abs(dx) + distanceY * Math.Abs(dy) + distanceZ * Math.Abs(dz);
        long maximumFraction = length == 0 ? long.MaxValue : dot * 256 / length;
        long separationX = Math.Max(Math.Abs(minX - (long)Math.Max(gateway.A.X, gateway.B.X)), Math.Abs(maxX - (long)Math.Min(gateway.A.X, gateway.B.X)));
        long separationY = Math.Max(Math.Abs(minY - (long)Math.Max(gateway.A.Y, gateway.B.Y)), Math.Abs(maxY - (long)Math.Min(gateway.A.Y, gateway.B.Y)));
        long separationZ = Math.Max(Math.Abs((long)floor - (long)gateway.A.Z), Math.Abs((long)floor - (long)gateway.B.Z));
        if (length is <= 0 or > int.MaxValue || dot * 256 > int.MaxValue || maximumFraction * Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz))) > int.MaxValue || separationX * separationX + separationY * separationY + separationZ * separationZ > int.MaxValue)
        {
            return null;
        }
        long margin = player.CollisionRadius - 1L;
        long sourceMinX = Math.Max(minX, (long)Math.Min(gateway.A.X, gateway.B.X) - margin);
        long sourceMaxX = Math.Min(maxX, (long)Math.Max(gateway.A.X, gateway.B.X) + margin);
        long sourceMinY = Math.Max(minY, (long)Math.Min(gateway.A.Y, gateway.B.Y) - margin);
        long sourceMaxY = Math.Min(maxY, (long)Math.Max(gateway.A.Y, gateway.B.Y) + margin);
        if (sourceMinX > sourceMaxX || sourceMinY > sourceMaxY)
        {
            return false;
        }
        long endMinX = Math.Max(minX, sourceMinX - maximumStep);
        long endMaxX = Math.Min(maxX, sourceMaxX + maximumStep);
        long endMinY = Math.Max(minY, sourceMinY - maximumStep);
        long endMaxY = Math.Min(maxY, sourceMaxY + maximumStep);
        long[] sides = [Side(sourceMinX, sourceMinY), Side(sourceMinX, sourceMaxY), Side(sourceMaxX, sourceMinY), Side(sourceMaxX, sourceMaxY), Side(endMinX, endMinY), Side(endMinX, endMaxY), Side(endMaxX, endMinY), Side(endMaxX, endMaxY)];
        if (sides.Any(side => side is < int.MinValue or > int.MaxValue))
        {
            return null;
        }
        return sides.All(side => side > 0) || sides.All(side => side < 0) ? false : null;

        long Side(long x, long y) => dx * (y - (long)gateway.A.Y) - (x - (long)gateway.A.X) * dy;
    }
}
