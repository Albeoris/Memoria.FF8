using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public enum FieldNativeMovementStatus
{
    Accepted,
    Blocked,
    Unsupported
}

public readonly record struct FieldNativeMovementRequest(int Triangle, int FixedX, int FixedY, int FixedZ, byte Direction, short Speed, ushort Radius, bool IsLeader, bool ScriptControlled, byte EarlyRetryState, bool ActorEnvelopesClear);

public readonly record struct FieldNativeSlopeFactors(int NormalX, int NormalY, int NormalZ, int X, int Y);

public sealed record FieldNativeGroundProbe(int FixedX, int FixedY, int RadiusOffsetX, int RadiusOffsetY, int Triangle, int? NativeHeight, int Steering, IReadOnlyList<int> TraversedTriangles, string? UnsupportedReason, bool ActorCollision = false)
{
    public bool Supported => UnsupportedReason is null;
}

public sealed record FieldNativeMovementProposal(int Attempt, byte Direction, int FixedX, int FixedY, IReadOnlyList<FieldNativeGroundProbe> FanProbes);

public sealed record FieldNativeMovementResult(FieldNativeMovementStatus Status, int FixedX, int FixedY, int FixedZ, int Triangle, byte Direction, byte EarlyRetryState, int RetryCount, FieldNativeSlopeFactors? Factors, IReadOnlyList<FieldNativeMovementProposal> Proposals, FieldNativeGroundProbe? CenterProbe, string? UnsupportedReason);

/// <summary>Native 0x479C60 geometry and steering with caller-certified clear actor envelopes or pure ordinary-leader fan contact authority. Field triggers remain the caller's responsibility before applying the result.</summary>
public static class FieldNativeMovementProbe
{
    private readonly record struct Point(int X, int Y, int Z);

    public static FieldNativeMovementResult Resolve(FieldWalkmesh mesh, FieldNativeMovementRequest request, IReadOnlySet<int>? lockedTriangles = null, Func<FieldNativeGroundProbe, bool>? hasActorCollision = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        List<FieldNativeMovementProposal> proposals = [];
        byte direction = request.Direction;
        byte earlyRetryState = request.EarlyRetryState;
        int retryCount = 0;
        FieldNativeSlopeFactors? factors = null;

        FieldNativeMovementResult Unsupported(string reason, FieldNativeGroundProbe? center = null)
        {
            return new FieldNativeMovementResult(FieldNativeMovementStatus.Unsupported, request.FixedX, request.FixedY, request.FixedZ, center?.Triangle ?? request.Triangle, direction, earlyRetryState, retryCount, factors, proposals.AsReadOnly(), center, reason);
        }

        if (!request.ActorEnvelopesClear)
        {
            return Unsupported("Native actor-contact ordering and steering require a separate caller contract.");
        }
        if (!TryFactors(mesh, request.Triangle, out FieldNativeSlopeFactors slopeFactors, out string? factorReason))
        {
            return Unsupported(factorReason!);
        }
        factors = slopeFactors;
        bool ordinaryLeader = request.IsLeader && !request.ScriptControlled;
        int firstFanTriangle = request.Triangle;
        while (true)
        {
            retryCount++;
            if (request.IsLeader && earlyRetryState == 1)
            {
                if (retryCount > 2)
                {
                    earlyRetryState = 0;
                    break;
                }
            }
            else if (retryCount > 16)
            {
                break;
            }

            int x = unchecked(request.FixedX + (slopeFactors.X * FieldNativeWalkmeshProbe.Sine(direction) / 4096) * request.Speed / 256);
            int y = unchecked(request.FixedY + (slopeFactors.Y * FieldNativeWalkmeshProbe.Sine(direction + 64) / -4096) * request.Speed / 256);
            List<FieldNativeGroundProbe> fans = [];
            foreach (int angleOffset in new[] { 32, -32, 0 })
            {
                int offsetX = request.Radius * FieldNativeWalkmeshProbe.Sine(direction + angleOffset);
                int offsetY = -request.Radius * FieldNativeWalkmeshProbe.Sine(direction + angleOffset + 64);
                // 0x47A066 and 0x47A118 reset the local triangle only before the -32 and straight fans.
                int fanTriangle = angleOffset == 32 ? firstFanTriangle : request.Triangle;
                FieldNativeGroundProbe fan = Probe(mesh, fanTriangle, unchecked(x + offsetX), unchecked(y + offsetY), offsetX, offsetY, lockedTriangles);
                if (fan.Supported && hasActorCollision is not null)
                {
                    fan = fan with { ActorCollision = hasActorCollision(fan) };
                }
                fans.Add(fan);
                if (!fan.Supported)
                {
                    proposals.Add(new FieldNativeMovementProposal(retryCount, direction, x, y, fans.AsReadOnly()));
                    return Unsupported(fan.UnsupportedReason!);
                }
            }
            firstFanTriangle = fans[2].Triangle;
            proposals.Add(new FieldNativeMovementProposal(retryCount, direction, x, y, fans.AsReadOnly()));
            int plus = fans[0].Steering;
            int minus = fans[1].Steering;
            int straight = fans[2].Steering;
            bool plusCollision = fans[0].ActorCollision;
            bool minusCollision = fans[1].ActorCollision;
            bool straightCollision = fans[2].ActorCollision;
            bool collision = plusCollision || minusCollision || straightCollision;
            if (ordinaryLeader && collision)
            {
                // Native479C60 ordinary leader breaks on any47A720 collision
                // flag before ground steering, then still runs its center/line
                // calls and refuses the final XYZ commit.
                break;
            }
            if (plus == 0 && minus == 0 && straight == 0 && !collision)
            {
                break;
            }
            if (!ordinaryLeader && straight != 0 && plus == 0 && minus == 0)
            {
                direction = unchecked((byte)(direction - straight));
            }
            else if (!ordinaryLeader && straightCollision && !plusCollision && !minusCollision)
            {
                direction = unchecked((byte)(direction - 4));
            }
            if (plus != 0)
            {
                if (minus != 0)
                {
                    break;
                }
                direction = unchecked((byte)(direction - 8));
            }
            else if (plusCollision)
            {
                direction = unchecked((byte)(direction - 8));
            }
            else if (minus != 0 || minusCollision)
            {
                direction = unchecked((byte)(direction + 8));
            }
        }

        FieldNativeMovementProposal last = proposals[^1];
        FieldNativeGroundProbe lastStraight = last.FanProbes[2];
        // 0x47A2AC retains the last straight fan's offset vector for the final center steering test.
        FieldNativeGroundProbe centerProbe = Probe(mesh, request.Triangle, last.FixedX, last.FixedY, lastStraight.RadiusOffsetX, lastStraight.RadiusOffsetY, lockedTriangles);
        if (!centerProbe.Supported)
        {
            return Unsupported(centerProbe.UnsupportedReason!, centerProbe);
        }
        if (ordinaryLeader)
        {
            // 0x47A2F6 clears the early byte after the field-line call even when geometry rejects the step.
            earlyRetryState = 0;
        }
        bool accepted = centerProbe.Steering == 0 && last.FanProbes.All(probe => probe.Steering == 0 && !probe.ActorCollision);
        return new FieldNativeMovementResult(accepted ? FieldNativeMovementStatus.Accepted : FieldNativeMovementStatus.Blocked, accepted ? last.FixedX : request.FixedX, accepted ? last.FixedY : request.FixedY, accepted ? unchecked(centerProbe.NativeHeight!.Value << 12) : request.FixedZ, centerProbe.Triangle, direction, earlyRetryState, retryCount, factors, proposals.AsReadOnly(), centerProbe, null);
    }

    /// <summary>Native ordered adjacency/IDLOCK query, retaining blocked steering and plane height instead of discarding them.</summary>
    public static FieldNativeGroundProbe Probe(FieldWalkmesh mesh, int triangleIndex, int fixedX, int fixedY, int radiusOffsetX, int radiusOffsetY, IReadOnlySet<int>? lockedTriangles = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        List<int> traversed = [];
        HashSet<int> visited = [];
        int x = fixedX / 4096;
        int y = fixedY / 4096;
        int edgeX = unchecked((short)x);
        int edgeY = unchecked((short)y);
        while (true)
        {
            if ((uint)triangleIndex >= mesh.Triangles.Count || triangleIndex > short.MaxValue)
            {
                return UnsupportedProbe("Native adjacency points outside the bounded triangle table.");
            }
            if (!visited.Add(triangleIndex))
            {
                return UnsupportedProbe("Native adjacency would cycle without reaching a containing or blocked triangle.");
            }
            traversed.Add(triangleIndex);
            WalkmeshTriangle triangle = mesh.Triangles[triangleIndex];
            if (!TryPoint(triangle.A, out Point a) || !TryPoint(triangle.B, out Point b) || !TryPoint(triangle.C, out Point c))
            {
                return UnsupportedProbe("Native geometry requires original signed-WORD vertices.");
            }
            Point start;
            Point end;
            int neighbor;
            if (Edge(a, b, edgeX, edgeY) < 0)
            {
                start = a;
                end = b;
                neighbor = triangle.NeighborA;
            }
            else if (Edge(b, c, edgeX, edgeY) < 0)
            {
                start = b;
                end = c;
                neighbor = triangle.NeighborB;
            }
            else if (Edge(c, a, edgeX, edgeY) < 0)
            {
                start = c;
                end = a;
                neighbor = triangle.NeighborC;
            }
            else
            {
                return FinishProbe(0);
            }
            if (neighbor < short.MinValue || neighbor > short.MaxValue)
            {
                return UnsupportedProbe("Native adjacency requires a signed-WORD neighbor index.");
            }
            if (neighbor >= 0 && lockedTriangles?.Contains(neighbor) != true)
            {
                triangleIndex = neighbor;
                continue;
            }
            int dot = unchecked((end.X - start.X) * radiusOffsetX + (end.Y - start.Y) * radiusOffsetY);
            return FinishProbe(dot < 0 ? -8 : 8);
        }

        FieldNativeGroundProbe UnsupportedProbe(string reason)
        {
            return new FieldNativeGroundProbe(fixedX, fixedY, radiusOffsetX, radiusOffsetY, triangleIndex, null, 0, traversed.AsReadOnly(), reason);
        }

        FieldNativeGroundProbe FinishProbe(int steering)
        {
            try
            {
                int? height = FieldPartyTraceGeometry.ResolveTriangleHeight(mesh, triangleIndex, x, y);
                return new FieldNativeGroundProbe(fixedX, fixedY, radiusOffsetX, radiusOffsetY, triangleIndex, height, steering, traversed.AsReadOnly(), height is null ? "Native plane triangle is unavailable." : null);
            }
            catch (OverflowException)
            {
                return UnsupportedProbe("Native signed plane division would overflow.");
            }
        }
    }

    private static bool TryFactors(FieldWalkmesh mesh, int triangleIndex, out FieldNativeSlopeFactors factors, out string? reason)
    {
        factors = default;
        reason = null;
        if ((uint)triangleIndex >= mesh.Triangles.Count || triangleIndex > short.MaxValue)
        {
            reason = "Original actor triangle is outside the bounded native table.";
            return false;
        }
        WalkmeshTriangle triangle = mesh.Triangles[triangleIndex];
        if (!TryPoint(triangle.A, out Point a) || !TryPoint(triangle.B, out Point b) || !TryPoint(triangle.C, out Point c))
        {
            reason = "Native geometry requires original signed-WORD vertices.";
            return false;
        }
        Point u = new(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        Point v = new(c.X - b.X, c.Y - b.Y, c.Z - b.Z);
        int nx = unchecked(u.Y * v.Z - u.Z * v.Y) / 256;
        int ny = unchecked(u.Z * v.X - u.X * v.Z) / 256;
        int nz = unchecked(u.X * v.Y - u.Y * v.X) / 256;
        long squaredLength = (long)nx * nx + (long)ny * ny + (long)nz * nz;
        if (squaredLength == 0)
        {
            reason = "Quantized normal is zero; native actor-zero triangle fallback is outside this contract.";
            return false;
        }

        //56BC50 uses the selected PC precision53 profile, shared with camera
        //and world conversion. Cross/256 is bounded by2^23, so the squared sum
        //is exact; Y/Z are stored as FLOAT before the magic DOUBLE conversion.
        double root = Math.Sqrt(squaredLength);
        double scale = 4096.0 / root;
        nx = NativePrecision53.RoundInteger(nx * scale);
        ny = NativePrecision53.RoundInteger((float)(ny * scale));
        nz = NativePrecision53.RoundInteger((float)(nz * scale));
        int dx = NativeSquareRoot(unchecked(nx * nx / 4096 + nz * nz / 4096));
        int dy = NativeSquareRoot(unchecked(ny * ny / 4096 + nz * nz / 4096));
        if (dx == 0)
        {
            reason = "Native zero-X denominator replaces actor triangle with actor zero's triangle; caller state is required.";
            return false;
        }
        int fx = Math.Abs(Math.Clamp(unchecked(nz << 12) / dx, -4096, 4096));
        int fy = dy == 0 ? 0 : Math.Abs(Math.Clamp(unchecked(nz << 12) / dy, -4096, 4096));
        factors = new FieldNativeSlopeFactors(nx, ny, nz, fx, fy);
        return true;
    }

    private static int NativeSquareRoot(int value)
    {
        // 0x56BED0 truncates sqrt(value / 4096) * 4096. Here value is in [0,8192].
        // Exact squares have an exactly representable x87 root; other squares are far from integer boundaries.
        long squared = (long)value * 4096;
        int result = (int)Math.Sqrt(squared);
        while ((long)(result + 1) * (result + 1) <= squared)
        {
            result++;
        }
        while ((long)result * result > squared)
        {
            result--;
        }
        return result;
    }

    private static int Edge(Point start, Point end, int x, int y)
    {
        return unchecked(x * (end.Y - start.Y) + end.X * (start.Y - y) + start.X * (y - end.Y));
    }

    private static bool TryPoint(Vector3 value, out Point point)
    {
        point = default;
        if (!Word(value.X) || !Word(value.Y) || !Word(value.Z))
        {
            return false;
        }
        point = new Point((short)value.X, (short)value.Y, (short)value.Z);
        return true;

        static bool Word(float value)
        {
            return float.IsFinite(value) && value == MathF.Truncate(value) && value is >= short.MinValue and <= short.MaxValue;
        }
    }
}
