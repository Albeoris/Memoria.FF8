using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public static class NativeBattleArenaBodyDrawing
{
    public static NativeBattleGfGeometryBatch Draw(NativeBattleArenaGeometry geometry, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, uint color, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity identity, byte depthShift, int tableBaseBucket, short depthCue)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < geometry.Faces.Length; index++)
        {
            NativeBattleArenaFace face = geometry.Faces[index];
            var first = Point(face.A);
            var second = Point(face.B);
            var third = Point(face.C);
            int originX = Center(first.X, second.X, third.X);
            int originY = Center(first.Y, second.Y, third.Y);
            int originZ = Center(first.Z, second.Z, third.Z);
            NativeBattleVector Local((int X, int Y, int Z) point) => new(checked((short)(point.X - originX)), checked((short)(point.Y - originY)), checked((short)(point.Z - originZ)));
            NativeBattleForeignModelFace triangle = new(Local(first), Local(second), Local(third), face.Surface);
            NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(matrix, NativeBattleForeignModelGeometry.Identity with { X = originX, Y = originY, Z = originZ });
            NativeBattlePilotVisualIdentity selected = identity with { Primitive = unchecked(identity.Primitive * 397 + index) };
            NativeBattleRawModelBodyDraw draw = NativeBattleRawModelBodyDrawing.Draw5099D0(triangle.Triangle(), [basis], projection, color, 1, new byte[24].ToImmutableArray(), colors, depthShift, new(0, 0, 320, 216), tableBaseBucket, depthCue, selected, pilotScreenSpace: true, pilotContinuous: false);
            foreach (NativeBattleGfGeometryAttempt attempt in draw.Packets.Attempts)
            {
                NativeBattleGeometryPrimitive? primitive = attempt.Primitive;
                if (primitive?.PilotGeometry is { } pilot)
                {
                    primitive = primitive with { PilotGeometry = pilot with { ForeignSurface = face.Surface } };
                }
                attempts.Add(attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative), Primitive = primitive });
            }
            allocated = checked(allocated + draw.Packets.AllocationBytes);
        }
        return new(attempts.ToImmutable(), allocated);

        static (int X, int Y, int Z) Point(Vector3 point)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
            {
                throw new InvalidDataException("Host arena body drawing requires finite source vertices.");
            }
            return (NativePrecision53.RoundInteger(point.X), NativePrecision53.RoundInteger(point.Y), NativePrecision53.RoundInteger(point.Z));
        }

        static int Center(int first, int second, int third)
        {
            int minimum = Math.Min(first, Math.Min(second, third));
            int maximum = Math.Max(first, Math.Max(second, third));
            long extent = (long)maximum - minimum;
            if (extent > ushort.MaxValue)
            {
                throw new NotSupportedException("A host arena face exceeds the native signed local vertex domain.");
            }
            return checked(minimum + (int)((extent + 1) / 2));
        }
    }
}
