using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>880450's flags-zero call to 8DD260/8DD3A0, with its current GTE matrix and original frame directory.</summary>
public static class NativeBattleCuragaProjectedSprites
{
    public static NativeBattleModulePacketBatch Emit8DD260(ImmutableArray<byte> resource, ushort phase, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, byte depthShift, short depthBias = 0)
    {
        NativeBattleGfGeometryBatch batch = EmitBatch8DD260(resource, phase, matrix, projection, cameraRoll, tableBaseBucket, depthShift, depthBias);
        return new(batch.Attempts.Where(attempt => attempt.Primitive is not null).Select(attempt => new NativeBattleAllocatedPrimitive(attempt.Primitive!, attempt.ByteOffsetRelative, 0)).ToImmutableArray(), batch.AllocationBytes);
    }

    public static NativeBattleGfGeometryBatch EmitBatch8DD260(ImmutableArray<byte> resource, ushort phase, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, short cameraRoll, int tableBaseBucket, byte depthShift, short depthBias = 0)
    {
        if (resource.IsDefault || depthShift > 31)
        {
            throw new InvalidDataException("Native8DD260 requires its original resource and actual depth shift.");
        }
        matrix.Validate();
        // 45E500 copies the current matrix. 56D090/56C270 replace only its rotation with Rz(-roll) * R.
        int angle = -(int)cameraRoll;
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angle));
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angle));
        NativeBattleActionMatrix roll = new([cosine, unchecked((short)-sine), 0, sine, cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix main = NativeBattleGfMatrices.Multiply56C090(roll, matrix) with { X = matrix.X, Y = matrix.Y, Z = matrix.Z };
        int frame = BinaryPrimitives.ReadUInt16LittleEndian(Read(resource, checked(10 + phase * 2), 2));
        _ = Read(resource, checked(12 + phase * 2), 2); // Context WORD28 reads the next directory entry even though this leaf does not use it.
        uint descriptor = BinaryPrimitives.ReadUInt32LittleEndian(Read(resource, frame, 4));
        int count = checked((int)(descriptor & 0x7FFFFFFF));
        bool extended = (descriptor & 0x80000000) != 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int? retainedBucket = null;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> quad = Read(resource, checked(frame + 4 + index * 20), 20);
            NativeBattleActionMatrix current = NativeBattlePoseMatrices.ModuleSpriteQuadCamera(main, quad);
            short width = (short)(8 * quad[0]);
            short height = (short)(8 * quad[2]);
            NativeBattleModuleProjectedVertex a = NativeBattleModuleProjection.Project45FE10(current, new(unchecked((short)-width), unchecked((short)-height), 0), projection);
            NativeBattleModuleProjectedVertex b = NativeBattleModuleProjection.Project45FE10(current, new(width, unchecked((short)-height), 0), projection);
            NativeBattleModuleProjectedVertex c = NativeBattleModuleProjection.Project45FE10(current, new(unchecked((short)-width), height, 0), projection);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
            int selector = flags >> 12;
            if (selector > 3)
            {
                throw new NotSupportedException("Native8DD3A0 requires a retained context BYTE outside its four resource palette registers.");
            }
            int palette = unchecked((sbyte)Read(resource, 4 + selector, 1)[0]);
            SkinWrites packet = new(40, allocated);
            packet.Word(0, 0x09000000);
            packet.Word(4, (uint)quad[3] << 24 | (uint)quad[1] * 0x010101);
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(quad[4..]));
            packet.Byte(20, unchecked((byte)Math.Min(quad[0] + quad[4], 255)));
            packet.Byte(21, quad[5]);
            packet.Byte(28, quad[4]);
            packet.Byte(36, unchecked((byte)Math.Min(quad[0] + quad[4], 255)));
            packet.Word16(14, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad[6..]) + (palette << 6))));
            if (((a.Flags | b.Flags | c.Flags) & 0x60000) != 0)
            {
                attempts.Add(packet.Attempt(null));
                continue;
            }
            packet.Word16(22, (ushort)(flags & 0x1FF));
            packet.Word(8, a.Screen);
            packet.Word(16, b.Screen);
            packet.Word(24, c.Screen);
            NativeBattleModuleProjectedVertex d = NativeBattleModuleProjection.Project45FE10(current, new(width, height, 0), projection);
            byte bottom = unchecked((byte)Math.Min(quad[2] + quad[5], 255));
            packet.Byte(29, bottom);
            packet.Byte(37, bottom);
            packet.Word(32, d.Screen);
            int bucket = retainedBucket ?? checked(tableBaseBucket + (Math.Max(0, ((a.Depth + b.Depth + c.Depth + d.Depth) >> 4) + depthBias) >> depthShift));
            uint opcode = quad[3] & 0xFCu;
            ImmutableArray<int> payload = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? [a.Depth, b.Depth, c.Depth, d.Depth] : [b.Depth, c.Depth, d.Depth, 0];
            NativeBattleGeometryPrimitive primitive = new(bucket, packet.Words(), payload);
            bool firstExtended = retainedBucket is null && extended;
            if (firstExtended)
            {
                attempts.Add(packet.Attempt(null));
                SkinWrites mode = new(12, checked(allocated + 40));
                mode.Word(0, 0x01000000);
                mode.Word(4, 0xE1000220);
                attempts.Add(mode.Attempt(new(bucket, [0x01000000, 0xE1000220], [0, 0, 0, 0])));
                attempts.Add(new SkinWrites(40, allocated).Attempt(primitive));
            }
            else
            {
                attempts.Add(packet.Attempt(primitive));
            }
            allocated = checked(allocated + (firstExtended ? 52 : 40));
            retainedBucket = bucket;
        }
        return new(attempts.ToImmutable(), allocated);
    }

    private static ReadOnlySpan<byte> Read(ImmutableArray<byte> source, int offset, int length)
    {
        if (offset < 0 || offset > source.Length - length)
        {
            throw new InvalidDataException("Native8DD260 frame or palette access exceeds its actual resource bank.");
        }
        return source.AsSpan().Slice(offset, length);
    }
}
