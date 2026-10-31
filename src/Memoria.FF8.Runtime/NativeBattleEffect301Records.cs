using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect301Records
{
    public static NativeBattleEffect301Frame Birth66E930(NativeBattleEffect301Frame frame, int x, int z, int faceIndex)
    {
        uint main = Main301(frame);
        if ((uint)faceIndex >= 2400)
        {
            throw new InvalidDataException("The body record requires its current face-map index.");
        }
        uint map = checked(main + 0x4AC + (uint)faceIndex);
        byte retained = ReadBytes(frame, map, 1).Read(0, 1)[0];
        uint address;
        if (retained != 0xFF)
        {
            if (retained >= 128)
            {
                throw new InvalidDataException("The retained body record index leaves its physical bank.");
            }
            address = checked(main + 0xAC + (uint)retained * 8);
        }
        else
        {
            uint first = checked(main + 0xAC);
            uint last = checked(first + 127 * 8);
            address = Dword301(ReadBytes(frame, main + 0xE0C, 4), 0);
            if (address < first || address > last || (address - first) % 8 != 0)
            {
                throw new InvalidDataException("The body record requires its current free cursor.");
            }
            if (Word301(ReadBytes(frame, address + 6, 2), 0) != 0)
            {
                address = first;
                int count = 0;
                while (Word301(ReadBytes(frame, address + 6, 2), 0) != 0)
                {
                    if (++count >= 128)
                    {
                        return frame;
                    }
                    address += 8;
                }
            }
            frame = WriteBytes(frame, main + 0xE0C, Put301(NativeBattleEffectArena.Clear(), 0, address >= last ? first : address + 8), 4);
            frame = WriteBytes(frame, address + 4, PutWord301(NativeBattleEffectArena.Clear(), 0, faceIndex), 2);
            if (frame.CapturePresentation)
            {
                NativeBattleEffect32Presentation.Allocated(frame.Effect, main, first, checked((int)((address - first) / 8)));
            }
        }
        frame = WriteBytes(frame, address, PutWord301(PutWord301(NativeBattleEffectArena.Clear(), 0, x), 2, z), 4);
        return WriteBytes(frame, address + 6, PutWord301(NativeBattleEffectArena.Clear(), 0, 12), 2);
    }

    public static NativeBattleEffect301Frame Advance66DF20(NativeBattleEffect301Frame frame, uint main, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = PutWord301(context, offset, 0);
        }
        frame = PublishContext(frame, temporary, context, 180);
        for (int index = 0; index < 128; index++)
        {
            uint address = checked(main + 0xAC + (uint)(index * 8));
            if (Word301(ReadBytes(frame, address + 6, 2), 0) != 0)
            {
                frame = DrawRecord(frame, address, temporary, resources, inputs);
            }
        }
        return Release(frame, 180);
    }

    public static NativeBattleEffect301Frame DrawRecord(NativeBattleEffect301Frame frame, uint address, uint temporary, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        NativeBattleEffectArena row = ReadBytes(frame, address, 8);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleVector point = new(Word301(row, 0), 0, Word301(row, 2));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, point, 0));
        frame = Publish(frame, registers);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put301(context, 0x98 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            context = PutWord301(context, 0x84 + axis * 8, 4096);
        }
        NativeBattleModuleResource source = resources.ReadProjectedQuads(0x103CFB8);
        int count = Word301(NativeBattleEffectArena.Clear().Write(0, source.Bytes.AsSpan().Slice(8, 2)), 0);
        int phase = count - Word301(row, 6);
        int offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(checked(8 + phase * 2), 2));
        int quads = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(offset));
        context = Put301(Put301(Put301(context, 0, source.Address), 0x2C, checked(source.Address + (uint)offset + 4)), 0x30, unchecked((uint)quads));
        frame = PublishContext(frame, temporary, context, 180);
        NativeBattleEffect301QuadStep drawn = NativeBattleEffect301CustomQuads.DrawLoaded(frame.Memory, context, source, inputs.ReadProjection(frame), NativeBattleEffect32Presentation.Resource(NativeBattleEffect32Presentation.TaskInstance(frame.PilotIdentity, frame.Effect, Main301(frame), checked(Main301(frame) + 0xAC), checked((int)((address - Main301(frame) - 0xAC) / 8))), source.Address));
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Temporary180, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect301DrawBatch(drawn.Batch, 0x250773C));
        row = ReadBytes(frame, address, 8);
        short life = unchecked((short)(Word301(row, 6) - 1));
        frame = WriteBytes(frame, address + 6, PutWord301(NativeBattleEffectArena.Clear(), 0, life), 2);
        if (life == 0)
        {
            frame = WriteBytes(frame, checked(Main301(frame) + 0x4AC + (uint)Word301(row, 4)), NativeBattleEffectArena.Clear().Write(0, [0xFF]), 1);
        }
        return frame;
    }
}
