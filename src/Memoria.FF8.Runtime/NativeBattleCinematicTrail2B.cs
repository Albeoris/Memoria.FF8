using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicTrail2BServices(Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> TexturePage, Func<NativeBattleWaterExecutionFrame, short, uint> TextureClut, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleWaterExecutionFrame> Project, Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleCinematicColorStep> ScaleColor, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>Retained sampled centerline, projected widths and textured ribbon joints.</summary>
public static partial class NativeBattleCinematicTrail2B
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicTrail2BServices services)
    {
        if (SlotDword(0xBC) == 0)
        {
            uint payload = SlotDword(0xB8);
            int count = Word(unchecked(payload + 2));
            uint allocated;
            (frame, allocated) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)(count * 8 + 32)));
            frame = WriteSlotDword(frame, 0xBC, allocated);
            uint state = SlotDword(0xBC);
            StoreWord(unchecked(state + 4), unchecked((ushort)count));
            short texture = Word(payload);
            frame = services.TexturePage(frame, texture);
            StoreWord(unchecked(state + 8), frame.ReadTemporaryWord(0xFC));
            StoreWord(unchecked(state + 10), unchecked((ushort)services.TextureClut(frame, texture)));
            StoreWord(unchecked(state + 0x18), unchecked((ushort)Word(unchecked(payload + 8))));
            uint first = services.ResolveShape(frame, Word(unchecked(payload + 4)));
            StoreDword(unchecked(state + 0xC), unchecked(first + Dword(unchecked(first + 0x14))));
            StoreWord(unchecked(state + 0x14), unchecked((ushort)Word(unchecked(first + 0x18))));
            uint second = services.ResolveShape(frame, Word(unchecked(payload + 6)));
            StoreDword(unchecked(state + 0x10), unchecked(second + Dword(unchecked(second + 0x14))));
            StoreWord(unchecked(state + 0x16), unchecked((ushort)Word(unchecked(second + 0x18))));
            frame = frame.WriteTemporaryDword(0x74, unchecked((uint)Word(unchecked(payload + 14))));
            StoreByte(unchecked(state + 0x1A), Byte(unchecked(payload + 10)));
            StoreByte(unchecked(state + 0x1B), Byte(unchecked(payload + 12)));
            StoreWord(unchecked(state + 2), 0);
            bool reverse = (frame.ReadTemporaryByte(0x74) & 1) != 0;
            int firstCount = Word(unchecked(state + 0x14));
            int secondCount = Word(unchecked(state + 0x16));
            frame = frame.WriteTemporaryDword(0x78, unchecked((uint)((firstCount << 8) / count * (reverse ? -1 : 1))));
            frame = frame.WriteTemporaryDword(0x7C, unchecked((uint)((secondCount << 8) / count * (reverse ? -1 : 1))));
            frame = frame.WriteTemporaryDword(0x80, reverse ? unchecked((uint)((firstCount << 8) - 256)) : 0);
            frame = frame.WriteTemporaryDword(0x84, reverse ? unchecked((uint)((secondCount << 8) - 256)) : 0);
            frame = frame.WriteTemporaryDword(0x88, 0);
            frame = frame.WriteTemporaryDword(0x8C, unchecked((uint)((unchecked((short)SlotWord(frame, 0x96)) << 8) / count)));
            uint firstPoints = Dword(unchecked(state + 0xC));
            uint secondPoints = Dword(unchecked(state + 0x10));
            frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)Word(unchecked(state + 0x18))));
            int[] weights = [Random(), Random(), Random()];
            uint destination = unchecked(state + 0x20);
            for (int index = 0; index < count; index++)
            {
                uint from = unchecked(firstPoints + (uint)((unchecked((int)frame.ReadTemporaryDword(0x80)) >> 8) * 8));
                uint to = unchecked(secondPoints + (uint)((unchecked((int)frame.ReadTemporaryDword(0x84)) >> 8) * 8));
                for (int lane = 0; lane < 3; lane++)
                {
                    uint offset = (uint)(lane * 2);
                    int origin = Word(unchecked(from + offset));
                    int value = unchecked(origin + unchecked((Word(unchecked(to + offset)) - origin) * weights[lane]) / 256);
                    if (lane == 1)
                    {
                        value = unchecked(value + (unchecked((int)frame.ReadTemporaryDword(0x88)) >> 8));
                    }
                    StoreWord(unchecked(destination + offset), unchecked((ushort)value));
                }
                StoreWord(unchecked(destination + 6), frame.ReadTemporaryWord(0xF0));
                destination = unchecked(destination + 8);
                frame = frame.WriteTemporaryDword(0x80, unchecked(frame.ReadTemporaryDword(0x80) + frame.ReadTemporaryDword(0x78)));
                frame = frame.WriteTemporaryDword(0x84, unchecked(frame.ReadTemporaryDword(0x84) + frame.ReadTemporaryDword(0x7C)));
                frame = frame.WriteTemporaryDword(0x88, unchecked(frame.ReadTemporaryDword(0x88) + frame.ReadTemporaryDword(0x8C)));
            }
        }
        uint descriptor = SlotDword(0xBC);
        frame = frame.WriteTemporaryDword(0x60, unchecked(descriptor + 0x20));
        uint work = Manager(0x74);
        frame = services.PrepareBasis(frame);
        int total = Word(unchecked(descriptor + 4));
        frame = frame.WriteTemporaryDword(0x64, unchecked((uint)total)).WriteTemporaryWord(0xC4, 0).WriteTemporaryWord(0xC2, 0);
        int visible = Math.Min(unchecked((short)SlotWord(frame, 0x94)) + 1, total);
        int ramp = visible - total;
        for (int remaining = total; remaining > 0; remaining--)
        {
            int selected = (remaining - 1) % Word(unchecked(descriptor + 4));
            uint point = unchecked(frame.ReadTemporaryDword(0x60) + (uint)(selected * 8));
            frame = services.Project(frame, point, work);
            uint color = SlotDword(0xCC);
            int factor = remaining <= 4 ? remaining * 64 - 64 : remaining > visible ? Word(unchecked(work + 4)) : ramp;
            if (remaining <= 4 || factor < 4)
            {
                NativeBattleCinematicColorStep scaled = services.ScaleColor(frame, color, unchecked((short)(remaining <= 4 ? factor : factor << 6)));
                frame = scaled.Frame;
                color = scaled.Color;
            }
            StoreDword(unchecked(work + 0x14), color);
            work = unchecked(work + 32);
            ramp++;
        }
        visible = Math.Min(unchecked((short)SlotWord(frame, 0x94)) + 1, Word(unchecked(descriptor + 4)));
        uint start = unchecked(Manager(0x74) + (uint)((Word(unchecked(descriptor + 4)) - visible) * 32));
        int v = unchecked(unchecked((short)SlotWord(frame, 0x98)) * Byte(unchecked(descriptor + 0x1B)));
        return DrawRibbon(frame, descriptor, start, visible, v, services);

        int Random()
        {
            NativeCrtRandomState random = frame.Random.Draw(out ushort value);
            frame = frame with { Random = random };
            return unchecked(value * 256) >> 15;
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint Manager(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        byte Byte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void StoreByte(uint address, byte value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), new byte[] { value }) };
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        void StoreDword(uint address, uint value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
    }
}
