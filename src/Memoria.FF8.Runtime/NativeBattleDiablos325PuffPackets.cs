using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325PuffPackets
{
    public static NativeBattleGfGeometryBatch Emit6FC9E0(ImmutableArray<byte> resource, byte life, NativeBattleActionMatrix basis, NativeBattleCameraProjection projection, int bucket)
    {
        if (life == 0 || life > 127)
        {
            throw new InvalidDataException("Native puff drawing requires its active signed life byte.");
        }
        int phases = BinaryPrimitives.ReadInt16LittleEndian(Read(resource, 8, 2));
        if (phases <= 0)
        {
            throw new InvalidDataException("Native puff drawing requires its positive frame count.");
        }
        int phase = phases - ((life - 1) % phases);
        int frame = BinaryPrimitives.ReadInt16LittleEndian(Read(resource, checked(8 + phase * 2), 2));
        uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(Read(resource, frame, 4));
        if ((descriptor & 0x80000000) == 0)
        {
            throw new NotSupportedException("Native puff drawing requires its original extended frame or the actual retained scratch flag.");
        }
        int count = checked((int)(descriptor & 0x7FFFFFFF));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> quad = Read(resource, checked(frame + 4 + index * 20), 20);
            short scaleX = BinaryPrimitives.ReadInt16LittleEndian(quad[16..]);
            short scaleY = BinaryPrimitives.ReadInt16LittleEndian(quad[18..]);
            NativeBattleActionMatrix local = new([scaleX, 0, 0, 0, scaleY, 0, 0, 0, 4096], 0, 0, 0, 4096);
            NativeBattleActionMatrix current = NativeBattlePoseMatrices.MultiplyGteRotation45E470(basis, local);
            int dx = (BinaryPrimitives.ReadInt16LittleEndian(quad[8..]) << 4) + (quad[0] << 3);
            int dy = (BinaryPrimitives.ReadInt16LittleEndian(quad[10..]) << 4) + (quad[2] << 3);
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(basis, dx, dy, 0);
            current = current with { X = unchecked(basis.X + x), Y = unchecked(basis.Y + y), Z = unchecked(basis.Z + z) };
            short width = (short)(quad[0] << 3);
            short height = (short)(quad[2] << 3);
            NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(current, new(unchecked((short)-width), unchecked((short)-height), 0), projection);
            NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(current, new(width, unchecked((short)-height), 0), projection);
            NativeBattleModuleProjectedVertex c = NativeBattleModuleProjection.Project45FE10(current, new(unchecked((short)-width), height, 0), projection);
            SkinWrites packet = new(40, allocated);
            packet.Word(0, 0x09000000);
            packet.Word(4, (uint)quad[3] << 24 | (uint)quad[1] * 0x010101);
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(quad[4..]));
            packet.Byte(28, quad[4]);
            byte right = (byte)Math.Min(quad[0] + quad[4], 255);
            packet.Byte(20, right);
            packet.Byte(21, quad[5]);
            packet.Byte(36, right);
            if (((a.Flags | b.Flags | c.Flags) & 0x60000) != 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            packet.Word16(22, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]) & 0x1FF));
            packet.Word(8, a.Screen);
            packet.Word(16, b.Screen);
            packet.Word(24, c.Screen);
            NativeBattleModuleProjectedVertex d = NativeBattleModuleProjection.Project45FE10(current, new(width, height, 0), projection);
            byte bottom = (byte)Math.Min(quad[2] + quad[5], 255);
            packet.Byte(29, bottom);
            packet.Byte(37, bottom);
            packet.Word(32, d.Screen);
            uint opcode = quad[3] & 0xFCu;
            ImmutableArray<int> payload = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? [a.Depth, b.Depth, c.Depth, d.Depth] : [b.Depth, c.Depth, d.Depth, 0];
            NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload);
            attempts.Add(packet.Attempt(null));
            SkinWrites mode = new(8, checked(allocated + 40));
            mode.Word(0, 0x01000000);
            mode.Word(4, 0xE1000220);
            attempts.Add(mode.Attempt(new(bucket, [0x01000000, 0xE1000220], [0, 0, 0, 0])));
            attempts.Add(new SkinWrites(40, allocated).Attempt(primitive));
            allocated = checked(allocated + 48);
        }
        return new(attempts.ToImmutable(), allocated);
    }

    public static NativeBattleActionMatrix Basis658970(NativeBattleDiablos325Frame frame, int index, NativeBattleActionMatrix view, out byte life)
    {
        if ((uint)index >= NativeBattleDiablos325Puffs.Capacity)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native puff drawing requires its actual arena.");
        int offset = NativeBattleDiablos325Puffs.BaseOffset + index * 16;
        life = arena.Read(offset + 15, 1)[0];
        if (life == 0)
        {
            throw new InvalidDataException("Native puff basis requires an active record.");
        }
        byte[] position = arena.Read(offset + 4, 8);
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(view, new(BinaryPrimitives.ReadInt16LittleEndian(position), BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(position.AsSpan(4))));
        byte angle = position[6];
        byte shade = position[7];
        short diagonal = (short)(shade << 5);
        if (angle == 0)
        {
            return new([diagonal, 0, 0, 0, diagonal, 0, 0, 0, diagonal], translation.X, translation.Y, translation.Z, 4096);
        }
        byte[] table = arena.Read(0x994 + angle * 4, 4);
        short sine = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(table) * shade >> 7));
        short cosine = unchecked((short)(BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(2)) * shade >> 7));
        return new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, diagonal], translation.X, translation.Y, translation.Z, 4096);
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (source.IsDefault || offset < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native puff sprite access leaves its original resource.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}
