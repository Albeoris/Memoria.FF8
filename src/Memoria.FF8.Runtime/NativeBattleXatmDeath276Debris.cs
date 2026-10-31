using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleXatmDeath276Debris
{
    public static NativeBattleXatmDeath276Frame Burst687150(NativeBattleXatmDeath276Frame frame, NativeBattleXatmDeath276Resources resources, NativeBattleXatmDeath276Burst command, ImmutableArray<NativeBattleRawMesh> meshes, ImmutableArray<NativeBattleActionMatrix> bones, uint objectMask, NativeBattleModelTextureBinding? textureBinding)
    {
        uint seed = frame.State.Seed;
        int Random()
        {
            seed = unchecked(seed * 125 + 14) & 32767;
            return checked((int)seed);
        }
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native687150 requires its actual packet arena scratch.");
        int scratch = frame.State.DrawCursor ?? throw new NotSupportedException("Native687150 requires its current arena pointer.");
        NativeBattleActionMatrix[] orientations = new NativeBattleActionMatrix[64];
        NativeBattleActionMatrix[] spins = new NativeBattleActionMatrix[64];
        for (int bank = 0; bank < 2; bank++)
        {
            for (int index = 0; index < 64; index++)
            {
                short x = unchecked((short)(bank == 0 ? Random() : (Random() & 0x1FF) - 0x100));
                short y = unchecked((short)(bank == 0 ? Random() : (Random() & 0x1FF) - 0x100));
                short z = unchecked((short)(bank == 0 ? Random() : (Random() & 0x1FF) - 0x100));
                NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(x, y, z));
                int scale = bank == 0 ? (Random() & 0xFFF) + 0x2000 : 0xF81 - (Random() & 255);
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
                (bank == 0 ? orientations : spins)[index] = rotation;
                byte[] words = new byte[18];
                for (int lane = 0; lane < 9; lane++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(words.AsSpan(lane * 2), rotation.Rotation[lane]);
                }
                int matrixOffset = checked(scratch + (bank * 64 + index) * 32);
                arena = arena.Write(matrixOffset, words).MarkUnknown(matrixOffset + 18, 2);
            }
        }
        HashSet<int> selected = [];
        for (int offset = 0x16; command.Table.Data[offset] != 255; offset += 6)
        {
            selected.Add(command.Table.Data[offset] & 0x7F);
        }
        NativeBattleEffectTaskPool directors = frame.Effect.Pools[7];
        NativeBattleModulePoolTask director = directors.Slots[0];
        int remaining = command.Count;
        bool exhausted = false;
        for (int objectIndex = 0; objectIndex < meshes.Length && !exhausted; objectIndex++)
        {
            if ((objectMask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[objectIndex];
            NativeBattleVector[] positions = mesh.Vertices.Select(vertex => selected.Contains(vertex.Bone) ? NativeBattleGfMatrices.Transform460840(bones[vertex.Bone], vertex.Position) : vertex.Position).ToArray();
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                if (Random() > command.Probability)
                {
                    continue;
                }
                if (polygon.Bytes.Length != (polygon.Quad ? 20 : 16))
                {
                    throw new NotSupportedException("Native687320 requires the original plain triangle/quad groups.");
                }
                int count = polygon.Quad ? 4 : 3;
                int[] indices = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(polygon.Bytes.AsSpan().Slice(index * 2)) & 0xFFF).ToArray();
                if (indices.Any(index => !selected.Contains(mesh.Vertices[index].Bone)))
                {
                    continue;
                }
                int record = Allocate(director, directors.BankAddress);
                if (record < 0)
                {
                    exhausted = true;
                    break;
                }
                NativeBattleSelectedProgrammeVisuals.Allocated(frame.Effect.Pools, directors.BankAddress + (uint)record);
                director = director.WriteDword(0x50, checked(directors.BankAddress + (uint)(record >= 0x240C ? 0x54 : record + 0x48)));
                short Centre(Func<NativeBattleVector, short> component) => unchecked((short)(indices.Sum(index => component(positions[index])) / count));
                short cx = Centre(point => point.X);
                short cy = Centre(point => point.Y);
                short cz = Centre(point => point.Z);
                NativeBattleActionMatrix orientation = orientations[Random() & 63];
                int shapeBase = polygon.Quad ? 24 : 0;
                for (int corner = 0; corner < count; corner++)
                {
                    int source = shapeBase + corner * 8;
                    NativeBattleVector shape = new(BinaryPrimitives.ReadInt16LittleEndian(resources.Shapes.AsSpan().Slice(source)), BinaryPrimitives.ReadInt16LittleEndian(resources.Shapes.AsSpan().Slice(source + 2)), BinaryPrimitives.ReadInt16LittleEndian(resources.Shapes.AsSpan().Slice(source + 4)));
                    NativeBattleVector rotated = NativeBattleGfMatrices.Transform460840(orientation, shape);
                    director = Position(director, record + 0x24 + corner * 6, rotated);
                }
                director = Position(director, record, new(cx, cy, cz));
                director = W(director, record + 6, polygon.Quad ? 2 : 1);
                if (polygon.Quad)
                {
                    director = W(director, record + 8, (Random() & 0x1FF) - 0x100);
                    director = W(director, record + 12, (Random() & 0x1FF) - 0x100);
                    director = W(director, record + 10, (Random() & 255) - 0xC8);
                }
                else
                {
                    int speed = command.Speed;
                    director = W(director, record + 8, (Random() & unchecked((int)(0xFFFF0000u | (ushort)speed))) - (speed >> 1));
                    director = W(director, record + 12, (Random() & unchecked((int)(0xFFFF0000u | (ushort)speed))) - (speed >> 1));
                    director = W(director, record + 10, (Random() & (speed >> 1)) + (speed >> 3) - (speed >> 1));
                }
                director = W(director, record + 14, (Random() & 7) + 12);
                ImmutableArray<byte> material = textureBinding is null ? polygon.Bytes : NativeBattleRawGeometryPackets.Relocate507550(polygon, textureBinding);
                director = director.WriteDword(record + 0x3C, BinaryPrimitives.ReadUInt32LittleEndian(material.AsSpan().Slice(8)));
                director = director.WriteDword(record + 0x40, BinaryPrimitives.ReadUInt32LittleEndian(material.AsSpan().Slice(12)));
                if (polygon.Quad)
                {
                    director = W(director, record + 0x44, BinaryPrimitives.ReadUInt16LittleEndian(material.AsSpan().Slice(16)));
                    director = W(director, record + 0x46, BinaryPrimitives.ReadUInt16LittleEndian(material.AsSpan().Slice(18)));
                }
                else
                {
                    director = W(director, record + 0x44, BinaryPrimitives.ReadUInt16LittleEndian(material.AsSpan().Slice(6)));
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    int[] uv = Enumerable.Range(0, count).Select(index => (int)director.ReadByte(record + (index < 2 ? 0x3C + index * 4 : 0x44 + (index - 2) * 2) + channel)).ToArray();
                    int mean = uv.Sum() / count;
                    for (int corner = 0; corner < count; corner++)
                    {
                        int offset = record + (corner < 2 ? 0x3C + corner * 4 : 0x44 + (corner - 2) * 2) + channel;
                        director = director.WriteByte(offset, unchecked((byte)(unchecked((byte)((uv[corner] - mean) >> 1)) + unchecked((byte)mean))));
                    }
                }
                NativeBattleActionMatrix spin = spins[Random() & 63];
                for (int lane = 0; lane < 9; lane++)
                {
                    director = W(director, record + 16 + lane * 2, spin.Rotation[lane]);
                }
                if (--remaining <= 0)
                {
                    exhausted = true;
                    break;
                }
            }
        }
        directors = directors.WriteSlot(0, director);
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(7, directors) }, State = frame.State with { Seed = seed } };
    }

    private static int Allocate(NativeBattleModulePoolTask director, uint bank)
    {
        int selected = checked((int)(director.ReadDword(0x50) - bank));
        if (Word(director, selected + 6) == 0)
        {
            return selected;
        }
        for (int index = 0; index < 128; index++)
        {
            int record = 0x54 + index * 0x48;
            if (Word(director, record + 6) == 0)
            {
                return record;
            }
        }
        return -1;
    }

    public static NativeBattleModulePoolTask MoveDebris(NativeBattleModulePoolTask director, uint bank)
    {
        for (int index = 0; index < 128; index++)
        {
            int record = 0x54 + index * 0x48;
            if (Word(director, record + 6) == 0)
            {
                continue;
            }
            director = W(director, record + 14, Word(director, record + 14) - 1);
            if (Word(director, record + 14) != 0)
            {
                ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(lane => Word(director, record + 16 + lane * 2)).ToImmutableArray();
                NativeBattleActionMatrix spin = new(rotation, 0, 0, 0, 4096);
                NativeBattleVector first = NativeBattleGfMatrices.Transform460840(spin, Point(director, record + 0x24));
                director = Position(director, record + 0x24, first);
                NativeBattleVector second = NativeBattleGfMatrices.Transform460840(spin, Point(director, record + 0x2A));
                if (director.ReadDword(record + 0x24) != 0 || Word(director, record + 0x28) != 0)
                {
                    director = Position(director, record + 0x2A, second);
                    director = Position(director, record + 0x30, NativeBattleGfMatrices.Transform460840(spin, Point(director, record + 0x30)));
                    director = Position(director, record + 0x36, NativeBattleGfMatrices.Transform460840(spin, Point(director, record + 0x36)));
                    short vy = Word(director, record + 10);
                    director = W(director, record + 2, Word(director, record + 2) + vy);
                    director = W(director, record, Word(director, record) + Word(director, record + 8));
                    director = W(director, record + 4, Word(director, record + 4) + Word(director, record + 12));
                    short velocity = unchecked((short)(vy + 30));
                    director = W(director, record + 10, Word(director, record + 2) >= 0 ? -velocity : velocity);
                    continue;
                }
            }
            director = W(director, record + 6, 0).WriteDword(0x50, checked(bank + (uint)record));
        }
        return director;
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleModulePoolTask W(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)value));
    private static NativeBattleVector Point(NativeBattleModulePoolTask task, int offset) => new(Word(task, offset), Word(task, offset + 2), Word(task, offset + 4));
    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => W(W(W(task, offset, point.X), offset + 2, point.Y), offset + 4, point.Z);
}
