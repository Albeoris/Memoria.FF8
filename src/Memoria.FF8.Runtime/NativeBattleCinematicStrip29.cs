using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicStripProjection(NativeBattleWaterExecutionFrame Frame, uint Screen, int QuarterDepth);
public sealed record NativeBattleCinematicStrip29Services(Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> TexturePage, Func<NativeBattleWaterExecutionFrame, short, uint> TextureClut, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, NativeBattleVector, NativeBattleCinematicStripProjection> Project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>Three projected columns form two textured quads per sine strip band.</summary>
public static class NativeBattleCinematicStrip29
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicStrip29Services services)
    {
        uint payload = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
        short texture = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(payload, 2), 2));
        frame = services.TexturePage(frame, texture);
        frame = frame.WriteTemporaryDword(0x68, frame.ReadTemporaryDword(0xFC));
        frame = frame.WriteTemporaryDword(0x6C, services.TextureClut(frame, texture));
        frame = frame.WriteTemporaryDword(0x64, (ReadSlot(frame, 0xDE, 1)[0] & 0x20) != 0 ? SlotDword(0xC0) : uint.MaxValue);
        frame = services.PrepareBasis(frame);
        uint work = Manager(0x74);
        frame = frame.WriteTemporaryWord(0xC4, 0x800);
        int angle = unchecked((short)SlotWord(frame, 0x96)) << 4;
        int step = unchecked((short)SlotWord(frame, 0x98));
        int amplitude = unchecked((short)SlotWord(frame, 0x94));
        for (int row = 0; row < 127; row++)
        {
            frame = frame.WriteTemporaryWord(0xC2, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) >> 12)));
            for (int column = 0; column < 3; column++)
            {
                short x = column switch { 0 => unchecked((short)0xF810), 1 => 0, _ => 0x7F0 };
                frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)x));
                NativeBattleCinematicStripProjection projected = services.Project(frame, new(x, unchecked((short)frame.ReadTemporaryWord(0xC2)), unchecked((short)frame.ReadTemporaryWord(0xC4))));
                frame = projected.Frame;
                Store(work, projected.Screen);
                StoreWord(unchecked(work + 4), unchecked((ushort)projected.QuarterDepth));
                work = unchecked(work + 8);
            }
            frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(frame.ReadTemporaryWord(0xC4) - 32)));
            angle = unchecked(angle + step);
        }
        uint top = Manager(0x74);
        uint output = Manager(0x7C);
        uint command = SlotDword(0xCC) | 0x2C000000;
        for (int band = 0; band < 126; band++)
        {
            byte v = unchecked((byte)(1 + 2 * band));
            byte next = unchecked((byte)(v + 2));
            for (int half = 0; half < 2; half++)
            {
                uint first = unchecked(top + (uint)(half == 0 ? 0 : 16));
                uint second = unchecked(top + 8);
                uint third = unchecked(first + 24);
                uint fourth = unchecked(top + 32);
                byte[] data = new byte[40];
                data[3] = 9;
                Put(4, command);
                Put(8, Dword(first));
                Put(16, Dword(second));
                Put(24, Dword(third));
                Put(32, Dword(fourth));
                data[12] = data[28] = 0;
                data[20] = data[36] = 127;
                data[13] = data[21] = v;
                data[29] = data[37] = next;
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(14), unchecked((ushort)frame.ReadTemporaryDword(0x6C)));
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(22), unchecked((ushort)frame.ReadTemporaryDword(0x68)));
                int depth = unchecked((int)frame.ReadTemporaryDword(0x64));
                if (depth < 0)
                {
                    depth = (SignedDepth(first) + SignedDepth(second) + SignedDepth(third) + SignedDepth(fourth)) / 4;
                }
                uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + ((uint)depth & 0xFFFFFFFC));
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 40).Select(index => index >= 3 && index is not (30 or 31 or 38 or 39) ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                NativeBattleGeometryPrimitive primitive = new(0, words, []) { PhysicalWriteMask = mask };
                frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], 40));
                output = unchecked(output + 40);

                void Put(int offset, uint value)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                }
            }
            top = unchecked(top + 24);
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint Manager(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        short SignedDepth(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(address + 4), 2), 2));

        void Store(uint address, uint value)
        {
            byte[] data = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), data) };
        }

        void StoreWord(uint address, ushort value)
        {
            byte[] data = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(data, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), data) };
        }
    }
}
