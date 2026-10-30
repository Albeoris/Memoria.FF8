using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleControl18Body
{
    public static NativeBattleGfGeometryBatch Emit(ReadOnlySpan<byte> triangles, ReadOnlySpan<byte> quads, ReadOnlySpan<byte> projected, ReadOnlySpan<byte> faceFlags, uint uniformColor, Func<int, ImmutableArray<uint>, uint, uint>? light, int orderingBaseBucket, IReadOnlyList<System.Numerics.Vector3>? pilotWorld = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        int triangleCount = triangles.Length / 16;
        int quadCount = quads.Length / 20;
        if (triangles.Length % 16 != 0 || quads.Length % 20 != 0 || projected.Length % 8 != 0 || faceFlags.Length != triangleCount + quadCount)
        {
            throw new InvalidDataException("Native control body requires its complete polygon, projected and face-flag records.");
        }
        byte[] points = projected.ToArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        for (int index = 0; index < triangleCount; index++)
        {
            if (faceFlags[index] == 0)
            {
                Face(triangles.Slice(index * 16, 16), false, index);
            }
        }
        for (int index = 0; index < quadCount; index++)
        {
            if (faceFlags[triangleCount + index] == 0)
            {
                Face(quads.Slice(index * 20, 20), true, triangleCount + index);
            }
        }
        return new(attempts.ToImmutable(), allocation);

        void Face(ReadOnlySpan<byte> polygon, bool quad, int faceIndex)
        {
            int count = quad ? 4 : 3;
            int length = quad ? 40 : 32;
            byte[] packet = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            uint[] screen = new uint[count];
            int[] payload = new int[4];
            int sum = 0;
            bool clipped = false;
            for (int corner = 0; corner < count; corner++)
            {
                int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(corner * 2, 2)) & 0xFFF;
                int point = checked(index * 8);
                if (point > points.Length - 8)
                {
                    throw new InvalidDataException("Native control body polygon index leaves its actual projected vertex scratch.");
                }
                screen[corner] = BinaryPrimitives.ReadUInt32LittleEndian(points.AsSpan(point, 4));
                StoreDword(8 + corner * 8, screen[corner]);
                int depth = BinaryPrimitives.ReadUInt16LittleEndian(points.AsSpan(point + 4, 2));
                payload[corner] = depth;
                sum += depth;
                if (light is null && (BinaryPrimitives.ReadUInt16LittleEndian(points.AsSpan(point + 6, 2)) & 0x8000) != 0)
                {
                    clipped = true;
                }
            }
            if (light is null && !quad)
            {
                int minimum = screen.Min(value => unchecked((short)(value >> 16)));
                int maximum = screen.Max(value => unchecked((short)(value >> 16)));
                clipped |= minimum < 0 && maximum > 210;
            }
            if (clipped)
            {
                attempts.Add(new(allocation, packet.ToImmutableArray(), mask.ToImmutableArray(), null));
                return;
            }
            uint color = light is null ? uniformColor : light(faceIndex, screen.Take(3).ToImmutableArray(), uniformColor);
            packet[3] = quad ? (byte)9 : (byte)7;
            mask[3] = NativeBattleGfPacketByteState.Known;
            color = (color & 0xFFFFFF) | (quad ? 0x2C000000u : 0x24000000u);
            StoreDword(4, color);
            StoreWord(12, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(8, 2)));
            StoreWord(14, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(10, 2)));
            StoreWord(20, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(12, 2)));
            StoreWord(22, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(14, 2)));
            StoreWord(28, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(quad ? 16 : 6, 2)));
            if (quad)
            {
                StoreWord(36, BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(18, 2)));
            }
            int bucket = (sum / count) >> 4;
            ImmutableArray<NativeBattleGfPacketByteState> states = mask.ToImmutableArray();
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4, 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive? primitive = bucket < 4095 ? new(checked(orderingBaseBucket + bucket), words, payload.ToImmutableArray()) { PhysicalWriteMask = states } : null;
            if (primitive is not null && pilotIdentity is { } identity && pilotWorld is not null)
            {
                System.Numerics.Vector3[] world = new System.Numerics.Vector3[count];
                for (int corner = 0; corner < count; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.Slice(corner * 2, 2)) & 0xFFF;
                    world[corner] = pilotWorld[index];
                }
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397 + faceIndex) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)count)) };
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
    }
}
