using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanReticleRing(NativeBattleModulePacketBatch Packets, NativeBattleScanReticlePoint? FinalPoint);

/// <summary>84D6D0/84E1F0/84E290's unprojected green line packets, relative to the caller's private Scan cursor.</summary>
public static class NativeBattleScanReticleLines
{
    public static NativeBattleScanReticleRing Ring84D6D0(short x, short y, short radius, short angle, short count, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder lines = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        int dx = Component(NativeBattleCameraMath.Cosine(angle), radius);
        int dy = Component(NativeBattleCameraMath.Sine(angle), radius);
        NativeBattleScanReticlePoint? final = null;
        for (int index = 0; index < count; index++)
        {
            angle = (short)((angle + 128) & 0xFFF);
            int nextX = Component(NativeBattleCameraMath.Cosine(angle), radius);
            int nextY = Component(NativeBattleCameraMath.Sine(angle), radius);
            NativeBattleScanReticlePoint start = Point(x + dx + 160, y + dy + 108);
            final = Point(x + nextX + 160, y + nextY + 108);
            lines.Add(new(Line84E290(start, final, pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + index) } : null), index * 16, 1));
            dx = nextX;
            dy = nextY;
        }
        return new(new(lines.ToImmutable(), lines.Count * 16), final);
    }

    public static NativeBattleModulePacketBatch FourLines84E1F0(short x, short y, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleScanReticlePoint center = Point(x + 160, y + 108);
        NativeBattleScanReticlePoint[] ends = [new(16, 12), new(304, 12), new(16, 204), new(304, 204)];
        return new(ends.Select((end, index) => new NativeBattleAllocatedPrimitive(Line84E290(center, end, pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + index) } : null), index * 16, 1)).ToImmutableArray(), 64);
    }

    public static NativeBattleGeometryPrimitive Line84E290(NativeBattleScanReticlePoint start, NativeBattleScanReticlePoint end, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGeometryPrimitive primitive = new(5, [0x03000000, 0x4000FF00, Pack(start), Pack(end)], []);
        return pilotIdentity is { } identity ? primitive with { PilotGeometry = new(identity, new(start.X, start.Y, 0), new(end.X, end.Y, 0), default, default, 2) { ScreenSpace = true } } : primitive;
    }

    private static int Component(int trigonometric, short radius)
    {
        return unchecked(trigonometric * radius) / 4096;
    }

    private static NativeBattleScanReticlePoint Point(int x, int y)
    {
        return new(unchecked((short)x), unchecked((short)y));
    }

    private static uint Pack(NativeBattleScanReticlePoint point)
    {
        return (uint)(ushort)point.X | (uint)(ushort)point.Y << 16;
    }
}
