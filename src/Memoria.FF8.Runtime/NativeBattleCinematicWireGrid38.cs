using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicWireGrid38Services(Func<NativeBattleWaterExecutionFrame, int, uint> ResolveShape, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> ScaleBasis, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicGridProjection> Project, Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleCinematicGridValue> ScaleColor, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>A closed angular grid with independent axial and circumferential gradient lines.</summary>
public static class NativeBattleCinematicWireGrid38
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicWireGrid38Services services, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        Dictionary<uint, System.Numerics.Vector3>? pilotWorld = pilotIdentity is null ? null : [];
        uint payload = SlotDword(0xB8);
        uint state = SlotDword(0xBC);
        if (state == 0)
        {
            (frame, state) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 0x4C);
            frame = WriteSlotDword(frame, 0xBC, state);
            StoreDword(state, unchecked(payload + (uint)SignedWord(payload)));
            StoreWord(state + 4, 1);
            uint shape = services.ResolveShape(frame, Word(payload + 4));
            uint vertices = unchecked(shape + Dword(shape + 0x14));
            for (int index = 0; index < 17; index++)
            {
                uint source = unchecked(vertices + (uint)(Word(payload + 10 + (uint)(index % 16 * 2)) * 8));
                StoreWord(state + 8 + (uint)(index * 2), Word(source));
                StoreWord(state + 0x2A + (uint)(index * 2), Word(source + 4));
            }
        }
        frame = frame.WriteTemporaryDword(0x80, payload).WriteTemporaryDword(0x84, state);
        int period = unchecked((short)SlotWord(frame, 0x8E));
        int phase = SlotWord(frame, 0x96) & 0x7FFF;
        frame = frame.WriteTemporaryDword(0x70, unchecked((uint)period)).WriteTemporaryDword(0x74, unchecked((uint)phase));
        uint work = ManagerDword(0x74);
        frame = frame.WriteTemporaryDword(0x64, work).WriteTemporaryDword(0x60, work);
        int threshold = unchecked((short)SlotWord(frame, 0x94));
        int mask = Word(payload + 8) - 1;
        int quotient = phase / period;
        int rows = Word(payload + 6);
        for (int row = 0; row <= rows; row++)
        {
            for (int column = 0; column < 17; column++)
            {
                byte color = column > threshold ? (byte)0x80 : unchecked((byte)((column - quotient + threshold) & (byte)mask));
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(work++, 1), [color]) };
            }
        }
        work = unchecked((work + 4) & 0xFFFFFFFC);
        frame = frame.WriteTemporaryDword(0x68, work);
        int remainder = phase % period;
        frame = frame.WriteTemporaryWord(0xD2, unchecked((ushort)(remainder - unchecked(rows * period) / 2)));
        int fraction = unchecked(remainder << 12) / period;
        int delta;
        int acceleration;
        int radius = unchecked((short)SlotWord(frame, 0x8C)) << 12;
        int mode = Word(payload + 2);
        if (mode == 0)
        {
            delta = unchecked((unchecked((short)SlotWord(frame, 0x90)) << 12) - radius) / rows;
            radius = unchecked(radius + (unchecked(fraction * delta) >> 12));
            acceleration = 0;
        }
        else if (mode == 1)
        {
            delta = unchecked((short)SlotWord(frame, 0x90)) << 4;
            acceleration = unchecked((short)SlotWord(frame, 0x98)) << 4;
        }
        else
        {
            throw new NotSupportedException($"Native angular grid mode {mode} requires its retained caller-local contract.");
        }
        frame = frame.WriteTemporaryDword(0xF8, unchecked((uint)radius));
        frame = services.PrepareBasis(frame);
        frame = frame.WriteTemporaryDword(0xC4, 4096);
        for (int row = 0; row <= rows; row++)
        {
            int scale = unchecked((int)frame.ReadTemporaryDword(0xF8)) >> 8;
            frame = frame.WriteTemporaryDword(0xC0, unchecked((uint)scale)).WriteTemporaryDword(0xC8, unchecked((uint)scale));
            frame = services.ScaleBasis(frame);
            for (int column = 0; column < 17; column++)
            {
                frame = frame.WriteTemporaryWord(0xD0, Word(state + 8 + (uint)(column * 2))).WriteTemporaryWord(0xD4, Word(state + 0x2A + (uint)(column * 2)));
                NativeBattleCinematicGridProjection projected = services.Project(frame, work);
                frame = projected.Frame;
                if (pilotWorld is not null && projected.PilotWorld is { } world)
                {
                    pilotWorld[work] = world;
                }
                StoreWord(work + 4, unchecked((ushort)projected.QuarterDepth));
                work = unchecked(work + 8);
            }
            delta = unchecked(delta + acceleration);
            frame = frame.WriteTemporaryDword(0xF8, unchecked(frame.ReadTemporaryDword(0xF8) + (uint)delta));
            frame = frame.WriteTemporaryWord(0xD2, unchecked((ushort)(frame.ReadTemporaryWord(0xD2) + period)));
        }
        frame = frame.WriteTemporaryDword(0x6C, work);
        int fade = unchecked(rows * 64 - (unchecked(remainder * (65536 / period)) >> 10));
        for (int row = 0; row <= rows; row++)
        {
            if (fade < 0)
            {
                fade = 0;
            }
            StoreDword(work, unchecked((uint)fade));
            work = unchecked(work + 4);
            fade = unchecked(fade - 64);
        }
        uint output = ManagerDword(0x7C);
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)mask));
        DrawLines(false);
        frame = frame.WriteTemporaryDword(0xF4, 0x88);
        DrawLines(true);
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        void DrawLines(bool vertical)
        {
            uint colors = frame.ReadTemporaryDword(0x64);
            uint vertices = frame.ReadTemporaryDword(0x68);
            uint fades = frame.ReadTemporaryDword(0x6C);
            uint palette = Dword(state);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < 16; column++)
                {
                    int index = unchecked((sbyte)frame.Arena.Read(ArenaOffset(colors + (uint)(row * 17 + column), 1), 1)[0]);
                    if (index < 0)
                    {
                        continue;
                    }
                    int firstFade = unchecked((int)Dword(fades + (uint)(row * 4)));
                    uint firstColor = Shade(Dword(palette + (uint)(index * 4)), firstFade) | 0x52000000;
                    int secondFade = vertical ? unchecked((int)Dword(fades + (uint)((row + 1) * 4))) : firstFade;
                    uint secondColor = Shade(Dword(palette + (uint)(((index + 1) & mask) * 4)), secondFade);
                    uint first = vertices + (uint)((row * 17 + column) * 8);
                    uint second = first + (vertical ? 0x88u : 8u);
                    uint depth1 = Word(first + 4);
                    uint depth2 = Word(first + 12);
                    byte[] data = new byte[28];
                    data[3] = 6;
                    Put(4, 0xE1000220);
                    Put(8, 0);
                    Put(12, firstColor);
                    Put(16, Dword(first));
                    Put(20, secondColor);
                    Put(24, Dword(second));
                    ImmutableArray<NativeBattleGfPacketByteState> known = Enumerable.Range(0, 28).Select(index => index >= 3 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                    ImmutableArray<uint> words = Enumerable.Range(0, 7).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                    NativeBattleGeometryPrimitive primitive = new(0, words, [(int)depth1, (int)depth2, 0, 0]) { PhysicalWriteMask = known };
                    if (pilotIdentity is { } identity && pilotWorld is not null && pilotWorld.TryGetValue(first, out System.Numerics.Vector3 a) && pilotWorld.TryGetValue(second, out System.Numerics.Vector3 b))
                    {
                        primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked((vertical ? 1 << 24 : 0) + row * 65537 + column) }, a, b, default, default, 2) { UseSourceDepths = true, SourceDepths = new(depth1, depth2, 0, 0) } };
                    }
                    uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + ((depth1 + depth2) / 2 & 0xFFFFFFFC));
                    frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), known, primitive)], 28));
                    output = unchecked(output + 28);

                    void Put(int offset, uint value)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                    }
                }
            }
        }

        uint Shade(uint color, int factor)
        {
            if (factor >= 256)
            {
                return color;
            }
            NativeBattleCinematicGridValue scaled = services.ScaleColor(frame, color, unchecked((short)factor));
            frame = scaled.Frame;
            return scaled.Value;
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        uint ManagerDword(uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));

        void StoreWord(uint address, ushort value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
        }

        void StoreDword(uint address, uint value)
        {
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
        }
    }
}
