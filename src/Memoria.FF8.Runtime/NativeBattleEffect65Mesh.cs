using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect65Mesh
{
    public static NativeBattleEffect65Frame Draw6E4230(NativeBattleEffect65Frame frame, int x, int y, int z, short angle, short scale, int phase, uint flags, int depth, uint source, uint cursorGlobal, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        (frame, uint address) = Allocate(frame, 140);
        NativeBattleEffectArena temporary = Context(frame, address, 140);
        int sine = unchecked(NativeBattleCameraMath.Sine(angle) * scale) >> 12;
        int cosine = unchecked(NativeBattleCameraMath.Cosine(angle) * scale) >> 12;
        NativeBattleActionMatrix matrix = new([unchecked((short)cosine), 0, unchecked((short)-sine), 0, scale, 0, unchecked((short)sine), 0, unchecked((short)cosine)], x, y, z, 4096);
        temporary = temporary.Write(0, new byte[20]);
        if ((flags & 0x8000) != 0)
        {
            NativeBattleActionMatrix view = inputs.ReadView(frame).ReadMatrix();
            (int tx, int ty, int tz) = NativeBattleGfMatrices.RotateLong56C600(view, x, y, z);
            matrix = matrix with { X = unchecked(tx + view.X), Y = unchecked(ty + view.Y), Z = unchecked(tz + view.Z) };
        }
        else
        {
            matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix);
        }
        temporary = StoreMatrix(temporary, 0, matrix);
        temporary = Put(temporary, 0x20, source);
        temporary = Put(temporary, 0x38, unchecked((uint)depth));
        temporary = Put(temporary, 0x3C, flags);
        temporary = Put(temporary, 0x40, 0);
        if (phase != 0)
        {
            temporary = PutWord(temporary, 0x42, phase & 127);
            foreach ((int offset, int value) in new (int, int)[] { (0x44, 0), (0x46, 0), (0x48, 256), (0x4A, 256), (0x4C, 0), (0x4E, 0), (0x50, 256), (0x52, 128) })
            {
                temporary = PutWord(temporary, offset, value);
            }
        }
        frame = PublishContext(frame, address, temporary, 140);
        frame = LoadMatrix(frame, Packed(temporary, 0));
        frame = DrawRaw(frame, address, 140, 0x20, source, cursorGlobal, resources, inputs);
        return Release(frame, 140);
    }

    public static NativeBattleEffect65Frame Draw6E4710(NativeBattleEffect65Frame frame, NativeBattleEffectArena first8, NativeBattleEffectArena second8, int waveX, int waveY, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        frame = Transform4810(frame, waveX, waveY, resources);
        (frame, uint address) = Allocate(frame, 156);
        NativeBattleEffectArena temporary = Context(frame, address, 156);
        int dx = Word(second8, 0) - Word(first8, 0);
        int dy = Word(second8, 2) - Word(first8, 2);
        int dz = Word(first8, 4) - Word(second8, 4);
        temporary = Put(temporary, 0, unchecked((uint)dx));
        temporary = Put(temporary, 4, unchecked((uint)dy));
        temporary = Put(temporary, 8, unchecked((uint)dz));
        NativeBattleActionMatrix matrix = NativeBattleFunguarLaser277.Basis50CBA0(dx, dy, dz).Matrix with { X = Word(first8, 0), Y = Word(first8, 2), Z = Word(first8, 4) };
        temporary = StoreMatrix(temporary, 0x10, matrix);
        temporary = StoreMatrix(temporary, 0x10, NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix));
        temporary = Put(temporary, 0x30, 0x133822C);
        temporary = Put(temporary, 0x34, checked(frame.State.ReadDword(ArenaGlobal) + 0x6CB8));
        temporary = Put(temporary, 0x48, unchecked((uint)-2048));
        temporary = Put(temporary, 0x4C, 0x2030);
        temporary = Put(temporary, 0x50, 0);
        frame = PublishContext(frame, address, temporary, 156);
        frame = LoadMatrix(frame, Packed(temporary, 0x10));
        frame = DrawRaw(frame, address, 156, 0x30, 0x133822C, CursorGlobal, resources, inputs);
        return Release(frame, 156);
    }

    public static NativeBattleEffect65Frame Transform4810(NativeBattleEffect65Frame frame, int waveX, int waveY, NativeBattleEffect65Resources resources)
    {
        NativeBattleStaticMeshResource source = resources.ReadMesh(0x133822C);
        uint destination = checked(frame.State.ReadDword(ArenaGlobal) + 0x6CB8);
        int count = checked((int)Dword(source.Bytes, 4));
        for (int index = 0; index < count; index++)
        {
            int offset = 8 + index * 8;
            short x = Word(source.Bytes, offset);
            short y = Word(source.Bytes, offset + 2);
            short z = Word(source.Bytes, offset + 4);
            uint lookup = unchecked(TrigonometryAddress - (uint)(2 * (z & ~1)));
            short factor = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(lookup, 2).AsSpan());
            NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear();
            xyz = PutWord(xyz, 0, unchecked((short)(x << 1)) + (unchecked(factor * waveX) >> 12));
            xyz = PutWord(xyz, 2, unchecked((short)(y << 1)) + (unchecked(factor * waveY) >> 12));
            xyz = PutWord(xyz, 4, z << 3);
            frame = WriteBytes(frame, checked(destination + (uint)(index * 8)), xyz, 6);
        }
        return frame;
    }

    public static NativeBattleEffect65Frame Transform4B70(NativeBattleEffect65Frame frame, int wave, int frequency, NativeBattleEffect65Resources resources)
    {
        NativeBattleStaticMeshResource source = resources.ReadMesh(0x133A0FC);
        uint destination = checked(frame.State.ReadDword(ArenaGlobal) + 0x6CB8);
        int count = checked((int)Dword(source.Bytes, 4));
        for (int index = 0; index < count; index++)
        {
            int offset = 8 + index * 8;
            short z = Word(source.Bytes, offset + 4);
            int row = (unchecked(z * frequency) >> 12) & 4095;
            short factor = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(checked(TrigonometryAddress + (uint)(row * 4)), 2).AsSpan());
            NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear();
            xyz = PutWord(xyz, 0, unchecked(Word(source.Bytes, offset) + (unchecked(factor * wave) >> 12)));
            xyz = PutWord(xyz, 2, Word(source.Bytes, offset + 2));
            xyz = PutWord(xyz, 4, z);
            frame = WriteBytes(frame, checked(destination + (uint)(index * 8)), xyz, 6);
        }
        return frame;
    }

    public static NativeBattleEffect65Frame Advance6E4570(NativeBattleEffect65Frame frame)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + 0xDC3);
        for (int index = 0; index < 1024; index++)
        {
            uint address = checked(start + (uint)(index * 16));
            byte color = ReadBytes(frame, address, 1).Read(0, 1)[0];
            sbyte delta = unchecked((sbyte)ReadBytes(frame, address + 4, 1).Read(0, 1)[0]);
            short sum = unchecked((short)(color + delta));
            if (unchecked((ushort)sum) > 255)
            {
                frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [unchecked((byte)~sum)]), 1);
                frame = WriteBytes(frame, address + 4, NativeBattleEffectArena.Clear().Write(0, [unchecked((byte)-delta)]), 1);
            }
            else
            {
                frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [unchecked((byte)sum)]), 1);
            }
        }
        for (int index = 0; index < 8; index++)
        {
            uint address = checked(LiteralBase + (uint)(index * 16));
            frame = frame with { State = frame.State.WriteWord(address + 4, unchecked((ushort)(frame.State.ReadWord(address + 4) + frame.State.ReadWord(address)))).WriteWord(address + 6, unchecked((ushort)(frame.State.ReadWord(address + 6) + frame.State.ReadWord(address + 2)))) };
        }
        return frame;
    }

    private static NativeBattleEffect65Frame DrawRaw(NativeBattleEffect65Frame frame, uint temporaryAddress, int temporaryLength, int contextOffset, uint sourceAddress, uint cursorGlobal, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs)
    {
        NativeBattleStaticMeshResource mesh;
        if (resources.Meshes.TryGetValue(sourceAddress, out NativeBattleStaticMeshResource? immutable))
        {
            mesh = immutable;
        }
        else
        {
            int offset = checked((int)(sourceAddress - frame.State.ReadDword(ArenaGlobal)));
            if (!CurrentMeshLengths.TryGetValue(offset, out int length))
            {
                throw new InvalidDataException("Effect65 raw draw requires its current selected loaded mesh.");
            }
            mesh = NativeBattleEffect65Resources.ReadCurrentRawMesh(sourceAddress, ReadBytes(frame, sourceAddress, length).Read(0, length).ToImmutableArray());
        }
        NativeBattleEffectArena context = Context(frame, checked(temporaryAddress + (uint)contextOffset), 108);
        uint selected = (Dword(context, 0x1C) & 0x2000) == 0 ? checked(sourceAddress + 8) : Dword(context, 4);
        NativeBattleWindowedMeshContext108Step result = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, 306, offset =>
        {
            NativeBattleEffectArena row = selected >= mesh.Address && (ulong)selected + (uint)offset + 8 <= (ulong)mesh.Address + (uint)mesh.Bytes.Length ? NativeBattleEffectArena.Clear().Write(0, mesh.Bytes.AsSpan(checked((int)(selected - mesh.Address)) + offset, 8)) : ReadBytes(frame, checked(selected + (uint)offset), 8);
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            if (!known.Take(6).All(value => value))
            {
                throw new NotSupportedException("Effect65 raw draw requires reached current XYZ rows.");
            }
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        });
        frame = PublishContext(frame with { Memory = result.Memory }, checked(temporaryAddress + (uint)contextOffset), result.Context108, 108);
        return inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(result.Batch, cursorGlobal));
    }

    private static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena arena, int offset, NativeBattleActionMatrix matrix, bool translation = true)
    {
        arena = arena.Write(offset, matrix.Rotation.SelectMany(BitConverter.GetBytes).ToArray());
        if (translation)
        {
            arena = Put(arena, offset + 20, unchecked((uint)matrix.X));
            arena = Put(arena, offset + 24, unchecked((uint)matrix.Y));
            arena = Put(arena, offset + 28, unchecked((uint)matrix.Z));
        }
        return arena;
    }

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
    private static uint Dword(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
