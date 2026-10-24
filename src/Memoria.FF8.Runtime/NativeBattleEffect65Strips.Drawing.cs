using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Strips
{
    public static NativeBattleEffect65Frame Draw6E3990(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        (frame, uint temporaryAddress) = Allocate(frame, 400);
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 400).MarkUnknown(0, 4).Write(0x2C, BitConverter.GetBytes(0u)).Write(0x30, BitConverter.GetBytes(0x80F0F0u)).Write(0x34, BitConverter.GetBytes(0x40F0u)).Write(0x38, BitConverter.GetBytes(0x80F0F0u)).Write(0x4C, BitConverter.GetBytes((int)unchecked((short)frame.State.ReadWord(0x2544FEE))));
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect65 strips require current projection controls.");
        uint recordStart = checked(frame.State.ReadDword(ArenaGlobal) + 0x3CD30);
        for (int recordIndex = 0; recordIndex < 32; recordIndex++)
        {
            NativeBattleEffectArena record = ReadBytes(frame, checked(recordStart + (uint)(recordIndex * 56)), 56);
            uint pointAddress = Dword(record, 0);
            int count = Word(record, 0x34);
            if (pointAddress == 0 || count < 2)
            {
                continue;
            }
            if (count > 9)
            {
                throw new InvalidDataException("Effect65 strip rows exceed their selected native birth capacity.");
            }
            int fade = Word(record, 0x36);
            for (int colorIndex = 3; colorIndex >= 0; colorIndex--)
            {
                int from = 0x2C + colorIndex * 4;
                int to = 0x3C + colorIndex * 4;
                if (fade < 4096)
                {
                    registers = Color(registers, temporary, from, fade, false, inputs.Colors);
                    temporary = StoreColor(temporary, to, registers);
                }
                else
                {
                    temporary = NativeBattleEffectNodeContextMemory.Copy(temporary, from, temporary, to, 4);
                }
            }
            temporary = NativeBattleEffectNodeContextMemory.Copy(ReadSource(frame, resources, Dword(record, 8), 8), 0, temporary, 0xC, 8);
            temporary = NativeBattleEffectNodeContextMemory.Copy(ReadSource(frame, resources, Dword(record, 12), 8), 0, temporary, 0x14, 8);
            uint shape = TrigonometryAddress;
            int shapeStep = (2048 / count) * 4;
            int rowIndex = 0;
            while (pointAddress != 0)
            {
                if (rowIndex >= count)
                {
                    throw new InvalidDataException("Effect65 strip count disagrees with its current linked rows.");
                }
                NativeBattleEffectArena point = ReadBytes(frame, pointAddress, 24);
                int factor = Word(point, 0xE);
                int complement = unchecked(4096 - factor);
                registers = Weight(registers, Vector(temporary, 0xC), factor, false, inputs.Colors);
                registers = Weight(registers, Vector(point, 0), complement, true, inputs.Colors);
                temporary = Point(temporary, 0x1C, Ir(registers));
                registers = Weight(registers, Vector(temporary, 0x14), complement, false, inputs.Colors);
                registers = Weight(registers, Vector(point, 0), factor, true, inputs.Colors);
                temporary = Point(temporary, 0x24, Ir(registers));
                registers = Weight(registers, Vector(temporary, 0x1C), factor, false, inputs.Colors);
                registers = Weight(registers, Vector(temporary, 0x24), complement, true, inputs.Colors);
                temporary = Point(temporary, 0x1C, Ir(registers));
                registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(temporary, 0x1C), NullableWord(temporary, 0x22));
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                int destination = 0x50 + rowIndex * 20;
                uint screen = registers.Geometry.Sxy[2]!.Value;
                temporary = PutWord(temporary, destination, unchecked((short)screen) / 8);
                temporary = PutWord(temporary, destination + 2, unchecked((short)(screen >> 16)) / 8);
                temporary = temporary.Write(destination + 4, BitConverter.GetBytes(registers.Geometry.Sz[3]!.Value >> 2));
                short shapeFactor = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(shape, 2).AsSpan());
                temporary = PutWord(temporary, destination + 6, unchecked(Word(point, 6) * shapeFactor) >> 12);
                shape = checked(shape + (uint)shapeStep);
                int phase = Word(point, 0x10);
                int color = phase >> 12;
                int fraction = phase & 4095;
                registers = Color(registers, temporary, checked(0x3C + color * 4), 4096 - fraction, false, inputs.Colors);
                registers = Color(registers, temporary, checked(0x40 + color * 4), fraction, true, inputs.Colors);
                temporary = StoreColor(temporary, destination + 16, registers);
                pointAddress = Dword(point, 0x14);
                rowIndex++;
            }
            if (rowIndex != count)
            {
                throw new InvalidDataException("Effect65 strip count disagrees with its current linked rows.");
            }
            for (int index = 0; index < count; index++)
            {
                int row = 0x50 + index * 20;
                int? next = index + 1 < count ? row + 20 : null;
                if (Word(temporary, row + 4) < 0 || next is int following && Word(temporary, following + 4) < 0)
                {
                    continue;
                }
                int dx = 0;
                int dy = 0;
                if (next is int nextRow)
                {
                    int differenceX = Word(temporary, nextRow) - Word(temporary, row);
                    int differenceY = Word(temporary, row + 2) - Word(temporary, nextRow + 2);
                    int distanceIndex = Math.Min(Math.Abs(differenceY), 127) * 128 + Math.Min(Math.Abs(differenceX), 127);
                    byte distance = resources.ReadStatic(checked(DistanceAddress + (uint)distanceIndex), 1)[0];
                    short? previousX = NullableWord(temporary, 4);
                    short? previousY = NullableWord(temporary, 6);
                    NativeBattleRibbonDirectionStep direction = NativeBattleRibbonDirection.Resolve(differenceX, differenceY, distance, index != 0, previousX, previousY);
                    if (!direction.Valid)
                    {
                        temporary = PutWord(temporary, row + 4, -1);
                        continue;
                    }
                    temporary = PutWord(PutWord(temporary, 4, direction.PreviousX!.Value), 6, direction.PreviousY!.Value);
                    int width = Word(temporary, nextRow + 6) / (Word(temporary, row + 4) + 128);
                    dx = unchecked(width * direction.X) >> 12;
                    dy = unchecked(width * direction.Y) >> 12;
                }
                short x = Word(temporary, row);
                short y = Word(temporary, row + 2);
                temporary = PutWord(PutWord(temporary, row, x - dx), row + 2, y - dy);
                temporary = PutWord(PutWord(temporary, row + 8, x + dx), row + 10, y + dy);
            }
            frame = PublishContext(Publish(frame, registers), temporaryAddress, temporary, 400);
            for (int index = 0; index < count - 1; index++)
            {
                int row = 0x50 + index * 20;
                int depthA = Word(temporary, row + 4);
                int depthB = Word(temporary, row + 24);
                if (depthA <= 0 || depthB <= 0)
                {
                    continue;
                }
                NativeBattleEffect65PacketWrites packet = new(52);
                packet.Dword(0, 0x0C000000);
                packet.Copy(8, temporary, row, 4);
                packet.Copy(20, temporary, row + 8, 4);
                packet.Copy(32, temporary, row + 20, 4);
                packet.Copy(44, temporary, row + 28, 4);
                packet.Copy(4, temporary, row + 16, 4);
                packet.Copy(16, temporary, row + 16, 4);
                packet.Copy(28, temporary, row + 36, 4);
                packet.Copy(40, temporary, row + 36, 4);
                packet.Byte(7, 0x3E);
                packet.Dword(12, 0x3F54A048);
                packet.Dword(24, 0x00B6A078);
                packet.Word(36, 0xA048);
                packet.Word(48, 0xA078);
                int bias = unchecked((int)Dword(temporary, 0x4C));
                int bucket = unchecked(17 + Math.Max(0, unchecked(((depthA + depthB) >> 3) + bias)));
                NativeBattleGfGeometryAttempt attempt = packet.Attempt(0, bucket, [0, 0, 0, 0]);
                frame = inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(new([attempt], 52), 0x1D8E054, 1));
            }
            temporary = Context(frame, temporaryAddress, 400);
        }
        frame = PublishContext(Publish(frame, registers), temporaryAddress, temporary, 400);
        return Release(frame, 400);
    }

    private static NativeBattleGeometryRegisterStep Color(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena source, int offset, int factor, bool accumulate, NativeBattleSummonColors colors)
    {
        byte[] rgb = source.Read(offset, 3);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, rgb[0], rgb[1], rgb[2]);
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, colors);
    }
    private static NativeBattleEffectArena StoreColor(NativeBattleEffectArena destination, int offset, NativeBattleGeometryRegisterStep registers)
    {
        byte[] rgb = Enumerable.Range(0, 3).Select(index => unchecked((byte)registers.Colors.Ir[index]!.Value)).ToArray();
        return destination.Write(offset, rgb);
    }
    private static short? NullableWord(NativeBattleEffectArena source, int offset)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
}
