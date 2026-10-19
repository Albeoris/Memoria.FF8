using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicGrid59
{
    private static NativeBattleWaterExecutionFrame DrawCylinder(NativeBattleWaterExecutionFrame frame, uint payload, NativeBattleCinematicGrid59Services services)
    {
        uint work = ManagerDword(frame, 0x74);
        int radius = Word(frame, payload + 4);
        int spacing = Word(frame, payload + 6);
        int remainder = unchecked((short)SlotWord(frame, 0x96)) % radius;
        frame = StoreDword(frame, work, 0);
        frame = StoreWord(frame, work + 4, 0);
        frame = frame.WriteTemporaryDword(0xB0, 0).WriteTemporaryWord(0xB4, unchecked((ushort)-remainder));
        frame = frame.WriteTemporaryDword(0xC0, 0).WriteTemporaryWord(0xC4, unchecked((ushort)-radius));
        frame = frame.WriteTemporaryDword(0xD0, 0).WriteTemporaryWord(0xD4, 0);
        int x = unchecked((int)CoreDword(frame, 0x27971F8));
        int y = unchecked((int)CoreDword(frame, 0x27971FC));
        int z = unchecked((int)CoreDword(frame, 0x2797200));
        uint curve = unchecked(work + 16);
        for (int sample = 0; sample < 33; sample++)
        {
            frame = frame.WriteTemporaryWord(0xD0, unchecked((ushort)(frame.ReadTemporaryWord(0xD0) + SlotWord(frame, 0x8E))));
            NativeBattleVector angles = new(unchecked((short)frame.ReadTemporaryWord(0xD0)), unchecked((short)frame.ReadTemporaryWord(0xD2)), unchecked((short)frame.ReadTemporaryWord(0xD4)));
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(angles) with { X = x, Y = y, Z = z };
            for (int index = 0; index < 9; index++)
            {
                frame = frame.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)rotation.Rotation[index]));
            }
            frame = services.TransformCurve(frame, rotation, frame.TemporaryFrame.Address + 0xC0, curve);
            frame = services.TransformCurve(frame, rotation, frame.TemporaryFrame.Address + 0xB0, curve - 8);
            x = unchecked((short)Word(frame, curve));
            y = unchecked((short)Word(frame, curve + 2));
            z = unchecked((short)Word(frame, curve + 4));
            curve = unchecked(curve + 16);
        }
        frame = frame.WriteTemporaryDword(0x94, unchecked((uint)(int)(short)Word(frame, curve - 0x1E)));
        frame = frame.WriteTemporaryDword(0x90, curve);
        frame = services.PrepareBasis(frame);
        frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(-16 * spacing)));
        uint cursor = curve;
        for (int row = 0; row < 33; row++)
        {
            frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(frame.ReadTemporaryWord(0xC0) + spacing)));
            uint source = work;
            for (int column = 0; column < 33; column++)
            {
                frame = StoreWord(frame, source, frame.ReadTemporaryWord(0xC0));
                frame = frame.WriteTemporaryWord(0xC2, Word(frame, source + 2)).WriteTemporaryWord(0xC4, Word(frame, source + 4));
                Project(cursor);
                source = unchecked(source + 16);
                cursor = unchecked(cursor + 8);
            }
        }
        uint output = ManagerDword(frame, 0x7C);
        DrawRows(curve, 32, 32, true);
        PublishOutput();
        frame = services.PrepareBasis(frame);
        cursor = curve;
        uint profile = work + 8;
        for (int row = 0; row < 33; row++)
        {
            for (int column = 0; column < 9; column++)
            {
                frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(-16 * spacing + column * (spacing << 2))));
                frame = StoreWord(frame, profile, frame.ReadTemporaryWord(0xC0));
                frame = frame.WriteTemporaryWord(0xC2, Word(frame, profile + 2)).WriteTemporaryWord(0xC4, Word(frame, profile + 4));
                Project(cursor);
                cursor = unchecked(cursor + 8);
            }
            profile = unchecked(profile + 16);
        }
        DrawRows(curve, 32, 8, false);
        PublishOutput();
        if ((SlotWord(frame, 0x4A) & 1) != 0)
        {
            frame = services.PrepareBasis(frame);
            DrawClosure(0, 0, radius - remainder);
        }
        if ((SlotWord(frame, 0x4A) & 2) != 0)
        {
            DrawClosure(unchecked((ushort)frame.ReadTemporaryDword(0x94)), radius, radius + remainder);
        }
        return frame;

        void DrawClosure(ushort level, int firstRadius, int capRadius)
        {
            frame = frame.WriteTemporaryWord(0xC2, level);
            cursor = work;
            for (int row = 0; row <= 32; row++)
            {
                frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)((row - 15) * spacing)));
                for (int column = 0; column < 5; column++)
                {
                    frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(firstRadius + column * (radius << 2))));
                    Project(cursor);
                    cursor = unchecked(cursor + 8);
                }
            }
            DrawRows(work, 32, 4, true);
            PublishOutput();
            frame = frame.WriteTemporaryWord(0xC2, level).WriteTemporaryWord(0xC4, unchecked((ushort)capRadius));
            cursor = work;
            for (int row = 0; row < 17; row++)
            {
                for (int column = 0; column < 9; column++)
                {
                    frame = frame.WriteTemporaryWord(0xC0, unchecked((ushort)(-16 * spacing + column * (spacing << 2))));
                    Project(cursor);
                    cursor = unchecked(cursor + 8);
                }
                frame = frame.WriteTemporaryWord(0xC4, unchecked((ushort)(frame.ReadTemporaryWord(0xC4) + radius)));
            }
            DrawRows(work, 16, 8, false);
            PublishOutput();
        }

        void DrawRows(uint vertices, int rows, int columns, bool flat)
        {
            for (int row = 0; row < rows; row++)
            {
                uint constantColor = 0;
                if (flat)
                {
                    constantColor = Shade(frame.ReadTemporaryDword(0x60), services.ReadStaticByte(0x1870FD8 + (uint)row) << 4);
                    frame = frame.WriteTemporaryDword(0xF4, constantColor);
                }
                for (int column = 0; column < columns; column++)
                {
                    uint firstColor = constantColor;
                    uint? secondColor = null;
                    if (!flat)
                    {
                        uint palette = 0x1870FD8 + (uint)(column * 4);
                        int first = services.ReadStaticByte(palette);
                        int second = services.ReadStaticByte(palette + 3);
                        if (first == second)
                        {
                            firstColor = frame.ReadTemporaryDword(0x60);
                        }
                        else
                        {
                            firstColor = Shade(frame.ReadTemporaryDword(0x64), first << 4);
                            secondColor = Shade(frame.ReadTemporaryDword(0x64), second << 4);
                        }
                    }
                    var packet = AppendLine(frame, output, vertices, vertices + 8, firstColor, secondColor, services.Append);
                    frame = packet.Frame;
                    output = packet.Output;
                    vertices = unchecked(vertices + 8);
                }
                vertices = unchecked(vertices + 8);
            }
        }

        void Project(uint address)
        {
            NativeBattleCinematicGridProjection projected = services.Project(frame, address);
            frame = StoreWord(projected.Frame, address + 4, unchecked((ushort)projected.QuarterDepth));
        }

        uint Shade(uint command, int factor)
        {
            NativeBattleCinematicGridValue result = services.ScaleColor(frame, command, unchecked((short)factor));
            frame = result.Frame;
            return result.Value;
        }

        void PublishOutput()
        {
            frame = WriteCoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x7C), output);
        }
    }

    private static NativeBattleWaterExecutionFrame StoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
}
