using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect318Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect318Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect318Records
{
    public static NativeBattleEffect318Frame Birth65F710(NativeBattleEffect318Frame frame, uint main, NativeBattleEffectArena point8, NativeBattleEffectArena direction8, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        uint first = checked(main + 0xB00);
        uint last = checked(first + 127 * 16);
        uint address = Dword318(ReadBytes(frame, main + 0x1300, 4), 0);
        if (address < first || address > last || (address - first) % 16 != 0)
        {
            throw new InvalidDataException("The record cursor requires its current physical row.");
        }
        if (Life(address) != 0)
        {
            address = first;
            int index = 0;
            while (Life(address) != 0)
            {
                if (++index >= 128)
                {
                    return frame;
                }
                address += 16;
            }
        }
        frame = WriteBytes(frame, main + 0x1300, Put318(NativeBattleEffectArena.Clear(), 0, address >= last ? first : address + 16), 4);
        NativeBattleEffectArena row = NativeBattleEffectNodeContextMemory.Copy(point8, 0, ReadBytes(frame, address, 16), 0, 8);
        row = PutWord318(row, 6, 11);
        frame = frame with { Random = frame.Random.Draw(out ushort random) };
        row = PutWord318(row, 8, (random >> 4) + 2048);
        NativeBattleEffectArena local = Direction65F7E0(direction8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector318(local), null), false);
        for (int axis = 0; axis < 3; axis++)
        {
            row = row.Write(12 + axis, [unchecked((byte)(unchecked((short)registers.Colors.Ir[axis]!.Value) >> 8))]);
        }
        frame = WriteBytes(Publish(frame, registers), address, row, 16);
        if (frame.CapturePresentation)
        {
            NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena!, address, 0);
        }
        return frame;

        short Life(uint current) => Word318(ReadBytes(frame, current + 6, 2), 0);
    }

    public static NativeBattleEffect318Frame Advance65F340(NativeBattleEffect318Frame frame, uint main, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = PutWord318(context, offset, 0);
        }
        frame = PublishContext(frame, temporary, context, 180);
        for (int index = 0; index < 128; index++)
        {
            uint address = checked(main + 0xB00 + (uint)(index * 16));
            if (Word318(ReadBytes(frame, address + 6, 2), 0) != 0)
            {
                frame = DrawRecord(frame, address, temporary, resources, inputs);
            }
        }
        return Release(frame, 180);
    }

    public static NativeBattleEffect318Frame DrawRecord(NativeBattleEffect318Frame frame, uint address, uint temporary, NativeBattleEffect318Resources resources, NativeBattleEffect318Inputs inputs)
    {
        NativeBattleEffectArena row = ReadBytes(frame, address, 16);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        short life = Word318(row, 6);
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector318(row), life), true);
        frame = Publish(frame, registers);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put318(context, 0x98 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            context = PutWord318(context, 0x84 + axis * 8, Word318(row, 8));
        }
        NativeBattleModuleResource source = resources.ReadProjectedQuads(0xFF7B6C);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8));
        int directory = checked(8 + 2 * (count - life));
        if (directory < 0 || directory > source.Bytes.Length - 2)
        {
            throw new InvalidDataException("The record requires its actual projected-quad directory row.");
        }
        int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(directory));
        int quads = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(offset));
        context = Put318(Put318(Put318(context, 0, source.Address), 0x2C, checked(source.Address + (uint)offset + 4)), 0x30, unchecked((uint)quads));
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleEffect318RasterStep draw = NativeBattleEffect318Raster.DrawLoaded(frame.Memory, context, source, inputs.ReadProjection(frame), NativeBattleActor33Presentation.Row(frame.PilotIdentity, frame.Memory.Arena!, address, source.Address));
        frame = PublishContext(frame with { Memory = draw.Memory }, temporary, draw.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect318DrawBatch(draw.Batch, 0x250541C));
        row = ReadBytes(frame, address, 16);
        row = PutWord318(row, 6, Word318(row, 6) - 1);
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = unchecked((sbyte)row.Read(12 + axis, 1)[0]);
            row = PutWord318(row, axis * 2, Word318(row, axis * 2) + (velocity << 2));
        }
        return WriteBytes(frame, address, row, 16);
    }
}
