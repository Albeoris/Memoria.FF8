using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleControl18Shards
{
    public static NativeBattleGfGeometryBatch Emit(ReadOnlySpan<byte> records, Func<uint, int, ImmutableArray<byte>> readPolygon, ReadOnlySpan<uint> shadeTable, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project, int orderingBaseBucket, Func<NativeBattleVector, System.Numerics.Vector3>? pilotWorld = null, Func<int, NativeBattlePilotVisualIdentity>? pilotIdentity = null)
    {
        if (records.Length % 68 != 0)
        {
            throw new InvalidDataException("Native shards require complete68-byte records.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        for (int offset = 0; offset < records.Length; offset += 68)
        {
            ReadOnlySpan<byte> record = records.Slice(offset, 68);
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(record);
            if (pointer == 0)
            {
                continue;
            }
            bool quad = record[0x26] != 0;
            int corners = quad ? 4 : 3;
            int length = quad ? 40 : 32;
            byte[] packet = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            int centerX = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(4, 4)) >> 8;
            int centerY = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(8, 4)) >> 8;
            int centerZ = BinaryPrimitives.ReadInt32LittleEndian(record.Slice(12, 4)) >> 8;
            System.Numerics.Vector3[]? world = pilotWorld is not null && pilotIdentity is not null ? new System.Numerics.Vector3[corners] : null;
            int sum = 0;
            int[] payload = new int[4];
            for (int corner = 0; corner < corners; corner++)
            {
                int position = 16 + corner * 8;
                NativeBattleVector point = new(unchecked((short)(centerX + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(position, 2)))), unchecked((short)(centerY + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(position + 2, 2)))), unchecked((short)(centerZ + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(position + 4, 2)))));
                NativeBattleModuleProjectedVertex projected = project(point);
                if (world is not null)
                {
                    world[corner] = pilotWorld!(point);
                }
                StoreDword(8 + corner * 8, projected.Screen);
                sum += projected.Depth >> 2;
                payload[corner] = unchecked((int)(projected.Flags << 2));
            }
            int shade = unchecked((sbyte)record[0x2E]);
            if ((uint)shade >= shadeTable.Length)
            {
                throw new NotSupportedException("Native shard shade index leaves its supplied original palette.");
            }
            packet[3] = quad ? (byte)9 : (byte)7;
            mask[3] = NativeBattleGfPacketByteState.Known;
            uint color = shadeTable[shade] | (quad ? 0x2E000000u : 0x26000000u);
            StoreDword(4, color);
            ImmutableArray<byte> polygon = readPolygon(pointer, quad ? 20 : 16);
            if (polygon.IsDefault || polygon.Length != (quad ? 20 : 16))
            {
                throw new InvalidDataException("Native shard polygon owner must supply its exact requested original bytes.");
            }
            StoreWord(12, BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(8, 2)));
            StoreWord(14, BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(10, 2)));
            StoreWord(20, BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(12, 2)));
            StoreWord(22, (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(14, 2)) | 0x20));
            StoreWord(28, BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(quad ? 16 : 6, 2)));
            if (quad)
            {
                StoreWord(36, BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(18, 2)));
            }
            int bucket = (sum / corners) >> 2;
            ImmutableArray<NativeBattleGfPacketByteState> states = mask.ToImmutableArray();
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4, 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive? primitive = bucket < 4095 ? new(checked(orderingBaseBucket + bucket), words, payload.ToImmutableArray()) { PhysicalWriteMask = states } : null;
            if (primitive is not null && world is not null)
            {
                NativeBattlePilotVisualIdentity identity = pilotIdentity!(offset / 68);
                identity = identity with { ResourceGeneration = unchecked(identity.ResourceGeneration * 397 ^ pointer) };
                primitive = primitive with { PilotGeometry = new(identity, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)corners)) };
            }
            attempts.Add(new(allocation, packet.ToImmutableArray(), states, primitive));
            allocation = checked(allocation + length);

            void StoreDword(int destination, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(destination, 4), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, destination, 4);
            }
            void StoreWord(int destination, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(destination, 2), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, destination, 2);
            }
        }
        return new(attempts.ToImmutable(), allocation);
    }
}
