using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicColorStep(NativeBattleWaterExecutionFrame Frame, uint Color);
public sealed record NativeBattleCinematicScanlines37Services(Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleCinematicColorStep> ScaleColor, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

public static class NativeBattleCinematicScanlines37
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicScanlines37Services services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (SlotDword(0xBC) == 0)
        {
            uint manager = CoreDword(frame, 0x27973EC);
            uint allocated = CoreDword(frame, unchecked(manager + 0x74));
            frame = WriteCoreDword(frame, unchecked(manager + 0x74), unchecked(allocated + 0x1B8));
            frame = WriteSlotDword(frame, 0xBC, allocated);
            uint initialPayload = SlotDword(0xB8);
            short width = SignedWord(unchecked(initialPayload + 4));
            for (int index = 0; index < 216; index++)
            {
                StoreWord(unchecked(allocated + 8 + (uint)(index * 2)), unchecked((ushort)Random(width)));
            }
            StoreWord(allocated, unchecked((ushort)Random(60000)));
            StoreWord(unchecked(allocated + 2), unchecked((ushort)Random(60000)));
            StoreWord(unchecked(allocated + 4), unchecked((ushort)Random(60000)));
        }
        uint payload = SlotDword(0xB8);
        uint state = SlotDword(0xBC);
        int noiseScale = unchecked((short)SlotWord(frame, 0x98));
        uint work = Work();
        for (int index = 0; index < 216; index++)
        {
            ushort scaled = unchecked((ushort)(Word(unchecked(state + 8 + (uint)(index * 2))) * noiseScale));
            StoreWord(unchecked(work + (uint)(index * 2)), scaled);
        }
        work = Work();
        int verticalScale = unchecked((short)SlotWord(frame, 0x96));
        if (verticalScale > 0)
        {
            int position = unchecked(-108 * verticalScale);
            for (int index = 0; index < 216; index++)
            {
                StoreByte(unchecked(work + 0x200 + (uint)index), 0);
                StoreByte(unchecked(work + 0x300 + (uint)index), unchecked((byte)((position >> 8) + 108)));
                position = unchecked(position + verticalScale);
            }
        }
        int border = unchecked(108 - ((unchecked((short)SlotWord(frame, 0x96)) * 108) >> 8));
        work = Work();
        for (int index = 0; index < border; index++)
        {
            StoreByte(unchecked(work + 0x2D7 - (uint)index), 0x80);
            StoreByte(unchecked(work + 0x200 + (uint)index), 0x80);
        }
        if (unchecked((short)SlotWord(frame, 0x96)) < 0)
        {
            work = Work();
            for (int index = 0; index < 216; index++)
            {
                StoreByte(unchecked(work + 0x200 + (uint)index), 0x80);
                StoreByte(unchecked(work + 0x300 + (uint)index), 108);
            }
            StoreByte(unchecked(work + 0x26C), 0);
        }
        for (int lane = 0; lane < 3; lane++)
        {
            uint address = unchecked(state + (uint)(lane * 2));
            StoreWord(address, unchecked((ushort)(Word(address) + Word(unchecked(payload + 6 + (uint)(lane * 2))))));
            frame = frame.WriteTemporaryDword(0x80 + lane * 4, (uint)(Word(address) >> 8));
        }
        uint page = (((uint)(Word(payload) & 0xFC0) | 0x4000) >> 2 | (uint)(Word(unchecked(payload + 2)) & 0x100)) >> 4;
        page |= (uint)(ReadSlot(frame, 0x92, 1)[0] & 0x60);
        frame = frame.WriteTemporaryDword(0x64, page);
        frame = frame.WriteTemporaryDword(0x6C, SlotDword(0xCC) & 0x02000000);
        NativeBattleCinematicColorStep first = services.ScaleColor(frame, frame.ReadTemporaryDword(0x6C) | 0x24804040, unchecked((short)SlotWord(frame, 0x8E)));
        frame = first.Frame.WriteTemporaryDword(0x70, first.Color);
        NativeBattleCinematicColorStep second = services.ScaleColor(frame, frame.ReadTemporaryDword(0x6C) | 0x24404060, unchecked((short)SlotWord(frame, 0x8E)));
        frame = second.Frame.WriteTemporaryDword(0x74, second.Color);
        int side = unchecked((short)SlotWord(frame, 0x94));
        if (side < 160)
        {
            int margin = unchecked(160 - side);
            if (margin < 0)
            {
                margin = 160;
            }
            uint output = Cursor();
            Emit(output, [0x05000000, 0x28000000, 0, unchecked((uint)margin), 0x00D00000, unchecked(0x00D00000u + (uint)margin)]);
            output = unchecked(output + 24);
            Emit(output, [0x05000000, 0x28000000, 320, unchecked((uint)(320 - margin)), 0x00D00140, unchecked(0x00D00140u - (uint)margin)]);
            SetCursor(unchecked(output + 24));
        }
        int stripWidth = unchecked((short)SlotWord(frame, 0x8C));
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)stripWidth));
        int center = -((stripWidth - 320) / 2);
        work = Work();
        uint cursor = Cursor();
        for (int index = 0; index < 216; index++)
        {
            byte row = Read(unchecked(work + 0x300 + (uint)index), 1)[0];
            uint visited = unchecked(work + 0x200 + row);
            if (Read(visited, 1)[0] != 0)
            {
                continue;
            }
            StoreByte(visited, 1);
            int offset = SignedWord(unchecked(work + (uint)(index * 2)));
            Triangle(cursor, frame.ReadTemporaryDword(0x70), (offset >> 8) + center, row, index);
            cursor = unchecked(cursor + 32);
            offset = SignedWord(unchecked(work + (uint)(index * 2)));
            Triangle(cursor, frame.ReadTemporaryDword(0x74), (offset >> 9) + center, row, index);
            cursor = unchecked(cursor + 32);
        }
        SetCursor(cursor);
        for (int lane = 0; lane < 3; lane++)
        {
            if (Word(unchecked(payload + 6 + (uint)(lane * 2))) == 0)
            {
                continue;
            }
            uint row = frame.ReadTemporaryDword(0x80 + lane * 4);
            work = Work();
            if (unchecked((sbyte)Read(unchecked(work + 0x200 + row), 1)[0]) < 0)
            {
                continue;
            }
            uint color = frame.ReadTemporaryDword(0x6C) | (lane == 0 ? 0x2C70B0B0u : lane == 1 ? 0x2E70C070u : 0x2EF0F0F0u);
            uint output = Cursor();
            Quad(output, color, center - 16, unchecked((int)row));
            SetCursor(unchecked(output + 40));
        }
        return frame;

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint Work() => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
        uint Cursor() => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C));
        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        short SignedWord(uint address) => unchecked((short)Word(address));

        void SetCursor(uint value)
        {
            frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), value);
        }

        void StoreByte(uint address, byte value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [value]) };
        }

        void StoreWord(uint address, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
        }

        int Random(int operand)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            int magnitude = unchecked((int)((uint)(value * operand) >> 15));
            return (value & 1) != 0 ? unchecked(-magnitude) : magnitude;
        }

        void Emit(uint output, ImmutableArray<uint> words, NativeBattleGfPacketByteState[]? retainedMask = null)
        {
            byte[] bytes = new byte[words.Length * 4];
            for (int index = 0; index < words.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = retainedMask?.ToImmutableArray() ?? Enumerable.Range(0, bytes.Length).Select(index => index >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = mask };
            frame = services.Append(frame, output, frame.ReadTemporaryDword(0x14C), new([new(0, bytes.ToImmutableArray(), mask, primitive)], bytes.Length));
        }

        void Triangle(uint output, uint color, int left, int row, int sourceRow)
        {
            ushort right = unchecked((ushort)(left + frame.ReadTemporaryWord(0x60)));
            uint pageWord = (uint)frame.ReadTemporaryWord(0x64) << 16;
            ImmutableArray<uint> words = [0x07000000, color, Pack(left, row), (uint)(sourceRow & 255) << 8, Pack(right, row), 255u | ((uint)(sourceRow & 255) << 8) | pageWord, Pack(right, row + 1), 255u | ((uint)((sourceRow + 1) & 255) << 8)];
            NativeBattleGfPacketByteState[] mask = Enumerable.Range(0, 32).Select(index => index >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToArray();
            mask[14] = mask[15] = mask[30] = mask[31] = NativeBattleGfPacketByteState.Untouched;
            Emit(output, words, mask);
        }

        void Quad(uint output, uint color, int left, int row)
        {
            int right = unchecked(left + (short)frame.ReadTemporaryWord(0x60));
            uint pageWord = (uint)frame.ReadTemporaryWord(0x64) << 16;
            ImmutableArray<uint> words = [0x09000000, color, Pack(left, row), (uint)(row & 255) << 8, Pack(right, row), 255u | ((uint)(row & 255) << 8) | pageWord, Pack(left, row + 1), (uint)((row + 1) & 255) << 8, Pack(right, row + 1), 255u | ((uint)((row + 1) & 255) << 8)];
            NativeBattleGfPacketByteState[] mask = Enumerable.Range(0, 40).Select(index => index >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToArray();
            mask[14] = mask[15] = mask[30] = mask[31] = mask[38] = mask[39] = NativeBattleGfPacketByteState.Untouched;
            Emit(output, words, mask);
        }
    }

    private static uint Pack(int x, int y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
}
