using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame InitializeShards6FA3E0(NativeBattleRinoa89Frame frame)
    {
        const uint mesh = 0x1496550;
        NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 36);
        NativeBattleFileStaging staging = temporary.Staging;
        uint cursor = checked(mesh + BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(mesh, 4)) + 8);
        int count = BinaryPrimitives.ReadInt32LittleEndian(staging.Read(cursor, 4));
        if (count != 120)
        {
            throw new InvalidDataException("Native6FA3E0 requires its actual120-triangle original shard mesh.");
        }
        cursor += 4;
        staging = Dword(staging, temporary.Address, cursor);
        staging = staging.Write(temporary.Address + 28, BitConverter.GetBytes((short)160)).Write(temporary.Address + 30, BitConverter.GetBytes((short)108)).Write(temporary.Address + 32, new byte[2]).MarkUnknown(temporary.Address + 34, 2);
        byte[] lookup = staging.Read(0x14AF4E0, 512);
        ImmutableArray<ushort> squareRoots = Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(lookup.AsSpan(index * 2))).ToImmutableArray();
        NativeBattleEffectArenaWriter writer = new(RequireArena(frame));
        for (int triangle = 0; triangle < count; triangle++)
        {
            NativeBattleVector[] points = new NativeBattleVector[3];
            byte[] face = staging.Read(cursor + 4, 6);
            for (int corner = 0; corner < 3; corner++)
            {
                ushort reference = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(corner * 2));
                byte[] raw = staging.Read(checked(mesh + 8u + (uint)reference * 4), 8);
                points[corner] = Vector(raw);
                staging = staging.Write(temporary.Address + 4u + (uint)corner * 8, raw);
            }
            short centerX = unchecked((short)(points.Sum(point => point.X) / 3));
            short centerY = unchecked((short)(points.Sum(point => point.Y) / 3));
            short centerZ = unchecked((short)(points.Sum(point => point.Z) / 3));
            if (points[2].X < Math.Min(points[0].X, points[1].X))
            {
                points = [points[2], points[0], points[1]];
            }
            else if (points[1].X < points[0].X)
            {
                points = [points[1], points[2], points[0]];
            }
            for (int corner = 0; corner < 3; corner++)
            {
                staging = staging.Write(temporary.Address + 4u + (uint)corner * 8, Bytes(points[corner]));
            }
            int offset = 24228 + triangle * 52;
            writer.Write(offset, Bytes(new(centerX, centerY, centerZ)));
            for (int corner = 0; corner < 3; corner++)
            {
                NativeBattleVector relative = new(unchecked((short)(points[corner].X - centerX)), unchecked((short)(points[corner].Y - centerY)), unchecked((short)(points[corner].Z - centerZ)));
                writer.Write(offset + 20 + corner * 6, Bytes(relative));
            }
            int pageOrigin = points[0].X & ~63;
            byte[] uv = new byte[10];
            uv[0] = unchecked((byte)(points[0].X - pageOrigin));
            uv[1] = unchecked((byte)points[0].Y);
            uv[4] = unchecked((byte)(points[1].X - pageOrigin));
            uv[5] = unchecked((byte)points[1].Y);
            BinaryPrimitives.WriteUInt16LittleEndian(uv.AsSpan(6), unchecked((ushort)((((pageOrigin + 384) >> 6) & 15) | 0x130)));
            uv[8] = unchecked((byte)(points[2].X - pageOrigin));
            uv[9] = unchecked((byte)points[2].Y);
            writer.Write(offset + 40, uv);
            NativeBattleVector delta = new(unchecked((short)(centerX - 160)), unchecked((short)(centerY - 108)), centerZ);
            staging = staging.Write(temporary.Address + 4, Bytes(delta));
            int length = NativeBattleQuezacotlRibbonMath.SqrtQ12(NativeBattleQuezacotlRibbonMath.SquaredLength(delta.X, delta.Y, delta.Z), squareRoots) >> 12;
            if (length == 0)
            {
                throw new InvalidDataException("Native6FA3E0 reaches its original zero-length integer division.");
            }
            writer.Write(offset + 14, BitConverter.GetBytes(unchecked((short)length)));
            writer.Write(offset + 8, Bytes(new(unchecked((short)(4 * delta.X / length)), unchecked((short)(4 * delta.Y / length)), unchecked((short)(4 * delta.Z / length)))));
            cursor += 20;
        }
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 36);
        NativeCrtRandomState random = frame.Random;
        for (int triangle = 0; triangle < count; triangle++)
        {
            int offset = 24228 + triangle * 52;
            writer.Write(offset + 6, BitConverter.GetBytes((ushort)16));
            random = random.Draw(out ushort draw);
            writer.Write(offset + 16, BitConverter.GetBytes(checked(ArenaAddress + 30468u + 20u * (uint)(draw & 63))));
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Staging = staging, Random = random };
    }

    public static NativeBattleRinoa89Frame AdvanceShards6FD100(NativeBattleRinoa89Frame frame, int distance)
    {
        NativeBattleEffectArena arena = RequireArena(frame);
        NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < 120; index++)
        {
            int offset = 24228 + index * 52;
            byte[] shard = arena.Read(offset, 38);
            if (BinaryPrimitives.ReadUInt16LittleEndian(shard.AsSpan(6)) == 0 || BinaryPrimitives.ReadInt16LittleEndian(shard.AsSpan(14)) >= distance)
            {
                continue;
            }
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(shard.AsSpan(16));
            int rotationOffset = NativeBattleWaterExecutionFields.ArenaOffset(address, 18);
            byte[] raw = arena.Read(rotationOffset, 18);
            NativeBattleActionMatrix rotation = new(Enumerable.Range(0, 9).Select(element => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(element * 2))).ToImmutableArray(), 0, 0, 0, 4096);
            NativeBattleVector first = NativeBattleGfMatrices.Transform460840(rotation, Vector(shard.AsSpan(20)));
            writer.Write(offset + 20, Bytes(first));
            if (first is { X: 0, Y: 0, Z: 0 })
            {
                writer.Write(offset + 6, new byte[2]);
                continue;
            }
            for (int corner = 1; corner < 3; corner++)
            {
                NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(rotation, Vector(shard.AsSpan(20 + corner * 6)));
                writer.Write(offset + 20 + corner * 6, Bytes(transformed));
            }
            NativeBattleVector center = Vector(shard);
            NativeBattleVector velocity = Vector(shard.AsSpan(8));
            writer.Write(offset, Bytes(new(unchecked((short)(center.X + velocity.X)), unchecked((short)(center.Y + velocity.Y)), unchecked((short)(center.Z + velocity.Z)))));
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() } };
    }

    private static NativeBattleVector Vector(ReadOnlySpan<byte> data)
    {
        return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]), BinaryPrimitives.ReadInt16LittleEndian(data[4..]));
    }

    private static byte[] Bytes(NativeBattleVector vector)
    {
        byte[] data = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(data, vector.X);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2), vector.Y);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(4), vector.Z);
        return data;
    }
}
