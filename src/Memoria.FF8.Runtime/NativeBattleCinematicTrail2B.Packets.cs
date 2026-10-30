using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicTrail2B
{
    public static int JoinAngle(int first, int second, int previous)
    {
        first &= 4095;
        second &= 4095;
        int low = Math.Min(first, second);
        int high = Math.Max(first, second);
        int difference = high - low;
        int angle = difference < 2048 ? low + difference / 2 : high + (4096 - difference) / 2;
        int separation = Math.Abs(previous - (angle & 4095));
        if (separation >= 1024 && separation < 3072)
        {
            angle += 2048;
        }
        return angle & 4095;
    }

    private static NativeBattleWaterExecutionFrame DrawRibbon(NativeBattleWaterExecutionFrame frame, uint descriptor, uint start, int count, int v, NativeBattleCinematicTrail2BServices services)
    {
        frame = frame.WriteTemporaryDword(0xF0, start);
        int direction = Direction(start);
        int edge = unchecked(direction + 1024);
        Expand(start, edge);
        uint current = unchecked(start + 32);
        for (int index = 1; index < count - 1; index++)
        {
            int opposite = unchecked(direction + 2048);
            direction = Direction(current);
            edge = JoinAngle(direction, opposite, edge);
            Expand(current, edge);
            current = unchecked(current + 32);
        }
        Expand(current, edge);
        uint output = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C));
        frame = frame.WriteTemporaryDword(0x68, unchecked((uint)Word(unchecked(descriptor + 8))));
        frame = frame.WriteTemporaryDword(0x6C, unchecked((uint)Word(unchecked(descriptor + 10))));
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)(Byte(unchecked(descriptor + 0x1A)) - 1)));
        frame = frame.WriteTemporaryDword(0xF8, Byte(unchecked(descriptor + 0x1B)));
        v &= 255;
        current = frame.ReadTemporaryDword(0xF0);
        for (int segment = 0; segment < count - 1; segment++)
        {
            uint next = unchecked(current + 32);
            uint firstColor = Dword(unchecked(current + 0x14));
            uint secondColor = Dword(unchecked(next + 0x14));
            bool flat = firstColor == secondColor;
            int length = flat ? 40 : 52;
            byte[] data = new byte[length];
            bool[] written = new bool[length];
            PutByte(3, flat ? (byte)9 : (byte)12);
            int secondColorOffset = flat ? 4 : 0x1C;
            if (flat)
            {
                PutDword(4, firstColor | 0x2C000000);
            }
            else
            {
                PutDword(4, firstColor | 0x3C000000);
                PutDword(0x10, firstColor | 0x3C000000);
                PutDword(secondColorOffset, secondColor);
                PutDword(0x28, secondColor);
            }
            int[] xyOffsets = flat ? [8, 16, 24, 32] : [8, 20, 32, 44];
            PutDword(xyOffsets[0], Dword(unchecked(current + 8)));
            PutDword(xyOffsets[1], Dword(unchecked(current + 12)));
            PutDword(xyOffsets[2], Dword(unchecked(next + 8)));
            PutDword(xyOffsets[3], Dword(unchecked(next + 12)));
            int[] uvOffsets = flat ? [12, 20, 28, 36] : [12, 24, 36, 48];
            PutByte(uvOffsets[0], 1);
            PutByte(uvOffsets[2], 1);
            PutByte(uvOffsets[1], unchecked((byte)frame.ReadTemporaryDword(0xF4)));
            PutByte(uvOffsets[3], unchecked((byte)frame.ReadTemporaryDword(0xF4)));
            PutByte(uvOffsets[0] + 1, unchecked((byte)v));
            PutByte(uvOffsets[1] + 1, unchecked((byte)v));
            v = unchecked(v + (int)frame.ReadTemporaryDword(0xF8));
            byte endpoint = v >= 256 ? (byte)255 : unchecked((byte)v);
            PutByte(uvOffsets[2] + 1, endpoint);
            PutByte(uvOffsets[3] + 1, endpoint);
            if (v >= 256)
            {
                v = 0;
            }
            PutWord(14, unchecked((ushort)frame.ReadTemporaryDword(0x6C)));
            PutWord(flat ? 22 : 26, unchecked((ushort)frame.ReadTemporaryDword(0x68)));
            int depth = (Word(unchecked(current + 4)) + Word(unchecked(next + 4))) / 2;
            uint ordering = unchecked(frame.ReadTemporaryDword(0x14C) + ((uint)depth & 0xFFFFFFFC));
            ImmutableArray<NativeBattleGfPacketByteState> mask = written.Select(value => value ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(0, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
            if (services.PilotIdentity is { } identity && services.PilotSamples is { } samples && samples.TryGetValue(current, out NativeBattleCinematicTrail2BSample? first) && samples.TryGetValue(next, out NativeBattleCinematicTrail2BSample? second))
            {
                System.Numerics.Vector2 Offset(uint endpoint, uint center) => new(Word(endpoint) - Word(center), Word(endpoint + 2) - Word(center + 2));
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked((int)first.Source) }, first.World, first.World, second.World, second.World, 4) { ScreenOffsets = [Offset(current + 8, current), Offset(current + 12, current), Offset(next + 8, next), Offset(next + 12, next)], DepthDivisor = 4 } };
                if (identity.Effect == 258)
                {
                    primitive = primitive with { PilotGeometry = primitive.PilotGeometry! with { UseSourceDepths = true, SourceDepths = System.Numerics.Vector4.Zero } };
                }
            }
            frame = services.Append(frame, output, ordering, new([new(0, data.ToImmutableArray(), mask, primitive)], length));
            output = unchecked(output + (uint)length);
            current = unchecked(current + 32);
            frame = frame.WriteTemporaryDword(0x70, unchecked(frame.ReadTemporaryDword(0x70) + 1));

            void PutByte(int offset, byte value)
            {
                data[offset] = value;
                written[offset] = true;
            }

            void PutWord(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
                Array.Fill(written, true, offset, 2);
            }

            void PutDword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                Array.Fill(written, true, offset, 4);
            }
        }
        return WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);

        int Direction(uint address)
        {
            int angle;
            (frame, angle) = NativeBattleCinematicRibbonBasis.Angle(frame, Word(unchecked(address + 2)), Word(address), Word(unchecked(address + 34)), Word(unchecked(address + 32)));
            return angle;
        }

        void Expand(uint address, int angle)
        {
            int width = Word(unchecked(address + 0x10));
            int x = unchecked(NativeBattleCameraMath.Cosine(angle) * width) >> 12;
            int y = unchecked(NativeBattleCameraMath.Sine(angle) * width) >> 12;
            short centerX = Word(address);
            short centerY = Word(unchecked(address + 2));
            StoreWord(unchecked(address + 8), unchecked((ushort)(centerX + x)));
            StoreWord(unchecked(address + 12), unchecked((ushort)(centerX - x)));
            StoreWord(unchecked(address + 10), unchecked((ushort)(centerY + y)));
            StoreWord(unchecked(address + 14), unchecked((ushort)(centerY - y)));
        }

        byte Byte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        void StoreWord(uint address, ushort value) => frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
    }
}
