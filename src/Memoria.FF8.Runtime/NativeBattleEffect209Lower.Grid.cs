using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect209Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Lower
{
    public static NativeBattleEffect209Frame InitializeGrid7F40(NativeBattleEffect209Frame frame)
    {
        for (int row = 0; row < GridRows; row++)
        {
            for (int column = 0; column < GridColumns; column++)
            {
                uint address = GridAddress + (uint)((row * GridColumns + column) * 12);
                ushort edge = (ushort)((row is 0 or 27 ? 1 : 0) | (column is 0 or 40 ? 2 : 0));
                frame = frame with { State = frame.State.WriteWord(address + 4, unchecked((ushort)(-164 + column * 8))).WriteWord(address + 6, unchecked((ushort)(-112 + row * 8))).WriteWord(address + 8, 0).WriteWord(address + 10, edge) };
            }
        }
        uint cursor = frame.State.ReadDword(GuardianGlobal);
        for (int row = 0; row < 27; row++)
        {
            for (int column = 0; column < 40; column++)
            {
                uint grid = GridAddress + (uint)((row * 41 + column) * 12);
                NativeBattleEffectArena record = ReadBytes(frame, cursor, 96);
                record = Put(Put(Put(Put(record, 0, grid), 4, grid + 12), 8, grid + 492), 12, grid + 504);
                for (int side = 0; side < 2; side++)
                {
                    int offset = 16 + side * 40;
                    int x = column * 8;
                    int y = row * 8;
                    byte left = unchecked((byte)(x & 63));
                    byte right = unchecked((byte)((x & 63) + (column == 39 ? 7 : 8)));
                    byte top = (byte)y;
                    byte bottom = (byte)(row == 26 ? 215 : y + 8);
                    record = Put(Put(record, offset, 0x09000000), offset + 4, 0x2C808080);
                    record = record.Write(offset + 12, [left, top]).Write(offset + 20, [right, top]).Write(offset + 28, [left, bottom]).Write(offset + 36, [right, bottom]);
                    record = Word(record, offset + 22, unchecked((ushort)(((x >> 6) + 25) | 0x120)));
                }
                frame = WriteBytes(frame, cursor, record, 96);
                cursor += 96;
            }
        }
        return frame;
    }

    public static NativeBattleEffect209Frame DeformGrid80F0(NativeBattleEffect209Frame frame, int age, int duration, NativeBattleEffect209Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 12);
        NativeBattleEffect209PointStep anchor = inputs.Point502170(frame, checked((int)frame.State.ReadDword(CasterGlobal)), 0xF1, 0, Context(frame, temporary, 12));
        frame = PublishContext(anchor.Frame, temporary, anchor.Output8, 8);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(anchor.Output8), OptionalWord(anchor.Output8, 6)), inputs.ReadProjection(frame), frame.Memory.ScriptedGraphics!.Dqa, frame.Memory.ScriptedGraphics.Dqb);
        frame = Publish(frame, registers);
        uint screen = registers.Geometry.Sxy[2]!.Value;
        short x = unchecked((short)((unchecked((short)screen) >> 3) - 164));
        short y = unchecked((short)((unchecked((short)(screen >> 16)) >> 3) - 112));
        frame = PublishContext(frame, temporary, Word(Word(Context(frame, temporary, 12), 8, unchecked((ushort)x)), 10, unchecked((ushort)y)), 12);
        int amplitude = unchecked(500 * NativeBattleCameraMath.Sine(unchecked(age << 11) / duration)) >> 12;
        int phase = unchecked(age * 385);
        int radius = unchecked(600 - age * 9);
        for (int index = 0; index < GridRows * GridColumns; index++)
        {
            uint address = GridAddress + (uint)(index * 12);
            if ((frame.State.ReadWord(address + 10) & 3) != 0)
            {
                continue;
            }
            int dx = unchecked((short)frame.State.ReadWord(address + 4)) - x;
            int dy = unchecked((short)frame.State.ReadWord(address + 6)) - y;
            int distance = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(dx * dx + dy * dy));
            int cosine = NativeBattleCameraMath.Cosine(unchecked(distance * 96 - phase)) - 4096;
            int scale = unchecked((radius - distance) * amplitude) / radius;
            frame = frame with { State = frame.State.WriteWord(address + 8, unchecked((ushort)(unchecked(cosine * scale) >> 13))) };
        }
        return Release(frame, 12);
    }

    public static NativeBattleEffect209Frame DrawGrid8250(NativeBattleEffect209Frame frame, byte side, uint color, NativeBattleEffect209Inputs inputs)
    {
        if (side > 1)
        {
            throw new InvalidDataException("Effect209 requires its current framebuffer side.");
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { X = 4, Y = 4, Z = 2048 };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        frame = inputs.Execute(frame, new NativeBattleEffect209SetProjectionH(2048));
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame) with { FocalLength = 2048 };
        for (int index = 0; index < GridRows * GridColumns; index++)
        {
            uint address = GridAddress + (uint)(index * 12);
            NativeBattleEffectArena point = frame.State.ReadView(address + 4, 8);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(point), unchecked((short)frame.State.ReadWord(address + 10))), projection, frame.Memory.ScriptedGraphics!.Dqa, frame.Memory.ScriptedGraphics.Dqb);
            frame = Publish(frame, registers);
            uint screen = registers.Geometry.Sxy[2]!.Value;
            ushort x = unchecked((ushort)(unchecked((short)screen) >> 3));
            ushort y = unchecked((ushort)(unchecked((short)(screen >> 16)) >> 3));
            frame = frame with { State = frame.State.WriteDword(address, (uint)x | (uint)y << 16) };
        }
        frame = inputs.Execute(frame, new NativeBattleEffect209SetProjectionH(unchecked((ushort)inputs.ReadScalarFocal(frame))));
        uint recordAddress = frame.State.ReadDword(GuardianGlobal);
        for (int index = 0; index < 27 * 40; index++)
        {
            NativeBattleEffectArena record = ReadBytes(frame, recordAddress, 96);
            uint packetAddress = recordAddress + 16 + (uint)(side * 40);
            NativeBattleEffectArena packet = ReadBytes(frame, packetAddress, 40);
            packet = Put(packet, 4, color | 0x2C000000);
            for (int corner = 0; corner < 4; corner++)
            {
                uint pointer = U(record, corner * 4);
                packet = Put(packet, 8 + corner * 8, frame.State.ReadDword(pointer));
            }
            frame = inputs.Execute(frame, new NativeBattleEffect209DrawAddressedTemplate(packetAddress, packet, 0x101A));
            recordAddress += 96;
        }
        return frame;
    }
}
