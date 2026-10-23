using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBreath129Geometry
{
    public static NativeBattleBreath129Frame DrawLinked6B6450(NativeBattleBreath129Frame frame, uint firstPoint, short phase, int count, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        if (count is < 2 or > 256)
        {
            throw new InvalidDataException("Breath linked drawing requires its bounded native point count.");
        }
        uint point = firstPoint;
        HashSet<uint> visited = [];
        while (point != 0)
        {
            uint bank = frame.State.ReadDword(0x252115C) + 0x19C0;
            if (point < bank || point - bank >= 256 * 28 || (point - bank) % 28 != 0 || !visited.Add(point))
            {
                throw new InvalidDataException("Breath linked drawing leaves its actual point bank or reaches a cycle.");
            }
            int index = Word(frame, point);
            if ((uint)index >= 88)
            {
                throw new InvalidDataException("Breath linked point leaves its original indexed source.");
            }
            NativeBattleEffectArena source = frame.State.ReadView(0x1228F0Cu + (uint)(index * 8), 8);
            frame = LoadRotation(frame, frame.State.ReadView(0x2521138, 20));
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = registers with { Geometry = registers.Geometry with { Translation = [0, 0, 0] } };
            registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(source), W(source));
            registers = NativeBattleGeometry.Transform(registers);
            NativeBattleVector transformed = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
            frame = Publish(frame, registers);
            int w = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(source.Read(6, 2));
            int radius = resources.TrigonometricWords8192[w & 0xFFE] >> 4;
            int selected = ((w + phase) & 0xFFF) * 2;
            transformed = transformed with { X = unchecked((short)(transformed.X + (unchecked(resources.TrigonometricWords8192[selected + 1] * radius) >> 12))), Y = unchecked((short)(transformed.Y - (unchecked(resources.TrigonometricWords8192[selected] * radius) >> 12))) };
            frame = Load(frame, Packed(frame.State.ReadView(0x2521110, 32)));
            frame = Project(frame, Point(transformed), inputs);
            registers = Registers(frame);
            frame = Dword(frame, point + 4, registers.Geometry.Sxy[2]!.Value);
            frame = Dword(frame, point + 8, unchecked((uint)(unchecked((int)registers.Geometry.Sz[3]!.Value) >> 2)));
            if ((registers.Colors.Flags!.Value & 0x60000) != 0)
            {
                frame = Word(frame, point + 8, 0);
            }
            point = Dword(frame, point + 24);
        }
        if (visited.Count != count)
        {
            throw new InvalidDataException("Breath linked count differs from its physical chain.");
        }
        return EdgesAndPackets(frame, firstPoint, count, false, resources.DistanceTable, inputs);
    }

    public static NativeBattleBreath129Frame DrawFibre6B6CC0(NativeBattleBreath129Frame frame, uint address, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs, NativeBattleBreath129FibreStack? stack = null)
    {
        frame = Load(frame, inputs.ReadView(frame));
        for (int index = 0; index < 16; index++)
        {
            uint point = checked(address + (uint)(index * 16));
            frame = Project(frame, Get(frame, point, 8), inputs);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            frame = Dword(frame, point, registers.Geometry.Sxy[2]!.Value);
            frame = Word(frame, point + 4, unchecked((int)registers.Geometry.Sz[3]!.Value) >> 2);
        }
        return EdgesAndPackets(frame, address, 16, true, resources.DistanceTable, inputs, stack);
    }

    private static NativeBattleBreath129Frame EdgesAndPackets(NativeBattleBreath129Frame frame, uint first, int count, bool fibre, ImmutableArray<byte> distances, NativeBattleBreath129Inputs inputs, NativeBattleBreath129FibreStack? stack = null)
    {
        if (distances.IsDefault || distances.Length != 16384)
        {
            throw new InvalidDataException("Breath ribbon requires its original distance table.");
        }
        uint Next(NativeBattleBreath129Frame current, uint point) => fibre ? checked(point + 16) : Dword(current, point + 24);
        int screen = fibre ? 0 : 4;
        int depth = fibre ? 4 : 8;
        int right = fibre ? 8 : 12;
        int width = fibre ? 6 : 2;
        short? previousX = fibre && stack is not null ? unchecked((short)stack.CrtSavedEdi) : null;
        short? previousY = fibre && stack is not null ? unchecked((short)(stack.CrtSavedEdi >> 16)) : null;
        uint at = first;
        for (int index = 0; index < count; index++)
        {
            uint next = index == count - 1 ? 0 : Next(frame, at);
            if (Word(frame, at + (uint)depth) >= 0 && (next == 0 || Word(frame, next + (uint)depth) >= 0))
            {
                int offsetX = 0;
                int offsetY = 0;
                bool valid = true;
                if (next != 0)
                {
                    int dx = Word(frame, next + (uint)screen) - Word(frame, at + (uint)screen);
                    int dy = Word(frame, at + (uint)screen + 2) - Word(frame, next + (uint)screen + 2);
                    int distance = distances[Math.Min(Math.Abs(dy), 127) * 128 + Math.Min(Math.Abs(dx), 127)];
                    NativeBattleRibbonDirectionStep direction = NativeBattleRibbonDirection.Resolve(dx, dy, distance, index != 0, previousX, previousY);
                    previousX = direction.PreviousX;
                    previousY = direction.PreviousY;
                    valid = direction.Valid;
                    if (valid)
                    {
                        int projectedWidth = unchecked(Word(frame, next + (uint)width) << 5) / (Word(frame, at + (uint)depth) + 1024);
                        offsetX = unchecked(projectedWidth * direction.X) >> 12;
                        offsetY = unchecked(projectedWidth * direction.Y) >> 12;
                    }
                    else
                    {
                        frame = Word(frame, at + (uint)depth, -1);
                    }
                }
                if (valid)
                {
                    short x = Word(frame, at + (uint)screen);
                    short y = Word(frame, at + (uint)screen + 2);
                    frame = Word(Word(Word(Word(frame, at + (uint)screen, x - offsetX), at + (uint)right, x + offsetX), at + (uint)screen + 2, y - offsetY), at + (uint)right + 2, y + offsetY);
                }
            }
            at = next;
        }
        at = first;
        for (int index = 0; index < count - 1; index++)
        {
            uint next = Next(frame, at);
            if (Word(frame, at + (uint)depth) > 0 && Word(frame, next + (uint)depth) > 0)
            {
                uint cursor = frame.State.ReadDword(0x252117C);
                NativeBattleEffectArena packet = Get(frame, cursor, 52).Write(0, BitConverter.GetBytes(0x0C000000u)).Write(8, Get(frame, at + (uint)screen, 4).Read(0, 4)).Write(20, Get(frame, at + (uint)right, 4).Read(0, 4)).Write(12, BitConverter.GetBytes(0x3CD4E048u)).Write(24, BitConverter.GetBytes(0x00B8E078u)).Write(36, BitConverter.GetBytes((ushort)0xE048)).Write(48, BitConverter.GetBytes((ushort)0xE078)).Write(32, Get(frame, next + (uint)screen, 4).Read(0, 4)).Write(44, Get(frame, next + (uint)right, 4).Read(0, 4));
                int firstColor = Word(frame, at + (uint)(fibre ? 14 : 20));
                int secondColor = Word(frame, next + (uint)(fibre ? 14 : 20));
                if (!fibre)
                {
                    firstColor >>= 4;
                    secondColor >>= 4;
                }
                static uint Gray(int value) => unchecked((uint)(((value << 8) | value) << 8 | value));
                packet = packet.Write(4, BitConverter.GetBytes(Gray(firstColor))).Write(16, BitConverter.GetBytes(Gray(firstColor))).Write(7, [0x3E]).Write(28, BitConverter.GetBytes(Gray(secondColor))).Write(40, BitConverter.GetBytes(Gray(secondColor)));
                int bucket = 17 + ((Word(frame, at + (uint)depth) + Word(frame, next + (uint)depth)) >> (fibre ? 4 : 5));
                frame = Append(frame, packet, 52, bucket, inputs);
            }
            at = next;
        }
        return frame;
    }
}
