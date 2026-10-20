using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89ParticleDraw(NativeBattleRinoa89Frame Frame, NativeBattleGfGeometryBatch Packets);

public static partial class NativeBattleRinoa89ParticleDrawing
{
    public static NativeBattleRinoa89ParticleDraw DrawSparks6FC8B0(NativeBattleRinoa89Frame frame, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, Func<uint, int, byte[]> readResource)
    {
        NativeBattleEffectArena arena = ParticleArena(frame);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 180);
        NativeBattleFileStaging staging = allocation.Staging;
        uint temporary = allocation.Address;
        foreach (uint displacement in new uint[] { 72, 80, 88, 96 })
        {
            staging = Word(staging, temporary + displacement, 0);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < 512; index++)
        {
            int offset = 4672 + index * 24;
            short life = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 10, 2));
            if (life == 0)
            {
                continue;
            }
            byte[] task = arena.Read(offset, 24);
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(task);
            short scale = BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(18));
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(6)), BinaryPrimitives.ReadInt16LittleEndian(task.AsSpan(8)));
            (int x, int y, int z) = Transform(view, point.X, point.Y, point.Z);
            staging = Matrix(staging, temporary + 132, new([scale, 0, 0, 0, scale, 0, 0, 0, scale], x, y, z, 4096), true);
            staging = Dword(staging, temporary, source);
            short frames = BinaryPrimitives.ReadInt16LittleEndian(readResource(source + 8, 2));
            if (frames == 0)
            {
                throw new InvalidDataException("Native6FC8B0 sprite has zero animation frames.");
            }
            int directory = frames - (life - 1) % frames;
            short displacement = BinaryPrimitives.ReadInt16LittleEndian(readResource(checked(source + (uint)(8 + directory * 2)), 2));
            uint selected = unchecked(source + (uint)displacement);
            int count = BinaryPrimitives.ReadInt32LittleEndian(readResource(selected, 4));
            staging = Dword(Dword(staging, temporary + 44, selected), temporary + 48, unchecked((uint)count));
            if (count < 0)
            {
                byte flags = staging.Read(temporary + 37, 1)[0];
                staging = staging.Write(temporary + 37, [(byte)(flags | 1)]);
                count &= int.MaxValue;
                staging = Dword(staging, temporary + 48, checked((uint)count));
            }
            staging = Dword(staging, temporary + 44, selected + 4);
            NativeBattleRinoa89ParticleDraw draw = DrawQuads6FC9E0(frame with { Staging = staging }, temporary, projection, readResource);
            staging = draw.Frame.Staging;
            foreach (NativeBattleGfGeometryAttempt attempt in draw.Packets.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(allocated + attempt.ByteOffsetRelative) });
            }
            allocated = checked(allocated + draw.Packets.AllocationBytes);
        }
        return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 180) }, new(attempts.ToImmutable(), allocated));
    }

    public static NativeBattleRinoa89ParticleDraw DrawQuads6FC9E0(NativeBattleRinoa89Frame frame, uint temporary, NativeBattleCameraProjection projection, Func<uint, int, byte[]> readResource)
    {
        NativeBattleFileStaging staging = frame.Staging;
        int count = unchecked((int)ReadDword(staging, temporary + 48));
        uint source = ReadDword(staging, temporary + 44);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            byte[] quad = readResource(checked(source + (uint)index * 20), 20);
            int width = quad[0] * 8;
            int height = quad[2] * 8;
            short sx = BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(16));
            short sy = BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(18));
            NativeBattleActionMatrix main = ReadMatrix(staging, temporary + 132);
            NativeBattleActionMatrix rotation = NativeBattlePoseMatrices.MultiplyGteRotation45E470(main, new([sx, 0, 0, 0, sy, 0, 0, 0, 4096], 0, 0, 0, 4096));
            int dx = unchecked((short)(width + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(8))));
            int dy = unchecked((short)(height + 16 * BinaryPrimitives.ReadInt16LittleEndian(quad.AsSpan(10))));
            (int x, int y, int z) = Transform(main, dx, dy, 0);
            NativeBattleActionMatrix matrix = rotation with { X = x, Y = y, Z = z };
            staging = Matrix(staging, temporary + 100, matrix, false);
            NativeBattleVector[] vertices = [new((short)-width, (short)-height, 0), new((short)width, (short)-height, 0), new((short)-width, (short)height, 0), new((short)width, (short)height, 0)];
            for (int corner = 0; corner < 4; corner++)
            {
                staging = Word(Word(staging, temporary + 68 + (uint)corner * 8, unchecked((ushort)vertices[corner].X)), temporary + 70 + (uint)corner * 8, unchecked((ushort)vertices[corner].Y));
            }
            NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(matrix, vertices[0], projection);
            NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(matrix, vertices[1], projection);
            NativeBattleModuleProjectedVertex c = NativeBattleModuleProjection.Project45FE10(matrix, vertices[2], projection);
            uint flags = a.Flags | b.Flags | c.Flags;
            staging = Dword(staging, temporary + 64, flags);
            byte[] packet = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            Store32(0, 0x9000000);
            Store32(4, (uint)(quad[1] | quad[1] << 8 | quad[1] << 16 | quad[3] << 24));
            Store32(12, BinaryPrimitives.ReadUInt32LittleEndian(quad.AsSpan(4)));
            Store8(28, quad[4]);
            Store8(21, quad[5]);
            byte u1 = (byte)Math.Min(255, quad[0] + quad[4]);
            Store8(20, u1);
            Store8(36, u1);
            if ((flags & 0x60000) != 0)
            {
                attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), null));
                continue;
            }
            Store16(22, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad.AsSpan(14)) & 0x1FF));
            Store32(8, a.Screen);
            Store32(16, b.Screen);
            Store32(24, c.Screen);
            NativeBattleModuleProjectedVertex d = NativeBattleModuleProjection.Project45FAA0(matrix, vertices[3], projection, 0, 0);
            byte v1 = (byte)Math.Min(255, quad[2] + quad[5]);
            Store8(29, v1);
            Store8(37, v1);
            Store32(32, d.Screen);
            if ((staging.Read(temporary + 37, 1)[0] & 1) != 0)
            {
                byte[] environment = new byte[8];
                BinaryPrimitives.WriteUInt32LittleEndian(environment, 0x1000000);
                BinaryPrimitives.WriteUInt32LittleEndian(environment.AsSpan(4), 0xE1000220);
                attempts.Add(new(allocated + 40, environment.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), new(19, [0x1000000, 0xE1000220], [0, 0, 0, 0])));
                attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), Primitive()));
                allocated += 48;
            }
            else
            {
                attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), Primitive()));
                allocated += 40;
            }

            NativeBattleGeometryPrimitive Primitive() => new(19, Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(word * 4))).ToImmutableArray(), [a.Depth, b.Depth, c.Depth, d.Depth]);
            void Store8(int offset, byte value)
            {
                packet[offset] = value;
                mask[offset] = NativeBattleGfPacketByteState.Known;
            }
            void Store16(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }
            void Store32(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }
        }
        return new(frame with { Staging = staging }, new(attempts.ToImmutable(), allocated));
    }

    private static NativeBattleEffectArena ParticleArena(NativeBattleRinoa89Frame frame)
    {
        if (ReadDword(frame.Staging, 0x2556600) != 0x20DFAB8)
        {
            throw new InvalidDataException("Native Wishing Star particle base differs from its actual arena owner.");
        }
        return frame.Memory.Arena ?? throw new NotSupportedException("Native Wishing Star requires its actual particle arena.");
    }
    private static (int X, int Y, int Z) Transform(NativeBattleActionMatrix matrix, int x, int y, int z)
    {
        int Row(int row) => NativeBattlePoseMatrices.RoundNative((float)(matrix.Rotation[row * 3 + 2] / 4096d) * (double)z + (float)(matrix.Rotation[row * 3 + 1] / 4096d) * (double)y + (float)(matrix.Rotation[row * 3] / 4096d) * (double)x);
        return (unchecked(Row(0) + matrix.X), unchecked(Row(1) + matrix.Y), unchecked(Row(2) + matrix.Z));
    }

    private static NativeBattleActionMatrix ReadMatrix(NativeBattleFileStaging staging, uint address)
    {
        short[] rotation = new short[9];
        for (int index = 0; index < 9; index++)
        {
            rotation[index] = BinaryPrimitives.ReadInt16LittleEndian(staging.Read(address + (uint)index * 2, 2));
        }
        return new(rotation.ToImmutableArray(), unchecked((int)ReadDword(staging, address + 20)), unchecked((int)ReadDword(staging, address + 24)), unchecked((int)ReadDword(staging, address + 28)), 4096);
    }

    private static NativeBattleFileStaging Matrix(NativeBattleFileStaging staging, uint address, NativeBattleActionMatrix matrix, bool clearPadding)
    {
        for (int index = 0; index < 9; index++)
        {
            staging = Word(staging, address + (uint)index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        if (clearPadding)
        {
            staging = Word(staging, address + 18, 0);
        }
        return Dword(Dword(Dword(staging, address + 20, unchecked((uint)matrix.X)), address + 24, unchecked((uint)matrix.Y)), address + 28, unchecked((uint)matrix.Z));
    }

    private static uint ReadDword(NativeBattleFileStaging staging, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(address, 4));
    private static NativeBattleFileStaging Word(NativeBattleFileStaging staging, uint address, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return staging.Write(address, bytes);
    }
}



