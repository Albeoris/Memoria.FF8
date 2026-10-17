using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>An actual B8B914/B8B940 file selection and its complete original DAT, before507400 mutates staging memory.</summary>
public sealed record NativeBattleModelDatSource(byte ModelId, bool Linked, byte Selector, short FileId, string Name, ImmutableArray<byte> Bytes)
{
    public int Section(int index)
    {
        return NativeBattleDatDirectory.Section(Bytes.AsSpan(), index);
    }

    public NativeBattleEffectHeapAllocation TexturePack(uint callback)
    {
        int section = callback switch { 0x5077B0 => 5, 0x5079B0 => 8, 0x507BF0 => 6, 0x507E20 => 4, _ => throw new NotSupportedException("Native model loader lacks this callback's DAT layout.") };
        int start = Section(section);
        return new(Name + ":loader-textures", Bytes.Slice(start, Section(section + 1) - start));
    }

    public NativeBattleModelDatSource Relocate507400(uint callback, NativeBattleModelTextureLoad load)
    {
        byte[] bytes = Bytes.ToArray();
        int texture = callback switch { 0x5077B0 => 5, 0x5079B0 => 8, 0x507BF0 => 6, 0x507E20 => 4, _ => throw new NotSupportedException("Native model loader lacks this callback's DAT layout.") };
        load.Source.Data.AsSpan().CopyTo(bytes.AsSpan(Section(texture), Section(texture + 1) - Section(texture)));
        int geometry = Section(callback == 0x507E20 ? 0 : 1);
        int objects = Read32(geometry);
        if (objects is < 0 or > 4096)
        {
            throw new InvalidDataException("Native507550 geometry object directory is unbounded.");
        }
        for (int mesh = 0; mesh < objects; mesh++)
        {
            int cursor = checked(geometry + Read32(geometry + 4 + mesh * 4));
            int groups = Read16(cursor);
            cursor += 2;
            for (int group = 0; group < groups; group++)
            {
                int vertices = Read16(cursor + 2);
                cursor = checked(cursor + 4 + vertices * 6);
                Require(cursor, 0);
            }
            cursor = checked((cursor + 3) & ~3);
            int triangles = unchecked((short)Read16(cursor));
            int quads = unchecked((short)Read16(cursor + 2));
            cursor += 12;
            Rewrite(triangles, false);
            Rewrite(quads, true);

            void Rewrite(int count, bool quad)
            {
                int stride = quad ? 20 : 16;
                for (int face = 0; face < count; face++)
                {
                    Require(cursor, stride);
                    ImmutableArray<byte> relocated = NativeBattleRawGeometryPackets.Relocate507550(new(quad, bytes.AsSpan(cursor, stride).ToArray().ToImmutableArray()), load.Binding);
                    relocated.AsSpan().CopyTo(bytes.AsSpan(cursor, stride));
                    cursor += stride;
                }
            }
        }
        return this with { Bytes = bytes.ToImmutableArray() };

        int Read16(int offset)
        {
            Require(offset, 2);
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
        }
        int Read32(int offset)
        {
            Require(offset, 4);
            return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
        }
        void Require(int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > bytes.Length - length)
            {
                throw new InvalidDataException("Native507550 leaves its actual staging DAT.");
            }
        }
    }
}
