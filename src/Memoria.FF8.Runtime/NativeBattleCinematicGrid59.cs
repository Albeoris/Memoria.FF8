using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicGrid59Services(Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> PrepareBasis, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleCinematicGridProjection> Project, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, uint, uint, NativeBattleWaterExecutionFrame> TransformCurve, Func<NativeBattleWaterExecutionFrame, uint, short, NativeBattleCinematicGridValue> ScaleColor, Func<uint, byte> ReadStaticByte, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> Append);

/// <summary>Retained activation ages and the native planar lattice of the cinematic grid.</summary>
public static partial class NativeBattleCinematicGrid59
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGrid59Services services)
    {
        uint payload = SlotDword(frame, 0xB8);
        uint state = SlotDword(frame, 0xBC);
        if (state == 0)
        {
            (frame, state) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 12);
            frame = WriteSlotDword(frame, 0xBC, state);
            frame = StoreDword(frame, state, unchecked(payload + (uint)(short)Word(frame, payload)));
            uint ages;
            (frame, ages) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 128);
            frame = StoreDword(frame, state + 4, ages);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(ages, 128), Enumerable.Repeat((byte)255, 128).ToArray()) };
        }
        frame = frame.WriteTemporaryDword(0x60, SlotDword(frame, 0xCC) | 0x40000000).WriteTemporaryDword(0x64, SlotDword(frame, 0xCC) | 0x50000000);
        frame = frame.WriteTemporaryDword(0x68, Word(frame, payload + 4)).WriteTemporaryDword(0x6C, Word(frame, payload + 6));
        ushort mode = SlotWord(frame, 0x4A);
        if (mode == 0)
        {
            frame = DrawPlane(frame, state, payload, services);
        }
        if ((mode & 3) != 0)
        {
            frame = DrawCylinder(frame, payload, services);
        }
        return frame;
    }

    private static NativeBattleWaterExecutionFrame DrawPlane(NativeBattleWaterExecutionFrame frame, uint state, uint payload, NativeBattleCinematicGrid59Services services)
    {
        uint ages = Dword(frame, state + 4);
        Activate(0x10, Math.Min((int)unchecked((short)SlotWord(frame, 0x98)), 16));
        Activate(0x60, Math.Min((int)unchecked((short)SlotWord(frame, 0x94)), 32));
        for (uint index = 0; index < 48; index++)
        {
            Age(ages + index);
            Age(ages + 0x40 + index);
        }
        frame = services.PrepareBasis(frame);
        uint work = ManagerDword(frame, 0x74);
        uint cursor = work;
        int firstSpacing = Word(frame, payload + 4);
        int secondSpacing = Word(frame, payload + 6);
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)(-firstSpacing << 5)));
        frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(-16 * secondSpacing))).WriteTemporaryWord(0xC2, 0);
        for (int row = 0; row <= 32; row++)
        {
            frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)((row - 15) * secondSpacing)));
            int coordinate = unchecked(-firstSpacing << 5);
            for (int column = 0; column < 13; column++)
            {
                frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)coordinate));
                Project(cursor);
                cursor = unchecked(cursor + 8);
                coordinate = unchecked(coordinate + (firstSpacing << 2));
            }
        }
        uint output = ManagerDword(frame, 0x7C);
        uint vertices = work;
        for (uint row = 0; row < 32; row++)
        {
            int age = unchecked((sbyte)Byte(frame, ages + row));
            if (age != -1)
            {
                uint color = Shade(frame.ReadTemporaryDword(0x60), services.ReadStaticByte(0x1870FD8 + row) * age);
                frame = frame.WriteTemporaryDword(0xF4, color);
                for (int column = 0; column < 12; column++)
                {
                    Emit(vertices, vertices + 8, color, null);
                    vertices = unchecked(vertices + 8);
                }
                vertices = unchecked(vertices + 8);
            }
            else
            {
                vertices = unchecked(vertices + 0x68);
            }
        }
        frame = frame.WriteTemporaryDword(0xF0, unchecked((uint)(-firstSpacing << 5)));
        frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(-16 * secondSpacing))).WriteTemporaryWord(0xC2, 0);
        cursor = work;
        for (int row = 0; row <= 48; row++)
        {
            frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)((row - 31) * firstSpacing)));
            int coordinate = unchecked(-secondSpacing << 4);
            for (int column = 0; column < 9; column++)
            {
                frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)coordinate));
                Project(cursor);
                cursor = unchecked(cursor + 8);
                coordinate = unchecked(coordinate + (secondSpacing << 2));
            }
        }
        frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);
        vertices = work;
        for (uint row = 0; row < 48; row++)
        {
            int age = unchecked((sbyte)Byte(frame, ages + 0x40 + row));
            if (age != -1)
            {
                uint constantColor = Shade(frame.ReadTemporaryDword(0x60), age << 4);
                frame = frame.WriteTemporaryDword(0xF4, constantColor);
                for (uint palette = 0x1870FDB; palette < 0x1870FFB; palette += 4)
                {
                    int first = services.ReadStaticByte(palette - 3);
                    int second = services.ReadStaticByte(palette);
                    if (first == second)
                    {
                        age = unchecked((int)Emit(vertices, vertices + 8, constantColor, null));
                    }
                    else
                    {
                        uint firstColor = Shade(frame.ReadTemporaryDword(0x64), unchecked(first * age));
                        uint secondColor = Shade(frame.ReadTemporaryDword(0x64), unchecked(second * age));
                        age = unchecked((int)Emit(vertices, vertices + 8, firstColor, secondColor));
                    }
                    vertices = unchecked(vertices + 8);
                }
                vertices = unchecked(vertices + 8);
            }
            else
            {
                vertices = unchecked(vertices + 0x48);
            }
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        void Activate(uint center, int count)
        {
            for (int index = 1; index <= count; index++)
            {
                Clear(unchecked(ages + center + (uint)index));
                Clear(unchecked(ages + center - (uint)index));
            }
            if (count >= 1)
            {
                Clear(ages + center);
            }
        }

        void Clear(uint address)
        {
            if (Byte(frame, address) == 255)
            {
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [0]) };
            }
        }

        void Age(uint address)
        {
            int value = unchecked((sbyte)Byte(frame, address));
            if (value >= 0)
            {
                value++;
            }
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 1), [unchecked((byte)Math.Min(value, 16))]) };
        }

        void Project(uint address)
        {
            NativeBattleCinematicGridProjection result = services.Project(frame, address);
            frame = result.Frame;
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address + 4, 2), BitConverter.GetBytes(unchecked((ushort)result.QuarterDepth))) };
        }

        uint Shade(uint color, int factor)
        {
            NativeBattleCinematicGridValue result = services.ScaleColor(frame, color, unchecked((short)factor));
            frame = result.Frame;
            return result.Value;
        }

        uint Emit(uint first, uint second, uint firstColor, uint? secondColor)
        {
            var result = AppendLine(frame, output, first, second, firstColor, secondColor, services.Append);
            frame = result.Frame;
            output = result.Output;
            return result.Offset;
        }
    }

    internal static (NativeBattleWaterExecutionFrame Frame, uint Output, uint Offset) AppendLine(NativeBattleWaterExecutionFrame frame, uint output, uint first, uint second, uint firstColor, uint? secondColor, Func<NativeBattleWaterExecutionFrame, uint, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> append, uint fixedOrdering = 0)
    {
        uint firstDepth = Word(frame, first + 4);
        uint secondDepth = Word(frame, second + 4);
        byte[] data = new byte[secondColor.HasValue ? 20 : 16];
        data[3] = secondColor.HasValue ? (byte)4 : (byte)3;
        Put(4, firstColor);
        Put(8, Dword(frame, first));
        if (secondColor is uint color)
        {
            Put(12, color);
            Put(16, Dword(frame, second));
        }
        else
        {
            Put(12, Dword(frame, second));
        }
        ImmutableArray<NativeBattleGfPacketByteState> known = Enumerable.Range(0, data.Length).Select(index => index < 3 ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known).ToImmutableArray();
        ImmutableArray<uint> words = Enumerable.Range(0, data.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(0, words, [(int)firstDepth, (int)secondDepth, 0, 0]) { PhysicalWriteMask = known };
        uint average = unchecked((firstDepth + secondDepth) / 2) >> 2;
        uint offset = average & 0xFFFFFFFC;
        uint ordering = fixedOrdering != 0 ? fixedOrdering : unchecked(frame.ReadTemporaryDword(0x14C) + offset);
        frame = append(frame, output, ordering, new([new(0, data.ToImmutableArray(), known, primitive)], data.Length));
        output = unchecked(output + (uint)data.Length);
        return (frame, output, offset);

        void Put(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
        }
    }


    private static uint SlotDword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
    private static uint ManagerDword(NativeBattleWaterExecutionFrame frame, uint offset) => CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + offset));
    private static byte Byte(NativeBattleWaterExecutionFrame frame, uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    private static NativeBattleWaterExecutionFrame StoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
}
