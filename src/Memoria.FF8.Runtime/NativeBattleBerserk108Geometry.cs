using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBerserk108RayStep(ImmutableArray<byte> Rays, int Alive, NativeBattleGfGeometryBatch Packets);

public static class NativeBattleBerserk108Geometry
{
    public static ImmutableArray<byte> UpdateRay(ImmutableArray<byte> source)
    {
        if (source.Length != 48)
        {
            throw new InvalidDataException("Native Berserk requires its complete48-byte ray record.");
        }
        byte[] next = source.ToArray();
        W(4, R(4) + R(32));
        int total = 0;
        for (int color = 8; color <= 24; color += 8)
        {
            for (int channel = 0; channel < 3; channel++)
            {
                int offset = color + channel;
                next[offset] = (byte)Math.Max(0, next[offset] - next[offset + 4]);
                total |= next[offset];
            }
        }
        if (total == 0)
        {
            W(6, 0);
            return next.ToImmutableArray();
        }
        W(36, R(36) + R(38));
        W(34, R(34) + R(36));
        if (R(34) <= 0)
        {
            W(6, 0);
            return next.ToImmutableArray();
        }
        W(42, R(42) + R(44));
        W(40, R(40) + R(42));
        if (R(40) <= 0)
        {
            W(6, 0);
        }
        return next.ToImmutableArray();

        short R(int offset) => BinaryPrimitives.ReadInt16LittleEndian(next.AsSpan(offset));
        void W(int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(next.AsSpan(offset), unchecked((short)value));
    }

    public static NativeBattleGfGeometryBatch RayPackets(ImmutableArray<byte> ray, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project)
    {
        if (ray.Length != 48)
        {
            throw new InvalidDataException("Native Berserk requires its complete ray record.");
        }
        int tilt = R(2);
        int spin = R(4);
        int sine = NativeBattleCameraMath.Sine(tilt);
        NativeBattleVector direction = new(unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(spin) * sine) >> 12)), unchecked((short)(unchecked(NativeBattleCameraMath.Sine(spin) * sine) >> 12)), unchecked((short)-NativeBattleCameraMath.Cosine(tilt)));
        NativeBattleModuleProjectedVertex a = project(new(0, 0, 0));
        SkinWrites partial = new(28, 0);
        partial.Word(8, a.Screen);
        if (a.Depth == 0)
        {
            return new([partial.Attempt(null)], 0);
        }
        int length = R(34) >> 3;
        NativeBattleModuleProjectedVertex b = project(new(unchecked((short)(direction.X * length >> 12)), unchecked((short)(direction.Y * length >> 12)), unchecked((short)(direction.Z * length >> 12))));
        partial.Word(16, b.Screen);
        int dx = unchecked((short)b.Screen) - unchecked((short)a.Screen);
        int dy = unchecked((short)(a.Screen >> 16)) - unchecked((short)(b.Screen >> 16));
        int magnitude = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy));
        if (b.Depth == 0 || magnitude == 0)
        {
            return new([partial.Attempt(null)], 0);
        }
        int width = R(40) >> 3;
        short px = unchecked((short)((unchecked(unchecked((dy << 12) / magnitude) * width) / b.Depth) >> 4));
        short py = unchecked((short)((unchecked(unchecked((dx << 12) / magnitude) * width) / b.Depth) >> 4));
        int ax = unchecked((short)a.Screen);
        int ay = unchecked((short)(a.Screen >> 16));
        int bx = unchecked((short)b.Screen);
        int by = unchecked((short)(b.Screen >> 16));
        uint tipA = XY(unchecked((short)(bx + px)), unchecked((short)(by + py)));
        uint tipB = XY(unchecked((short)(bx - px)), unchecked((short)(by - py)));
        uint halfA = XY((unchecked((short)tipA) + ax) >> 1, (unchecked((short)(tipA >> 16)) + ay) >> 1);
        uint halfB = XY((unchecked((short)tipB) + ax) >> 1, (unchecked((short)(tipB >> 16)) + ay) >> 1);
        uint color0 = U(8);
        uint color1 = U(16);
        uint color2 = U(24);
        int bucket = 17 + (((a.Depth + b.Depth) / 2) >> 2);
        ImmutableArray<uint> triangle = [0x06000000, 0x32000000 | (color0 & 0xFFFFFF), a.Screen, color1, halfA, color1, halfB];
        ImmutableArray<uint> quad = [0x08000000, 0x3A000000 | (color1 & 0xFFFFFF), halfA, color1, halfB, color2, tipA, color2, tipB];
        return new([Packet(triangle, 0, bucket), Packet(quad, 28, bucket)], 64);

        short R(int offset) => BinaryPrimitives.ReadInt16LittleEndian(ray.AsSpan().Slice(offset));
        uint U(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ray.AsSpan().Slice(offset));
    }

    public static NativeBattleBerserk108RayStep Rays(ImmutableArray<byte> source, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project)
    {
        return Rays(source.AsSpan(), project);
    }

    internal static NativeBattleBerserk108RayStep Rays(ReadOnlySpan<byte> source, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project)
    {
        if (source.Length != 32 * 48)
        {
            throw new InvalidDataException("Native Berserk requires all32 retained ray slots.");
        }
        //Keep this bounded draw's updates private until every projection and packet succeeds.
        byte[] next = source.ToArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int alive = 0;
        for (int index = 0; index < 32; index++)
        {
            int offset = index * 48;
            if (BinaryPrimitives.ReadInt16LittleEndian(next.AsSpan(offset + 6)) == 0)
            {
                continue;
            }
            alive++;
            ImmutableArray<byte> ray = next.AsSpan(offset, 48).ToArray().ToImmutableArray();
            NativeBattleGfGeometryBatch batch = RayPackets(ray, project);
            attempts.AddRange(batch.Attempts.Select(attempt => attempt with { ByteOffsetRelative = attempt.ByteOffsetRelative + allocated }));
            allocated += batch.AllocationBytes;
            UpdateRay(ray).AsSpan().CopyTo(next.AsSpan(offset));
        }
        return new(System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(next), alive, new(attempts.ToImmutable(), allocated));
    }

    internal static NativeBattleGfGeometryAttempt Packet(ImmutableArray<uint> words, int offset, int bucket)
    {
        SkinWrites writes = new(words.Length * 4, offset);
        for (int index = 0; index < words.Length; index++)
        {
            writes.Word(index * 4, words[index]);
        }
        return writes.Attempt(new(bucket, words, [0, 0, 0, 0]));
    }

    internal static uint XY(int x, int y) => (uint)(ushort)x | ((uint)(ushort)y << 16);
}
