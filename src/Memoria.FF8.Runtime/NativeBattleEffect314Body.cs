using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect314Body
{
    public static NativeBattleEffect314Frame DrawCaster662C00(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint model, uint boneOffsets) => inputs.Execute(frame, new NativeBattleEffect314Prepare662C00(model, boneOffsets));

    public static NativeBattleEffect314Frame Draw663590(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint model, int x, int y, int z)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect314Shadow5088A0(model, 4112, 16));
        NativeBattleEffect314AddressStep allocation = inputs.AllocateTemporary(frame, 0x84);
        frame = allocation.Frame;
        uint temporary = allocation.Address;
        NativeBattleEffectArena context = inputs.ReadArena(frame, temporary, 0x84);
        context = PutWord(PutWord(PutWord(context, 0x6C, x), 0x6E, y), 0x70, z);
        frame = Write(frame, inputs, temporary, context, 0x84);
        frame = inputs.Execute(frame, new NativeBattleEffect314Prepare5095B0(model));
        context = inputs.ReadArena(frame, temporary, 0x84);
        context = PutDword(context, 0x24, ReadDword(frame, inputs, 0x1D98B3C));
        context = PutWord(PutWord(PutWord(PutWord(context, 0x2C, 0), 0x2E, 0), 0x30, 320), 0x32, 216);
        context = PutDword(PutDword(context, 0x34, ReadDword(frame, inputs, model + 0x28)), 0x38, ReadDword(frame, inputs, model + 0x7C));
        context = PutDword(PutDword(context, 0x7C, ReadDword(frame, inputs, 0x1D8E054)), 0x80, frame.State.ReadDword(NativeBattleEffect314Profile.CursorGlobal));
        frame = Write(frame, inputs, temporary, context, 0x84);
        frame = Walk663670(frame, inputs, model, inputs.ReadGeometryAddress(frame, model), temporary + 0x20, 17, 0);
        context = inputs.ReadArena(frame, temporary, 0x84);
        frame = inputs.Execute(frame, new NativeBattleEffect314PublishCursors(Dword(context, 0x7C), Dword(context, 0x80)));
        frame = inputs.Execute(frame, new NativeBattleEffect314Restore508C90(model));
        return inputs.ReleaseTemporary(frame, 0x84);
    }

    public static NativeBattleEffect314Frame Walk663670(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint model, uint source, uint contextAddress, int ordering, int shift)
    {
        int objects = unchecked((int)ReadDword(frame, inputs, source));
        for (int index = 0; index < objects; index++)
        {
            uint entry = ReadDword(frame, inputs, source + 4 + unchecked((uint)(index * 4)));
            NativeBattleEffectArena context = inputs.ReadArena(frame, contextAddress, 0x64);
            context = PutDword(context, 0x54, unchecked(source + entry));
            frame = Write(frame, inputs, contextAddress, context, 0x64);
            if ((Dword(context, 0x18) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            context = PutDword(context, 0x58, Dword(context, 4));
            uint raw = Dword(context, 0x54);
            int groups = Word(inputs.ReadArena(frame, raw, 2), 0);
            context = PutDword(context, 0x54, raw + 2);
            frame = Write(frame, inputs, contextAddress, context, 0x64);
            for (int group = 0; group < groups; group++)
            {
                context = inputs.ReadArena(frame, contextAddress, 0x64);
                raw = Dword(context, 0x54);
                int bone = Word(inputs.ReadArena(frame, raw, 2), 0);
                uint matrix = inputs.ResolveBoneRecord(frame, model, bone);
                frame = LoadMatrix(frame, inputs.ReadArena(frame, matrix, 32));
                context = PutDword(context, 0x54, raw + 2);
                frame = Write(frame, inputs, contextAddress, context, 0x64);
                NativeBattleEffect314ProjectionServices<NativeBattleEffect314Frame> projection = new(inputs.ReadArena, (current, address, data, length) => Write(current, inputs, address, data, length), current => current.Memory, (current, memory) => current with { Memory = memory }, inputs.ReadView, inputs.ReadProjection);
                frame = NativeBattleEffect314Projection663760.ProjectGroup(frame, projection, contextAddress, false);
            }
            context = inputs.ReadArena(frame, contextAddress, 0x64);
            raw = unchecked(Dword(context, 0x54) + 3) & 0xFFFFFFFC;
            NativeBattleEffectArena counts = inputs.ReadArena(frame, raw, 4);
            context = PutWord(PutWord(context, 8, Word(counts, 0)), 10, Word(counts, 2));
            context = PutDword(PutDword(context, 0, raw + 12), 0x54, raw + 12);
            frame = Write(frame, inputs, contextAddress, context, 0x64);
            NativeBattleEffect314FaceRasterServices<NativeBattleEffect314Frame> raster = new(inputs.ReadArena, (current, address, data, length) => Write(current, inputs, address, data, length), current => current.Memory, (current, memory) => current with { Memory = memory }, (current, batch, cursor) => inputs.Execute(current, new NativeBattleEffect314DrawBatch(batch, cursor)), _ => throw new NotSupportedException("The selected unexpanded body cannot draw a fragment random value."), (_, _, _) => throw new NotSupportedException("The selected unexpanded body cannot emit expanded fragments."));
            frame = NativeBattleEffect314FaceRaster661720.RasterFaces(frame, raster, contextAddress, ordering, shift, false, NativeBattleEffect314Profile.CursorGlobal);
        }
        return frame;
    }

    private static NativeBattleEffect314Frame LoadMatrix(NativeBattleEffect314Frame frame, NativeBattleEffectArena arena)
    {
        NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(index => Word(arena, index * 2)).ToImmutableArray(), unchecked((int)Dword(arena, 20)), unchecked((int)Dword(arena, 24)), unchecked((int)Dword(arena, 28)), 4096);
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(18, 2);
        short? padding = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect314 body requires its actual geometry registers.");
        return frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(geometry, matrix, true, true, padding) } };
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static uint ReadDword(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint address) => Dword(inputs.ReadArena(frame, address, 4), 0);
    private static NativeBattleEffect314Frame Write(NativeBattleEffect314Frame frame, NativeBattleEffect314Inputs inputs, uint address, NativeBattleEffectArena data, int length) => inputs.Execute(frame, new NativeBattleEffect314WriteArena(address, data, length));
}
