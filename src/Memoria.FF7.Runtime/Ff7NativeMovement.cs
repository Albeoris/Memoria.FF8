using System.Numerics;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7NativeMoveResult(Ff7Actor Actor, bool Arrived, bool Moved, int[] PushEntities, bool ClearLineBlocked = false, Ff7Actor? ProbeActor = null);

/// <summary>Integer field movement using caller-supplied native direction tables and original triangle identities.</summary>
public sealed class Ff7NativeMovement
{
    private readonly PcWalkmeshTriangle[] triangles;
    private readonly short[] vectors;
    private readonly byte[] angles;

    public Ff7NativeMovement(IReadOnlyList<PcWalkmeshTriangle> triangles, IReadOnlyList<short> directionVectors, IReadOnlyList<byte> arcAngles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        ArgumentNullException.ThrowIfNull(directionVectors);
        ArgumentNullException.ThrowIfNull(arcAngles);
        if (triangles.Count == 0 || directionVectors.Count != 512 || arcAngles.Count != 129 || directionVectors.Any(value => value is < -4096 or > 4096))
        {
            throw new ArgumentException("Native field movement requires triangles, 256 fixed-point direction pairs and 129 inverse-angle entries.");
        }
        this.triangles = triangles.ToArray();
        vectors = directionVectors.ToArray();
        angles = arcAngles.ToArray();
        for (int index = 0; index < this.triangles.Length; index++)
        {
            var triangle = this.triangles[index];
            if (triangle.Index != index || !Integral(triangle.A) || !Integral(triangle.B) || !Integral(triangle.C) || triangle.AdjacentAB < -1 || triangle.AdjacentBC < -1 || triangle.AdjacentCA < -1 || triangle.AdjacentAB >= triangles.Count || triangle.AdjacentBC >= triangles.Count || triangle.AdjacentCA >= triangles.Count)
            {
                throw new InvalidDataException("Movement triangle identity, vertices or adjacency is invalid.");
            }
        }
    }

    public int Interpolate(int start, int target, int duration, int step, bool eased)
    {
        if (duration <= 0)
        {
            throw new InvalidDataException("Native field interpolation requires a positive duration.");
        }
        int delta = target - start;
        if (eased)
        {
            int angle = ((step << 12) / duration / 32 + 128) & 255;
            return start + (vectors[angle * 2 + 1] + 4096) * delta / 8192;
        }
        return start + (delta > -524288 && delta < 524288 ? delta * step / duration : delta / duration * step);
    }

    public Ff7NativeMoveResult Step(Ff7Actor actor, short targetX, short targetY, IReadOnlyList<Ff7Actor> actors, ReadOnlySpan<byte> blockedTriangles, bool playerLocked = true, bool lineBlocked = false)
    {
        int dx = targetX - (actor.X4096 >> 12);
        int dy = targetY - (actor.Y4096 >> 12);
        int distanceSquared = unchecked(dx * dx + dy * dy);
        if (distanceSquared < 0)
        {
            throw new NotSupportedException("Movement distance exceeds the admitted signed 32-bit contract.");
        }
        int stepSquared = unchecked(actor.MoveSpeed * actor.MoveSpeed) >> 16;
        if (distanceSquared < stepSquared || distanceSquared < 4)
        {
            var arrived = actor with { X4096 = targetX << 12, Y4096 = targetY << 12 };
            return new Ff7NativeMoveResult(arrived, true, arrived != actor, []);
        }
        return StepDirection(actor, Direction(dx, dy), actors, blockedTriangles, playerLocked, lineBlocked);
    }

    public Ff7NativeMoveResult StepDirection(Ff7Actor actor, byte direction, IReadOnlyList<Ff7Actor> actors, ReadOnlySpan<byte> blockedTriangles, bool playerLocked = true, bool lineBlocked = false)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(actors);
        if ((uint)actor.Triangle >= triangles.Length || actor.MoveSpeed is < 0 or > 65535 || actor.CollisionRadius < 0)
        {
            throw new InvalidDataException("Movement actor has an invalid triangle, speed or collision radius.");
        }
        PcWalkmeshTriangle triangle = triangles[actor.Triangle];
        (int slopeX, int slopeY) = SlopeFactors(triangle);
        HashSet<int> pushed = [];
        int attempts = actor.PlayerControlled && lineBlocked ? 2 : 16;
        bool clearLine = false;
        int moveDirection = direction;
        int candidateX = actor.X4096;
        int candidateY = actor.Y4096;
        int candidateZ = actor.Z4096;
        int headCorrection = 0;
        int leftCorrection = 0;
        int rightCorrection = 0;
        bool headContact = false;
        bool leftContact = false;
        bool rightContact = false;
        int finalTriangle = actor.Triangle;
        int lastProbeX = 0;
        int lastProbeY = 0;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            int stepX = unchecked(VectorX(moveDirection) * slopeX) / 4096;
            int stepY = unchecked(-VectorY(moveDirection) * slopeY) / 4096;
            candidateX = unchecked(actor.X4096 + actor.MoveSpeed * stepX / 256);
            candidateY = unchecked(actor.Y4096 + actor.MoveSpeed * stepY / 256);
            int probeTriangle = actor.Triangle;
            int x = unchecked(candidateX + VectorX(moveDirection + 32) * actor.CollisionRadius);
            int y = unchecked(candidateY - VectorY(moveDirection + 32) * actor.CollisionRadius);
            int probeX = VectorX(moveDirection + 32) * actor.CollisionRadius;
            int probeY = -VectorY(moveDirection + 32) * actor.CollisionRadius;
            leftCorrection = Cross(ref probeTriangle, x, y, probeX, probeY, blockedTriangles, out int z);
            leftContact = Collides(actor, actors, x, y, z, pushed);
            probeTriangle = actor.Triangle;
            x = unchecked(candidateX + VectorX(moveDirection - 32) * actor.CollisionRadius);
            y = unchecked(candidateY - VectorY(moveDirection - 32) * actor.CollisionRadius);
            probeX = VectorX(moveDirection - 32) * actor.CollisionRadius;
            probeY = -VectorY(moveDirection - 32) * actor.CollisionRadius;
            rightCorrection = Cross(ref probeTriangle, x, y, probeX, probeY, blockedTriangles, out z);
            rightContact = Collides(actor, actors, x, y, z, pushed);
            probeTriangle = actor.Triangle;
            probeX = VectorX(moveDirection) * actor.CollisionRadius;
            probeY = -VectorY(moveDirection) * actor.CollisionRadius;
            lastProbeX = probeX;
            lastProbeY = probeY;
            x = unchecked(candidateX + probeX);
            y = unchecked(candidateY + probeY);
            headCorrection = Cross(ref probeTriangle, x, y, probeX, probeY, blockedTriangles, out z);
            headContact = Collides(actor, actors, x, y, z, pushed) && headCorrection == 0;
            if (headCorrection == 0 && leftCorrection == 0 && rightCorrection == 0 && !headContact && !leftContact && !rightContact)
            {
                break;
            }
            if (actor.PlayerControlled && !playerLocked)
            {
                if (headContact || leftContact || rightContact)
                {
                    break;
                }
            }
            else if (headCorrection != 0 && leftCorrection == 0 && rightCorrection == 0)
            {
                moveDirection = (moveDirection - headCorrection) & 255;
            }
            else if (headContact && !leftContact && !rightContact)
            {
                moveDirection = (moveDirection - 8) & 255;
            }
            if (leftCorrection != 0 && rightCorrection != 0)
            {
                break;
            }
            if (leftCorrection != 0 || leftContact)
            {
                moveDirection = (moveDirection - 8) & 255;
            }
            else if (rightCorrection != 0 || rightContact)
            {
                moveDirection = (moveDirection + 8) & 255;
            }
            if (attempt + 1 == attempts && attempts == 2)
            {
                clearLine = true;
            }
        }
        // The final crossing uses the last head probe, as does the native field movement contract.
        int finalCorrection = Cross(ref finalTriangle, candidateX, candidateY, lastProbeX, lastProbeY, blockedTriangles, out candidateZ);
        bool moved = headCorrection == 0 && leftCorrection == 0 && rightCorrection == 0 && !headContact && !leftContact && !rightContact && finalCorrection == 0;
        Ff7Actor result = actor with { Direction = moveDirection, Triangle = finalTriangle };
        if (moved)
        {
            result = result with { X4096 = candidateX, Y4096 = candidateY, Z4096 = unchecked(candidateZ << 12) };
        }
        return new Ff7NativeMoveResult(result, false, moved, pushed.Order().ToArray(), clearLine, result with { X4096 = candidateX, Y4096 = candidateY, Z4096 = unchecked(candidateZ << 12) });
    }

    public byte Direction(int dx, int dy)
    {
        int squared = unchecked(dx * dx + dy * dy);
        if (squared <= 0)
        {
            throw new ArgumentException("A movement direction requires a nonzero in-range vector.");
        }
        int distance = IntegerSquareRoot(squared);
        int x = unchecked(dx << 12) / distance / 32;
        int y = unchecked(dy << 12) / distance / 32;
        int angle;
        if (x * x > y * y)
        {
            angle = x > 0 ? y > 0 ? angles[y] : 256 - angles[-y] : y > 0 ? 128 - angles[y] : angles[-y] + 128;
        }
        else
        {
            angle = y > 0 ? x > 0 ? 64 - angles[x] : 64 + angles[-x] : x > 0 ? 192 + angles[x] : 192 - angles[-x];
        }
        return unchecked((byte)(angle - 192));
    }

    public int ResolveHeight(int x, int y, int triangleId)
    {
        if ((uint)triangleId >= triangles.Length)
        {
            throw new InvalidDataException("Movement height requires a valid triangle.");
        }
        var triangle = triangles[triangleId];
        int ax = (int)triangle.A.X;
        int ay = (int)triangle.A.Y;
        int az = (int)triangle.A.Z;
        int ux = (int)triangle.B.X - ax;
        int uy = (int)triangle.B.Y - ay;
        int uz = (int)triangle.B.Z - az;
        int vx = (int)triangle.C.X - (int)triangle.B.X;
        int vy = (int)triangle.C.Y - (int)triangle.B.Y;
        int vz = (int)triangle.C.Z - (int)triangle.B.Z;
        int nx = unchecked(-uy * vz + vy * uz);
        int ny = unchecked(-uz * vx + ux * vz);
        int nz = unchecked(-ux * vy + vx * uy);
        if (nz == 0)
        {
            throw new InvalidDataException("Movement triangle has no projected area.");
        }
        return unchecked(nx * ax + ny * ay + nz * az - nx * x - ny * y) / nz;
    }

    private static (int X, int Y) SlopeFactors(PcWalkmeshTriangle triangle)
    {
        int ux = (int)(triangle.B.X - triangle.A.X);
        int uy = (int)(triangle.B.Y - triangle.A.Y);
        int uz = (int)(triangle.B.Z - triangle.A.Z);
        int vx = (int)(triangle.C.X - triangle.B.X);
        int vy = (int)(triangle.C.Y - triangle.B.Y);
        int vz = (int)(triangle.C.Z - triangle.B.Z);
        float x = unchecked(uy * vz - uz * vy) / 256;
        float y = unchecked(uz * vx - ux * vz) / 256;
        float z = unchecked(ux * vy - uy * vx) / 256;
        float squared = (float)((double)x * x + (double)y * y + (double)z * z);
        float length = (float)Math.Sqrt(squared);
        if (!(length > 0))
        {
            throw new InvalidDataException("Movement triangle has a zero normal.");
        }
        float factor = (float)(4096.0 / length);
        int nx = (int)((double)x * factor);
        int ny = (int)((double)y * factor);
        int nz = (int)((double)z * factor);
        int rootX = SquareRoot12(unchecked(nx * nx) / 4096 + unchecked(nz * nz) / 4096);
        int rootY = SquareRoot12(unchecked(ny * ny) / 4096 + unchecked(nz * nz) / 4096);
        if (rootX == 0 || rootY == 0)
        {
            throw new InvalidDataException("Movement triangle slope has an undefined projected normal.");
        }
        int scaleX = Math.Clamp(unchecked(nz << 12) / rootX, -4096, 4096);
        int scaleY = Math.Clamp(unchecked(nz << 12) / rootY, -4096, 4096);
        return (Math.Abs(scaleX), Math.Abs(scaleY));
    }

    private static int SquareRoot12(int value)
    {
        if (value < 0)
        {
            throw new InvalidDataException("Movement fixed-point root received a negative value.");
        }
        return (int)(Math.Sqrt(value / 4096.0) * 4096.0);
    }

    private int Cross(ref int triangleId, int x4096, int y4096, int probeX, int probeY, ReadOnlySpan<byte> blocked, out int height)
    {
        int x = x4096 / 4096;
        int y = y4096 / 4096;
        for (int attempt = 0; attempt <= triangles.Length; attempt++)
        {
            var face = triangles[triangleId];
            var vertices = new[] { face.A, face.B, face.C };
            int[] adjacent = [face.AdjacentAB, face.AdjacentBC, face.AdjacentCA];
            int edge = -1;
            for (int index = 0; index < 3; index++)
            {
                Vector3 a = vertices[index];
                Vector3 b = vertices[(index + 1) % 3];
                int cross = unchecked((x - (int)a.X) * ((int)b.Y - (int)a.Y) - (y - (int)a.Y) * ((int)b.X - (int)a.X));
                if (cross < 0)
                {
                    edge = index;
                    break;
                }
            }
            if (edge == -1)
            {
                height = ResolveHeight(x, y, triangleId);
                return 0;
            }
            int next = adjacent[edge];
            bool locked = next >= 0 && (next >> 3) < blocked.Length && (blocked[next >> 3] & (1 << (next & 7))) != 0;
            if (next < 0 || locked)
            {
                Vector3 a = vertices[edge];
                Vector3 b = vertices[(edge + 1) % 3];
                int dot = unchecked(((int)b.X - (int)a.X) * probeX + ((int)b.Y - (int)a.Y) * probeY);
                height = ResolveHeight(x, y, triangleId);
                return dot >= 0 ? 8 : -8;
            }
            triangleId = next;
        }
        throw new InvalidDataException("Movement triangle traversal cycles without locating a face or blocked edge.");
    }

    private static bool Collides(Ff7Actor actor, IReadOnlyList<Ff7Actor> others, int x, int y, int z, HashSet<int> pushed)
    {
        bool collision = false;
        foreach (Ff7Actor other in others)
        {
            if (other.Slot == actor.Slot || !other.CollisionEnabled)
            {
                continue;
            }
            int dz = (other.Z4096 >> 12) - z;
            if (dz <= -127 || dz >= 128)
            {
                continue;
            }
            int dx = unchecked(other.X4096 - x) >> 12;
            int dy = unchecked(other.Y4096 - y) >> 12;
            int radius = (actor.CollisionRadius + other.CollisionRadius) / 2;
            if (unchecked(radius * radius) > unchecked(dx * dx + dy * dy))
            {
                collision = true;
                if (actor.PlayerControlled)
                {
                    pushed.Add(other.Entity);
                }
            }
        }
        return collision;
    }

    private int VectorX(int direction) => vectors[(direction & 255) * 2];
    private int VectorY(int direction) => vectors[(direction & 255) * 2 + 1];
    private static bool Integral(Vector3 value) => value.X == (short)value.X && value.Y == (short)value.Y && value.Z == (short)value.Z;
    private static int IntegerSquareRoot(int value)
    {
        int root = (int)Math.Sqrt(value);
        while ((long)(root + 1) * (root + 1) <= value)
        {
            root++;
        }
        while ((long)root * root > value)
        {
            root--;
        }
        return root;
    }
}
