using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleControl18PhysicsStep(ImmutableArray<byte> Records, ImmutableArray<byte> PolygonStates, ushort LiveCount);

public static class NativeBattleControl18Physics
{
    public static NativeBattleControl18PhysicsStep Advance(ReadOnlySpan<byte> source, ReadOnlySpan<byte> polygonStates, ushort liveCount, ushort mode)
    {
        if (source.Length % 68 != 0)
        {
            throw new InvalidDataException("Native breakup physics requires complete68-byte records.");
        }
        byte[] records = source.ToArray();
        byte[] states = polygonStates.ToArray();
        if (mode > 7 || mode == 4)
        {
            return new(records.ToImmutableArray(), states.ToImmutableArray(), liveCount);
        }
        for (int offset = 0; offset < records.Length; offset += 68)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(records.AsSpan(offset)) == 0)
            {
                continue;
            }
            if (mode is 1 or 3)
            {
                Move(4, 0x3C, 0x3E);
                Move(12, 0x40, 0x42);
                Move(8, 0x16, 0x1E);
                if (Dword(8) > 0)
                {
                    foreach (int word in new[] { 0x1E, 0x16, 0x3E, 0x3C, 0x42, 0x40 })
                    {
                        Word(word, 0);
                    }
                }
            }
            else if (mode == 2)
            {
                Move(4, 0x3C, 0x3E);
                Move(8, 0x16, 0x1E);
                Move(12, 0x40, 0x42);
            }
            else
            {
                Move(8, 0x16, 0x1E);
                Word(0x32, unchecked((short)(SignedWord(0x32) + SignedWord(0x34))));
            }
            sbyte life = unchecked((sbyte)records[offset + 0x2E]);
            records[offset + 0x2E] = unchecked((byte)(life - 1));
            if (life <= 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(offset), 0);
                liveCount = unchecked((ushort)(liveCount - 1));
                ushort polygon = BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan(offset + 0x30));
                states[polygon] = 255;
            }

            short SignedWord(int field) => BinaryPrimitives.ReadInt16LittleEndian(records.AsSpan(offset + field));
            int Dword(int field) => BinaryPrimitives.ReadInt32LittleEndian(records.AsSpan(offset + field));
            void Word(int field, short value) => BinaryPrimitives.WriteInt16LittleEndian(records.AsSpan(offset + field), value);
            void Move(int position, int velocity, int acceleration)
            {
                Word(velocity, unchecked((short)(SignedWord(velocity) + SignedWord(acceleration))));
                BinaryPrimitives.WriteInt32LittleEndian(records.AsSpan(offset + position), unchecked(Dword(position) + (SignedWord(velocity) << 4)));
            }
        }
        return new(records.ToImmutableArray(), states.ToImmutableArray(), liveCount);
    }
}
