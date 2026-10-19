using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleControl18FlyingStep(ImmutableArray<byte> Records, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleControl18Flying
{
    public static NativeBattleControl18FlyingStep Advance(ReadOnlySpan<byte> records, ushort mode, ReadOnlySpan<uint> colorTable, Func<NativeBattleVector, NativeBattleModuleProjectedVertex> project, int orderingBaseBucket)
    {
        if (records.Length % 28 != 0)
        {
            throw new InvalidDataException("Native flying particles require complete28-byte records.");
        }
        byte[] bytes = records.ToArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        for (int record = 0; record < bytes.Length; record += 28)
        {
            if (bytes[record + 24] == 0)
            {
                continue;
            }
            bool accelerate = mode is 0 or 1 or 2 or 3 or 5 or 6 or 7;
            if (accelerate)
            {
                bytes[record + 25] = unchecked((byte)(bytes[record + 25] - 1));
                if (unchecked((sbyte)bytes[record + 25]) < 0)
                {
                    bytes[record + 24] = 0;
                    continue;
                }
                if (mode is 1 or 3)
                {
                    Move(0);
                    Move(2);
                    Move(1);
                    if (Dword(4) > 0)
                    {
                        Word(16, 0);
                        Word(18, 0);
                    }
                }
                else
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Word(12 + axis * 4, unchecked((short)(SignedWord(12 + axis * 4) + SignedWord(14 + axis * 4))));
                    }
                    for (int axis = 0; axis < 3; axis++)
                    {
                        StoreDword(axis * 4, unchecked(Dword(axis * 4) + (SignedWord(12 + axis * 4) << 4)));
                    }
                }
            }
            int colorIndex = unchecked((sbyte)bytes[record + 25]);
            if ((uint)colorIndex >= colorTable.Length)
            {
                throw new NotSupportedException("Native flying particle color index leaves its supplied original palette.");
            }
            uint color = colorTable[colorIndex] | 0x6A000000;
            NativeBattleVector position;
            if (mode == 6)
            {
                int radius = Dword(0) >> 8;
                int angle = Dword(8) >> 4;
                position = new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), unchecked((short)(Dword(4) >> 8)), unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)));
            }
            else
            {
                position = new(unchecked((short)(Dword(0) >> 8)), unchecked((short)(Dword(4) >> 8)), unchecked((short)(Dword(8) >> 8)));
            }
            NativeBattleModuleProjectedVertex point = project(position);
            int quarterDepth = point.Depth >> 2;
            int depth = quarterDepth << 2;
            int bucket = quarterDepth >> 2;
            byte[] packet = new byte[12];
            packet[3] = 2;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), color);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), point.Screen);
            ImmutableArray<NativeBattleGfPacketByteState> mask = [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, .. Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 9)];
            NativeBattleGeometryPrimitive? primitive = bucket < 4095 ? new(checked(orderingBaseBucket + bucket), [0x02000000, color, point.Screen], [depth, depth, depth, depth]) { PhysicalWriteMask = mask } : null;
            attempts.Add(new(allocation, packet.ToImmutableArray(), mask, primitive));
            allocation = checked(allocation + 12);

            short SignedWord(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(record + offset, 2));
            int Dword(int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(record + offset, 4));
            void Word(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(record + offset, 2), value);
            void StoreDword(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(record + offset, 4), value);
            void Move(int axis)
            {
                Word(12 + axis * 4, unchecked((short)(SignedWord(12 + axis * 4) + SignedWord(14 + axis * 4))));
                StoreDword(axis * 4, unchecked(Dword(axis * 4) + (SignedWord(12 + axis * 4) << 4)));
            }
        }
        return new(bytes.ToImmutableArray(), new(attempts.ToImmutable(), allocation));
    }
}
