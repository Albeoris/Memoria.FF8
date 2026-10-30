using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicSpark53Services(Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, NativeBattleCinematicPointProjection> Project, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>A retained ring of accelerated particles with Cartesian or cylindrical projection.</summary>
public static class NativeBattleCinematicSpark53
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicSpark53Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint payload = SlotDword(0xB8);
        uint state = SlotDword(0xBC);
        int count = Word(payload + 4);
        if (state == 0)
        {
            (frame, state) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, unchecked((uint)(count * 28 + 16)));
            frame = WriteSlotDword(frame, 0xBC, state);
            for (int index = 0; index < count; index++)
            {
                StoreByte(state + 0x28 + (uint)(index * 28), 255);
            }
            StoreWord(state + 2, 0);
            StoreDword(state + 4, unchecked(payload + (uint)SignedWord(payload)));
        }
        uint reference = services.ResolveSlot(frame, SignedWord(payload + 6));
        for (int lane = 0; lane < 3; lane++)
        {
            int factor = SignedWord(reference + 0x8C + (uint)(lane * 2));
            for (int component = 0; component < 4; component++)
            {
                int value = unchecked(SignedWord(payload + 0xA + (uint)(lane * 8 + component * 2)) * factor) / 256;
                frame = frame.WriteTemporaryWord(0xA0 + component * 8 + lane * 2, unchecked((ushort)value));
            }
        }
        uint records = state + 16;
        frame = frame.WriteTemporaryDword(0xE0, records);
        ushort cursor = Word(state + 2);
        int spawn = SignedWord(reference + 0x94);
        for (int index = 0; index < spawn; index++)
        {
            uint record = unchecked(records + (uint)(cursor % count * 28));
            if (pilotIdentity is not null)
            {
                NativeBattlePilotVisualLifetimes.Allocated(frame.Arena, records, cursor % count);
            }
            for (int lane = 0; lane < 3; lane++)
            {
                int value = unchecked(Random(unchecked((short)SlotWord(frame, 0x8C + lane * 2))) + unchecked((short)SlotWord(frame, 0x94 + lane * 2)));
                StoreDword(record + (uint)(lane * 4), unchecked((uint)(value << 8)));
            }
            for (int lane = 0; lane < 3; lane++)
            {
                int offset = 0xA0 + lane * 2;
                StoreWord(record + 12 + (uint)(lane * 4), unchecked((ushort)(frame.ReadTemporaryWord(offset) + Random(unchecked((short)frame.ReadTemporaryWord(offset + 8))))));
                StoreWord(record + 14 + (uint)(lane * 4), unchecked((ushort)(frame.ReadTemporaryWord(offset + 16) + Random(unchecked((short)frame.ReadTemporaryWord(offset + 24))))));
            }
            StoreByte(record + 24, Byte(payload + 8));
            cursor++;
        }
        StoreWord(state + 2, cursor);
        int fixedDepth = unchecked((short)SlotWord(frame, 0xC0));
        uint orderingBase = frame.ReadTemporaryDword(0x14C);
        frame = frame.WriteTemporaryDword(0x64, fixedDepth == 0 ? 0 : unchecked(orderingBase + (uint)(fixedDepth & 0x3FFC)));
        frame = services.PrepareBasis(frame);
        frame = frame.WriteTemporaryDword(0xE4, Dword(state + 4));
        uint output = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C));
        for (int index = 0; index < count; index++)
        {
            uint record = records + (uint)(index * 28);
            if ((Byte(record + 24) & 128) != 0)
            {
                continue;
            }
            for (int lane = 0; lane < 3; lane++)
            {
                uint velocity = record + 12 + (uint)(lane * 4);
                StoreWord(velocity, unchecked((ushort)(Word(velocity) + Word(velocity + 2))));
                uint position = record + (uint)(lane * 4);
                StoreDword(position, unchecked(Dword(position) + (uint)(SignedWord(velocity) << 4)));
            }
            ushort mode = Word(payload + 2);
            if (mode == 0)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    frame = frame.WriteTemporaryWord(0xF0 + lane * 2, unchecked((ushort)((int)Dword(record + (uint)(lane * 4)) >> 8)));
                }
            }
            if (mode == 1)
            {
                int radius = unchecked((int)Dword(record)) >> 8;
                int angle = (unchecked((int)Dword(record + 8)) >> 4) & ~15;
                frame = frame.WriteTemporaryWord(0xF2, unchecked((ushort)((int)Dword(record + 4) >> 8)));
                frame = frame.WriteTemporaryWord(0xF0, unchecked((ushort)((unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12) + SlotWord(frame, 0x94))));
                frame = frame.WriteTemporaryWord(0xF4, unchecked((ushort)((unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12) + SlotWord(frame, 0x98))));
            }
            uint color = Dword(unchecked(frame.ReadTemporaryDword(0xE4) + (uint)(unchecked((sbyte)Byte(record + 24)) * 4))) | 0x6A000000;
            NativeBattleCinematicPointProjection projected = services.Project(frame);
            frame = projected.Frame;
            byte[] packet = new byte[12];
            packet[3] = 2;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), color);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), projected.Screen);
            uint ordering = frame.ReadTemporaryDword(0x64);
            if (ordering == 0)
            {
                ordering = unchecked(orderingBase + (uint)(projected.QuarterDepth & ~3));
            }
            ImmutableArray<NativeBattleGfPacketByteState> mask = [NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, NativeBattleGfPacketByteState.Untouched, .. Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 9)];
            NativeBattleGeometryPrimitive primitive = new(0, [BinaryPrimitives.ReadUInt32LittleEndian(packet), color, projected.Screen], []) { PhysicalWriteMask = mask };
            if (pilotIdentity is { } identity && projected.PilotWorld is { } anchor)
            {
                NativeBattlePilotVisualIdentity particleIdentity = NativeBattlePilotVisualLifetimes.Identity(identity.Effect, frame.Arena, records, index);
                primitive = primitive with { PilotGeometry = new(particleIdentity, anchor, anchor, anchor, anchor, 4) { ScreenOffsets = [new(0, 0), new(8, 0), new(0, 8), new(8, 8)] } };
                if (identity.Effect == 258)
                {
                    primitive = primitive with { PilotGeometry = primitive.PilotGeometry! with { UseSourceDepths = true, SourceDepths = System.Numerics.Vector4.Zero } };
                }
            }
            frame = services.Append(frame, output, ordering, new([new(0, packet.ToImmutableArray(), mask, primitive)], 12));
            StoreByte(record + 24, unchecked((byte)(Byte(record + 24) - 1)));
            output = unchecked(output + 12);
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        byte Byte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        void StoreByte(uint address, byte value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [value]) };
        }
        void StoreWord(uint address, ushort value)
        {
            byte[] raw = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(raw, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), raw) };
        }
        void StoreDword(uint address, uint value)
        {
            byte[] raw = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), raw) };
        }
        int Random(short operand)
        {
            NativeBattleWaterVectorRandom sampled = NativeBattleWaterVectorCommands.RandomB65740(operand, frame.Random);
            frame = frame with { Random = sampled.Random };
            return sampled.Value;
        }
    }
}
