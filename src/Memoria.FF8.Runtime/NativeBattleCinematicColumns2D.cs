using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicColumns2D
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> append)
    {
        ArgumentNullException.ThrowIfNull(append);
        if (BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4)) == 0)
        {
            uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
            uint page = (((uint)(Word(payload) & 0xFC0) | 0x4000) >> 2 | (uint)(Word(unchecked(payload + 2)) & 0x100)) >> 4;
            frame = WriteSlotDword(frame, 0xC0, page);
            uint manager = CoreDword(frame, 0x27973EC);
            uint storage = CoreDword(frame, unchecked(manager + 0x74));
            frame = WriteCoreDword(frame, unchecked(manager + 0x74), unchecked(storage + 0x2810));
            frame = WriteSlotDword(frame, 0xBC, storage);
            StoreWord(storage, 0);
            int angleStep = Word(unchecked(payload + 10));
            int amplitudeStep = Word(unchecked(payload + 6));
            int angle = Word(unchecked(payload + 8));
            int randomWidth = Word(unchecked(payload + 12));
            int randomHeight = Word(unchecked(payload + 14));
            int baseHeight = Word(unchecked(payload + 16));
            int baseWidth = Word(unchecked(payload + 18));
            int amplitude = Word(unchecked(payload + 4));
            uint record = unchecked(storage + 16);
            for (int index = 0; index < 320; index++)
            {
                StoreWord(record, 0);
                int wave = unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) >> 12;
                int width = unchecked(SignedRandom(randomWidth) + baseWidth + amplitude + wave);
                StoreWord(unchecked(record + 4), unchecked((ushort)width));
                int height = unchecked(SignedRandom(baseHeight) + randomHeight);
                StoreWord(unchecked(record + 6), unchecked((ushort)height));
                angle = unchecked(angle + angleStep);
                StoreDword(unchecked(record + 16), 0);
                StoreDword(unchecked(record + 12), 0);
                StoreDword(unchecked(record + 8), 0xE000);
                amplitude = unchecked(amplitude + amplitudeStep);
                record = unchecked(record + 32);
            }
        }
        uint state = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xBC, 4));
        uint parameters = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        frame = frame.WriteTemporaryDword(0x60, (uint)Word(unchecked(parameters + 20)) << 8);
        frame = frame.WriteTemporaryDword(0x64, BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xC0, 4)));
        frame = frame.WriteTemporaryDword(0x68, 0xE1000040u | (uint)(ReadSlot(frame, 0x92, 1)[0] & 0x20));
        int age = SignedWord(state);
        if (age >= 15)
        {
            age = 15;
        }
        int a = unchecked(128 - age * 8);
        int b = unchecked(128 + age * 8);
        uint color = unchecked((uint)a);
        color = unchecked((color | 0x3C00) << 8);
        color |= unchecked((uint)a);
        color = unchecked(color << 8) | unchecked((uint)b);
        frame = frame.WriteTemporaryDword(0x70, color).WriteTemporaryDword(0x74, 0x808080);
        uint output = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C));
        uint currentRecord = unchecked(state + 16);
        for (int group = 0; group < 5; group++)
        {
            for (int column = 0; column < 64; column++)
            {
                if (frame.ReadTemporaryByte(0x145) == 0)
                {
                    ushort acceleration = unchecked((ushort)(Word(unchecked(currentRecord + 4)) + Word(unchecked(currentRecord + 6))));
                    StoreWord(unchecked(currentRecord + 4), acceleration);
                    int bottom = unchecked((int)Dword(unchecked(currentRecord + 8)) + (short)acceleration);
                    StoreDword(unchecked(currentRecord + 8), unchecked((uint)bottom));
                    int threshold = unchecked((int)frame.ReadTemporaryDword(0x60));
                    if (bottom >= threshold)
                    {
                        StoreWord(currentRecord, 1);
                        StoreWord(unchecked(currentRecord + 12), acceleration);
                        if (bottom >= unchecked(threshold + 0x10000))
                        {
                            StoreWord(currentRecord, (ushort)(Word(currentRecord) | 0x8000));
                        }
                    }
                    ushort velocity = unchecked((ushort)(Word(unchecked(currentRecord + 12)) + Word(unchecked(currentRecord + 14))));
                    StoreWord(unchecked(currentRecord + 12), velocity);
                    StoreDword(unchecked(currentRecord + 16), unchecked(Dword(unchecked(currentRecord + 16)) + (uint)(int)(short)velocity));
                }
                if ((Read(unchecked(currentRecord + 1), 1)[0] & 0x80) == 0)
                {
                    int x = group * 64 + column;
                    int top = unchecked((int)Dword(unchecked(currentRecord + 16))) >> 8;
                    int bottom = unchecked((int)Dword(unchecked(currentRecord + 8))) >> 8;
                    byte[] data = new byte[52];
                    NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[52];
                    Byte(3, 12);
                    PacketWord(0x1A, unchecked((ushort)(frame.ReadTemporaryDword(0x64) + (uint)group)));
                    PacketDword(0x10, frame.ReadTemporaryDword(0x70));
                    PacketDword(4, frame.ReadTemporaryDword(0x70));
                    PacketDword(0x28, frame.ReadTemporaryDword(0x74));
                    PacketDword(0x1C, frame.ReadTemporaryDword(0x74));
                    PacketWord(0x20, unchecked((ushort)x));
                    PacketWord(8, unchecked((ushort)x));
                    PacketWord(0x2C, unchecked((ushort)(x + 1)));
                    PacketWord(0x14, unchecked((ushort)(x + 1)));
                    PacketWord(0x16, unchecked((ushort)top));
                    PacketWord(0xA, unchecked((ushort)top));
                    PacketWord(0x2E, unchecked((ushort)bottom));
                    PacketWord(0x22, unchecked((ushort)bottom));
                    Byte(0x24, (byte)column);
                    Byte(0xC, (byte)column);
                    Byte(0x19, 0);
                    Byte(0x30, unchecked((byte)(column + 1)));
                    Byte(0x18, unchecked((byte)(column + 1)));
                    Byte(0xD, 0);
                    Byte(0x31, 224);
                    Byte(0x25, 224);
                    Emit(data, mask);
                    if ((Read(currentRecord, 1)[0] & 1) != 0)
                    {
                        data = new byte[28];
                        mask = new NativeBattleGfPacketByteState[28];
                        Byte(3, 6);
                        PacketDword(8, 0);
                        PacketDword(0xC, 0x52000000);
                        PacketDword(4, frame.ReadTemporaryDword(0x68));
                        PacketDword(0x14, 0xFFFF00);
                        PacketWord(0x18, unchecked((ushort)x));
                        PacketWord(0x10, unchecked((ushort)x));
                        top = unchecked((int)Dword(unchecked(currentRecord + 16))) >> 8;
                        PacketWord(0x1A, unchecked((ushort)top));
                        PacketWord(0x12, unchecked((ushort)(top - 128)));
                        Emit(data, mask);
                    }

                    void Byte(int offset, byte value)
                    {
                        data[offset] = value;
                        mask[offset] = NativeBattleGfPacketByteState.Known;
                    }

                    void PacketWord(int offset, ushort value)
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
                        Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
                    }

                    void PacketDword(int offset, uint value)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                        Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
                    }
                }
                currentRecord = unchecked(currentRecord + 32);
            }
        }
        frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);
        if (frame.ReadTemporaryByte(0x145) == 0)
        {
            StoreWord(state, unchecked((ushort)(Word(state) + 1)));
        }
        return frame;

        byte[] Read(uint address, int length) => frame.Arena.Read(ArenaOffset(address, length), length);
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

        void StoreWord(uint address, ushort value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
        }

        void StoreDword(uint address, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), bytes) };
        }

        int SignedRandom(int operand)
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            int magnitude = unchecked((int)((uint)(value * operand) >> 15));
            return (value & 1) != 0 ? unchecked(-magnitude) : magnitude;
        }

        void Emit(byte[] data, NativeBattleGfPacketByteState[] mask)
        {
            ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            ImmutableArray<NativeBattleGfPacketByteState> physicalMask = mask.ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = physicalMask };
            frame = append(frame, output, frame.ReadTemporaryDword(0x14C), new([new(0, data.ToImmutableArray(), physicalMask, primitive)], data.Length));
            output = unchecked(output + (uint)data.Length);
        }
    }
}
