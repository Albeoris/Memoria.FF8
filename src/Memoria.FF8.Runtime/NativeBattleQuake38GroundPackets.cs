using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleQuake38GroundPackets
{
    public static NativeBattleGfGeometryBatch Face8D9BE0(ImmutableArray<byte> entry, int first, int second, int centroid, short height, NativeBattleCameraProjection projection, short zsf3, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (entry.IsDefault || entry.Length != 112 || (uint)first >= 5 || (uint)second >= 5 || (uint)centroid >= 5)
        {
            throw new InvalidDataException("Native Quake face requires its split-entry vertex selectors.");
        }
        int[] selectors = [first, second, centroid];
        byte[] bytes = new byte[32];
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, 32).ToArray();
        Dword(0, 0x07000000);
        Dword(4, BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan().Slice(40)));
        for (int index = 0; index < 3; index++)
        {
            int selector = selectors[index];
            Store(12 + index * 8, entry[100 + selector]);
            Store(13 + index * 8, entry[105 + selector]);
        }
        Word(14, BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan().Slice(50)));
        Word(22, BinaryPrimitives.ReadUInt16LittleEndian(entry.AsSpan().Slice(48)));
        return NativeBattleQuake38TrianglePackets.Emit(selectors.Select(Position).ToImmutableArray(), bytes.ToImmutableArray(), mask.ToImmutableArray(), projection, zsf3, pilotIdentity: pilotIdentity);

        NativeBattleVector Position(int selector)
        {
            short x = BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8));
            short y = BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8 + 2));
            short z = BinaryPrimitives.ReadInt16LittleEndian(entry.AsSpan().Slice(selector * 8 + 4));
            return new(x, unchecked((short)(y + height)), z);
        }
        void Store(int offset, byte value)
        {
            bytes[offset] = value;
            mask[offset] = NativeBattleGfPacketByteState.Known;
        }
        void Word(int offset, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
        }
        void Dword(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
        }
    }
}
