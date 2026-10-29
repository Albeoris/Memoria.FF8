using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The recipient shard leaf's raw quad stores and reverse-count ordering buckets.</summary>
public static class NativeBattleCarbuncle278ShardPackets
{
    public static NativeBattleGfGeometryBatch Emit683490(ImmutableArray<byte> resource, ushort phase, NativeBattleActionMatrix main, NativeBattleCameraProjection projection, int tableBaseBucket, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int frame = BinaryPrimitives.ReadUInt16LittleEndian(Read(checked(10 + phase * 2), 2));
        int count = BinaryPrimitives.ReadInt32LittleEndian(Read(frame, 4));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> quad = Read(checked(frame + 4 + index * 20), 20);
            short sx = BinaryPrimitives.ReadInt16LittleEndian(quad[16..]);
            short sy = BinaryPrimitives.ReadInt16LittleEndian(quad[18..]);
            NativeBattleActionMatrix local = new([sx, 0, 0, 0, sy, 0, 0, 0, 4096], 0, 0, 0, 4096);
            NativeBattleActionMatrix current = NativeBattlePoseMatrices.MultiplyGteRotation45E470(main, local);
            short width = unchecked((short)(quad[0] << 3));
            short height = unchecked((short)(quad[2] << 3));
            NativeBattleVector offset = new(unchecked((short)(width + (BinaryPrimitives.ReadInt16LittleEndian(quad[8..]) << 4))), unchecked((short)(height + (BinaryPrimitives.ReadInt16LittleEndian(quad[10..]) << 4))), 0);
            NativeBattleFullMacVertex position = NativeBattleGfMatrices.TransformMac460840(main, offset);
            current = current with { X = position.X, Y = position.Y, Z = position.Z };
            NativeBattleModuleProjectedVertex a = Project(unchecked((short)-width), unchecked((short)-height));
            NativeBattleModuleProjectedVertex b = Project(width, unchecked((short)-height));
            NativeBattleModuleProjectedVertex c = Project(unchecked((short)-width), height);
            SkinWrites packet = new(40, allocated);
            packet.Word(0, 0x09000000);
            packet.Word(4, BinaryPrimitives.ReadUInt32LittleEndian(quad));
            packet.Word(12, BinaryPrimitives.ReadUInt32LittleEndian(quad[4..]));
            byte right = (byte)Math.Min(quad[0] + quad[4], 255);
            packet.Byte(20, right);
            packet.Byte(21, quad[5]);
            packet.Byte(28, quad[4]);
            packet.Byte(36, right);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(quad[14..]);
            int selector = flags >> 12;
            if (selector > 3)
            {
                throw new NotSupportedException("Native Carbuncle shard requires the actual borrowed context byte beyond its four published palette lanes.");
            }
            int palette = unchecked((sbyte)Read(checked(4 + selector), 1)[0]);
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
            NativeBattleModuleProjectedVertex d = Project(width, height);
            byte bottom = (byte)Math.Min(quad[2] + quad[5], 255);
            packet.Byte(29, bottom);
            packet.Byte(37, bottom);
            packet.Word(32, d.Screen);
            uint opcode = quad[3] & 0xFCu;
            ImmutableArray<int> depths = opcode is < 0x20 or > 0x3C ? [0, 0, 0, 0] : (opcode & 8) != 0 ? [a.Depth, b.Depth, c.Depth, d.Depth] : [b.Depth, c.Depth, d.Depth, 0];
            NativeBattleGeometryPrimitive primitive = new(checked(tableBaseBucket + count - index), packet.Words(), depths);
            if (pilotIdentity is { } identity)
            {
                System.Numerics.Vector3 World(int x, int y) => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(current, new(unchecked((short)x), unchecked((short)y), 0)), projection);
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + index) }, World(-width, -height), World(width, -height), World(-width, height), World(width, height), 4) };
            }
            attempts.Add(packet.Attempt(primitive));
            allocated = checked(allocated + 40);

            NativeBattleModuleProjectedVertex Project(short x, short y) => NativeBattleModuleProjection.Project45FE10(current, new(x, y, 0), projection);
        }
        return new(attempts.ToImmutable(), allocated);

        ReadOnlySpan<byte> Read(int offset, int length)
        {
            if (resource.IsDefault || offset < 0 || offset > resource.Length - length)
            {
                throw new InvalidDataException("Native Carbuncle shard access leaves its actual resource bytes.");
            }
            return resource.AsSpan().Slice(offset, length);
        }
    }
}
