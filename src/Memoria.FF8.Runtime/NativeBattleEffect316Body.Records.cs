using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect316Body
{
    public static NativeBattleEffect316Frame Birth662590(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint main, NativeBattleEffectArena point)
    {
        uint first = checked(main + 0x18);
        uint last = checked(main + 0x808);
        uint rowAddress = ReadDword(frame, inputs, main + 0x818);
        if (rowAddress < first || rowAddress > last || (rowAddress - first) % 16 != 0)
        {
            throw new InvalidDataException("The record hint requires its current physical row.");
        }
        if (Life(rowAddress) != 0)
        {
            rowAddress = first;
            int index = 0;
            while (Life(rowAddress) != 0)
            {
                if (++index >= 128)
                {
                    return frame;
                }
                rowAddress += 16;
            }
        }
        frame = WriteDword(frame, inputs, main + 0x818, rowAddress >= last ? first : rowAddress + 16);
        NativeBattleEffectArena row = Copy(point, 0, inputs.ReadArena(frame, rowAddress, 16), 0, 8);
        row = PutWord(row, 6, 11);
        frame = NextRandom662570(frame, out int size);
        row = PutWord(row, 8, (size >> 12) + 1024);
        frame = NextRandom662570(frame, out int vx);
        row = row.Write(12, [unchecked((byte)((vx >> 13) - 2))]);
        frame = NextRandom662570(frame, out int vy);
        row = row.Write(13, [unchecked((byte)(-3 - (vy >> 11)))]);
        frame = NextRandom662570(frame, out int vz);
        row = row.Write(14, [unchecked((byte)((vz >> 13) - 2))]);
        return Write(frame, inputs, rowAddress, row, 16);

        short Life(uint address) => Word(inputs.ReadArena(frame, address + 6, 2), 0);
    }

    public static NativeBattleEffect316Frame AdvanceRecords662720(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint main)
    {
        NativeBattleEffect316AddressStep allocation = inputs.AllocateTemporary(frame, 0xB4);
        frame = allocation.Frame;
        uint temporary = allocation.Address;
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporary, 0xB4);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = PutWord(context, offset, 0);
        }
        frame = Write(frame, inputs, temporary, context, 0xB4);
        for (int index = 0; index < 128; index++)
        {
            uint address = checked(main + 0x18 + (uint)(index * 16));
            if (Word(inputs.ReadArena(frame, address + 6, 2), 0) != 0)
            {
                frame = DrawRecord6627B0(frame, resources, inputs, address, temporary);
            }
        }
        return inputs.ReleaseTemporary(frame, 0xB4);
    }

    public static NativeBattleEffect316Frame DrawRecord6627B0(NativeBattleEffect316Frame frame, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs, uint address, uint temporary)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleEffectArena row = inputs.ReadArena(frame, address, 16);
        short life = Word(row, 6);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(row), life));
        frame = Publish(frame, registers);
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporary, 0xB4);
        for (int axis = 0; axis < 3; axis++)
        {
            context = PutDword(context, 0x98 + axis * 4, unchecked((uint)(registers.Colors.Mac[axis] ?? throw new NotSupportedException("The record requires current transformed MAC coordinates."))));
            context = PutWord(context, 0x84 + axis * 8, Word(row, 8));
        }
        NativeBattleModuleResource source = resources.ReadProjectedQuads(0x1005AE4);
        int frames = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8));
        int directory = checked(8 + 2 * (frames - life));
        if (directory < 0 || directory > source.Bytes.Length - 2)
        {
            throw new InvalidDataException("The record requires its actual projected-quad directory index.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(directory));
        if (offset > source.Bytes.Length - 4)
        {
            throw new InvalidDataException("The selected projected-quad prefix leaves its source.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(offset));
        context = PutDword(PutDword(PutDword(context, 0, source.Address), 0x2C, checked(source.Address + (uint)offset + 4)), 0x30, unchecked((uint)count));
        frame = Write(frame, inputs, temporary, context, 0xB4);
        NativeBattleEffect318RasterStep draw = NativeBattleEffect318Raster.DrawLoaded(frame.Memory, context, source, inputs.ReadProjection(frame));
        frame = Write(frame with { Memory = draw.Memory }, inputs, temporary, draw.Temporary180, 0xB4);
        frame = inputs.Execute(frame, new NativeBattleEffect316DrawBatch(draw.Batch, PrivateCursorGlobal));
        row = inputs.ReadArena(frame, address, 16);
        row = PutWord(row, 6, Word(row, 6) - 1);
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = unchecked((sbyte)row.Read(12 + axis, 1)[0]);
            row = PutWord(row, axis * 2, Word(row, axis * 2) + (velocity << 2));
        }
        return Write(frame, inputs, address, row, 16);
    }
}
