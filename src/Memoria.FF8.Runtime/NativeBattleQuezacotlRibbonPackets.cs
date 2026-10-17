using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlRibbonPoint(NativeBattleVector Position, short Width, short Color);

/// <summary>6C8C50/6C8DE0's sixteen projected ribbon points and fifteen common-heap GT4 candidates.</summary>
public static class NativeBattleQuezacotlRibbonPackets
{
    public static NativeBattleGfGeometryBatch Emit6C8C50(ImmutableArray<NativeBattleQuezacotlRibbonPoint> points, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, ImmutableArray<byte> distanceBytes16384)
    {
        if (points.IsDefault || points.Length != 16 || distanceBytes16384.IsDefault || distanceBytes16384.Length != 16384)
        {
            throw new InvalidDataException("Native6C8C50 requires sixteen actual points and the original128x128 distance BYTE table.");
        }
        NativeBattleModuleProjectedVertex[] projected = points.Select(point => NativeBattleModuleProjection.Project45FE10(view, point.Position, projection)).ToArray();
        NativeBattleTrailOutline.Point[] outline = projected.Select((point, index) => new NativeBattleTrailOutline.Point(point.Screen, (short)(point.Depth >> 2), () => points[index].Width)).ToArray();
        NativeBattleTrailOutline.Expand(outline, distanceBytes16384, 1024);
        ImmutableArray<int> depthFifo = projected.Skip(12).Select(point => (int)point.Depth).ToImmutableArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < 15; index++)
        {
            NativeBattleTrailOutline.Point first = outline[index];
            NativeBattleTrailOutline.Point second = outline[index + 1];
            if (first.QuarterDepth <= 0 || second.QuarterDepth <= 0)
            {
                continue;
            }
            uint rightA = first.Right ?? throw new NotSupportedException("Native ribbon requires the executed6C8DE0 right endpoint.");
            uint rightB = second.Right ?? throw new NotSupportedException("Native ribbon requires the executed6C8DE0 right endpoint.");
            int bucket = 17 + ((first.QuarterDepth + second.QuarterDepth) >> 5);
            attempts.Add(NativeBattleTrailOutline.Packet(allocated, first.Left, rightA, second.Left, rightB, points[index].Color, points[index + 1].Color, bucket, depthFifo));
            allocated = checked(allocated + 52);
        }
        return new(attempts.ToImmutable(), allocated);
    }
}
