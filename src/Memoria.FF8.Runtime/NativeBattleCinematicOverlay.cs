using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicOverlayStep(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets);

/// <summary>The cinematic signed-channel screen quad and its separately linked draw mode.</summary>
public static class NativeBattleCinematicOverlay
{
    public static NativeBattleCinematicOverlayStep Draw(NativeBattleWaterExecutionFrame frame, Func<uint, int> resolveBucket)
    {
        uint depth = (ReadSlot(frame, 0xDE, 1)[0] & 0x60) != 0 ? BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xC0, 4)) : 0;
        frame = frame.WriteTemporaryDword(0xF0, 0x20);
        int red = Channel(0x94);
        int green = Channel(0x96);
        int blue = Channel(0x98);
        uint color = unchecked((uint)(red | (green << 8) | (blue << 16)));
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        attempts.Add(new(0, [0, 0, 0, 5], [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Known], null));
        if (color == 0)
        {
            return new(frame, new(attempts.ToImmutable(), 0));
        }
        uint table = frame.ReadTemporaryDword(0x14C);
        Add(0, [0x05000000, 0x2A000000 | color, 0, 320, 256u << 16, (256u << 16) | 320], resolveBucket(unchecked(table + 4 * depth)));
        Add(24, [0x02000000, 0xE1000000 | frame.ReadTemporaryDword(0xF0), 0], resolveBucket(table));
        uint manager = CoreDword(frame, 0x27973EC);
        frame = WriteCoreDword(frame, manager + 0x7C, unchecked(CoreDword(frame, manager + 0x7C) + 36));
        return new(frame, new(attempts.ToImmutable(), 36));

        int Channel(int offset)
        {
            int value = unchecked((short)SlotWord(frame, offset));
            if (value < 0)
            {
                frame = frame.WriteTemporaryDword(0xF0, 0x40);
                value = -value;
            }
            return Math.Min(value, 255);
        }

        void Add(int offset, ImmutableArray<uint> words, int bucket)
        {
            byte[] bytes = new byte[words.Length * 4];
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, bytes.Length).Select(index => index < 3 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(bucket, words, []) { PhysicalWriteMask = mask };
            attempts.Add(new(offset, bytes.ToImmutableArray(), mask, primitive));
        }
    }
}
