using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Particles
{
    public static NativeBattleEffect65Frame Draw6E2ED0(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        (frame, uint temporaryAddress) = Allocate(frame, 120);
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 120);
        uint common = frame.State.ReadDword(ArenaGlobal);
        for (int index = 0; index < 8; index++)
        {
            temporary = temporary.Write(0x40 + index * 4, BitConverter.GetBytes(checked(common + 0x39E00u + (uint)(index * 0x2B8))));
        }
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena row = ReadBytes(frame, checked(common + 0x3B530u + (uint)(index * 22)), 22);
            if (row.Read(6, 1)[0] == 0)
            {
                continue;
            }
            short scale = Word(row, 0xE);
            (short sinX, short cosX) = Trig(resources, Word(row, 0x10));
            (short sinY, short cosY) = Trig(resources, Word(row, 0x12));
            temporary = temporary.Write(0, new byte[20]);
            temporary = PutWord(temporary, 0, scale);
            temporary = PutWord(temporary, 8, unchecked(cosX * scale) >> 12);
            temporary = PutWord(temporary, 10, unchecked(sinX * scale) >> 12);
            temporary = PutWord(temporary, 14, -(unchecked(sinX * scale) >> 12));
            temporary = PutWord(temporary, 16, unchecked(cosX * scale) >> 12);
            temporary = temporary.Write(0x20, new byte[20]);
            temporary = PutWord(temporary, 0x20, cosY);
            temporary = PutWord(temporary, 0x24, -sinY);
            temporary = PutWord(temporary, 0x28, 4096);
            temporary = PutWord(temporary, 0x2C, sinY);
            temporary = PutWord(temporary, 0x30, cosY);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, Matrix(temporary, 0, false), true, false, Padding(temporary, 18)) };
            temporary = RotateColumns(ref registers, temporary, 0x20, 0);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = temporary.Write(0x14 + axis * 4, BitConverter.GetBytes((int)Word(row, axis * 2)));
            }
            NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, false, Padding(PackedArena(view), 18)) };
            temporary = RotateColumns(ref registers, temporary, 0, 0);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), false, true, null) };
            NativeBattleVector point = new(Word(row, 0), Word(row, 2), Word(row, 4));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, point.Z < 0 ? (short)-1 : (short)0));
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = temporary.Write(0x14 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis]!.Value));
            }
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, Matrix(temporary, 0, true), true, true, Padding(temporary, 18)) };
            int selected = unchecked((sbyte)row.Read(7, 1)[0]);
            if ((uint)selected >= 8)
            {
                throw new InvalidDataException("Effect65 particle selects outside its eight loaded raw sources.");
            }
            uint source = Dword(temporary, 0x40 + selected * 4);
            uint polygon = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, source, 4).Read(0, 4));
            temporary = temporary.Write(0x60, BitConverter.GetBytes(source + 8)).Write(0x64, BitConverter.GetBytes(checked(source + polygon + 0x18)));
            frame = PublishContext(Publish(frame, registers), temporaryAddress, temporary, 120);
            frame = DrawFaces(frame, temporaryAddress, 120, 0x60, 12, false, 0x1D8E054, 2, inputs);
            temporary = Context(frame, temporaryAddress, 120);
        }
        return Release(frame, 120);
    }

    public static NativeBattleEffect65Frame Draw6E3610(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        (frame, uint temporaryAddress) = Allocate(frame, 40);
        uint common = frame.State.ReadDword(ArenaGlobal);
        uint source = checked(common + 0x3B3C0);
        for (int index = 0; index < 64; index++)
        {
            NativeBattleEffectArena row = ReadBytes(frame, checked(common + 0x27690u + (uint)(index * 32)), 32);
            if (Word(row, 6) == 0)
            {
                continue;
            }
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, Matrix(row, 8, false), true, false, Padding(row, 26)) };
            NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 40);
            int x = Word(row, 0);
            int y = Word(row, 2);
            int z = Word(row, 4);
            temporary = temporary.Write(0, BitConverter.GetBytes(x)).Write(4, BitConverter.GetBytes(y)).Write(8, BitConverter.GetBytes(z));
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, new([0, 0, 0, 0, 0, 0, 0, 0, 0], x, y, z, 4096), false, true, null) };
            uint polygon = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, source, 4).Read(0, 4));
            temporary = temporary.Write(0x10, BitConverter.GetBytes(source + 8)).Write(0x14, BitConverter.GetBytes(checked(source + polygon + 0x1C)));
            frame = PublishContext(Publish(frame, registers), temporaryAddress, temporary, 40);
            frame = DrawFaces(frame, temporaryAddress, 40, 0x10, 14, true, CursorGlobal, 4, inputs);
        }
        return Release(frame, 40);
    }

    public static NativeBattleEffect65Frame Draw6E33C0(NativeBattleEffect65Frame frame, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        (frame, uint temporaryAddress) = Allocate(frame, 180);
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 180);
        foreach (int field in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = PutWord(temporary, field, 0);
        }
        NativeBattleModuleResource sprite = resources.Sprite(SpriteAddress);
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0x3E1F0);
        for (int index = 0; index < 128; index++)
        {
            NativeBattleEffectArena row = ReadBytes(frame, checked(start + (uint)(index * 8)), 8);
            short life = Word(row, 6);
            if (life == 0)
            {
                continue;
            }
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(Word(row, 0), Word(row, 2), Word(row, 4)), life));
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = temporary.Write(0x98 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis]!.Value));
            }
            temporary = temporary.Write(0x84, new byte[20]);
            foreach (int field in new[] { 0x84, 0x8C, 0x94 })
            {
                temporary = PutWord(temporary, field, 2048);
            }
            temporary = temporary.Write(0, BitConverter.GetBytes(SpriteAddress));
            int frames = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8));
            int directory = frames - (life - 1) % frames;
            int displacement = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8 + directory * 2));
            uint selected = unchecked(SpriteAddress + (uint)displacement);
            int count = BinaryPrimitives.ReadInt32LittleEndian(sprite.Bytes.AsSpan().Slice(checked((int)(selected - SpriteAddress))));
            if (count < 0)
            {
                temporary = temporary.Write(0x25, [unchecked((byte)(temporary.Read(0x25, 1)[0] | 1))]);
                count &= int.MaxValue;
            }
            temporary = temporary.Write(0x2C, BitConverter.GetBytes(selected + 4)).Write(0x30, BitConverter.GetBytes(count));
            NativeBattleShockwavePulsar260PuffStep drawn = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(Publish(frame, registers).Memory, temporary, sprite, inputs.ReadProjection(frame));
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporaryAddress, drawn.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(drawn.Batch, CursorGlobal));
            temporary = Context(frame, temporaryAddress, 180);
        }
        return Release(frame, 180);
    }

    private static NativeBattleEffectArena RotateColumns(ref NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena temporary, int source, int destination)
    {
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)Word(temporary, source + column * 2)), unchecked((ushort)Word(temporary, source + 6 + column * 2)), unchecked((ushort)Word(temporary, source + 12 + column * 2)));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                temporary = PutWord(temporary, destination + row * 6 + column * 2, registers.Colors.Ir[row]!.Value);
            }
        }
        return temporary;
    }
    private static NativeBattleActionMatrix Matrix(NativeBattleEffectArena source, int offset, bool translation) => new(Enumerable.Range(0, 9).Select(index => Word(source, offset + index * 2)).ToImmutableArray(), translation ? unchecked((int)Dword(source, offset + 20)) : 0, translation ? unchecked((int)Dword(source, offset + 24)) : 0, translation ? unchecked((int)Dword(source, offset + 28)) : 0, 4096);
    private static short? Padding(NativeBattleEffectArena source, int offset)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static (short Sin, short Cos) Trig(NativeBattleEffect65Resources resources, int angle)
    {
        byte[] bytes = resources.ReadStatic(checked(TrigonometryAddress + (uint)((angle & 4095) * 4)), 4).ToArray();
        return (BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)));
    }
}
