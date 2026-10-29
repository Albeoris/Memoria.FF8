using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa89ShardDrawing
{
    public static NativeBattleGfGeometryBatch Draw6FCFD0(NativeBattleEffectArena arena, bool captureVisual = false)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int cursor = 0;
        for (int index = 0; index < 120; index++)
        {
            int record = 24228 + index * 52;
            if (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 6, 2)) == 0)
            {
                continue;
            }
            _ = arena.Read(record, 4);
            _ = arena.Read(record + 20, 18);
            _ = arena.Read(record + 40, 10);
            byte[] shard = arena.ReadSnapshot(record, 52).Data;
            byte[] packet = new byte[32];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[32];
            Store(12, shard.AsSpan(40, 4));
            Store(20, shard.AsSpan(44, 4));
            Store(28, shard.AsSpan(48, 2));
            int clipped = 0;
            for (int corner = 0; corner < 3; corner++)
            {
                ushort x = unchecked((ushort)(Word(0) + Word(20 + corner * 6)));
                ushort y = unchecked((ushort)(Word(2) + Word(22 + corner * 6)));
                Store(8 + corner * 8, BitConverter.GetBytes(x));
                Store(10 + corner * 8, BitConverter.GetBytes(y));
                if (x > 2560)
                {
                    clipped |= 1 << corner;
                }
                if (y > 1728)
                {
                    clipped |= 16 << corner;
                }
            }
            NativeBattleGeometryPrimitive? primitive = null;
            if ((clipped & 7) != 7 && (clipped & 0x70) != 0x70)
            {
                Store(0, BitConverter.GetBytes(0x07000000u));
                Store(4, BitConverter.GetBytes(612401280u));
                ImmutableArray<uint> words = Enumerable.Range(0, 8).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(word * 4))).ToImmutableArray();
                primitive = new(1024, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask.ToImmutableArray() };
                if (captureVisual)
                {
                    System.Numerics.Vector3 Screen(int corner) => new(BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(8 + corner * 8)), BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(10 + corner * 8)), 0);
                    primitive = primitive with { PilotGeometry = new(NativeBattleRinoa89Visuals.Row(arena, 24228, index), Screen(0), Screen(1), Screen(2), default, 3) { ScreenSpace = true } };
                }
            }
            attempts.Add(new(cursor, packet.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                cursor += 32;
            }

            short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(shard.AsSpan(offset));
            void Store(int offset, ReadOnlySpan<byte> bytes)
            {
                bytes.CopyTo(packet.AsSpan(offset));
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, bytes.Length);
            }
        }
        return new(attempts.ToImmutable(), cursor);
    }
}
