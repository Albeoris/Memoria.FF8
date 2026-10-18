using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterMeshShape(int Id, int FileOffset, ImmutableArray<byte> Bytes)
{
    [JsonIgnore]
    public int VertexCount => checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(0x18)));
    [JsonIgnore]
    public ImmutableArray<byte> Vertices => Bytes.AsSpan().Slice(checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(0x14))), checked(VertexCount * 8)).ToArray().ToImmutableArray();

    public ImmutableArray<NativeBattleWaterMeshGroup> Groups()
    {
        int cursor = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(Bytes.AsSpan().Slice(8)));
        ImmutableArray<NativeBattleWaterMeshGroup>.Builder groups = ImmutableArray.CreateBuilder<NativeBattleWaterMeshGroup>();
        while (true)
        {
            Require(cursor, 4);
            short count = BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(cursor + 2));
            if (count == -1)
            {
                return groups.ToImmutable();
            }
            short type = BinaryPrimitives.ReadInt16LittleEndian(Bytes.AsSpan().Slice(cursor));
            int stride = type switch
            {
                7 => 20,
                9 => 28,
                17 => 24,
                _ => throw new NotSupportedException("Native Water shape contains an unsupported selected face type.")
            };
            if (count <= 0)
            {
                throw new InvalidDataException("Native Water shape has an unsupported signed face count.");
            }
            cursor = checked(cursor + 4);
            Require(cursor, checked(count * stride));
            ImmutableArray<byte> faces = Bytes.AsSpan().Slice(cursor, count * stride).ToArray().ToImmutableArray();
            int indexOffset = type == 7 ? 12 : type == 9 ? 18 : 16;
            int corners = type == 17 ? 4 : 3;
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < corners; corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(faces.AsSpan().Slice(face * stride + indexOffset + corner * 2));
                    if ((vertex & 7) != 0 || vertex >= checked(VertexCount * 8))
                    {
                        throw new InvalidDataException("Native Water face selector leaves its shape's original vertex bank.");
                    }
                }
            }
            groups.Add(new(type, count, faces));
            cursor = checked(cursor + count * stride);
        }
    }

    internal void Require(int offset, int length)
    {
        if (Bytes.IsDefault || offset < 0 || length < 0 || offset > Bytes.Length - length)
        {
            throw new InvalidDataException("Native Water shape read leaves its selected resource extent.");
        }
    }
}

public static class NativeBattleWaterMeshes
{
    public static NativeBattleWaterMeshShape ReadShape(ImmutableArray<byte> currentFile0, int id)
    {
        if (currentFile0.IsDefault)
        {
            throw new NotSupportedException("Native Water selected shape requires its current file0 byte owner.");
        }
        return ReadShapeCore(currentFile0.Length, id, (offset, length) => currentFile0.AsSpan().Slice(offset, length).ToArray());
    }

    public static NativeBattleWaterMeshShape ReadShape(NativeBattleEffectArena arena, int file0Offset, int file0Length, int id)
    {
        if (file0Offset < 0 || file0Length < 0 || file0Offset > NativeBattleEffectArena.Length - file0Length)
        {
            throw new InvalidDataException("Native Water file0 leaves its actual resource arena.");
        }
        return ReadShapeCore(file0Length, id, (offset, length) => arena.Read(checked(file0Offset + offset), length));
    }

    private static NativeBattleWaterMeshShape ReadShapeCore(int fileLength, int id, Func<int, int, byte[]> readCurrent)
    {
        int length = id switch
        {
            2 => 0x218,
            4 => 0x2A48,
            13 => 0x5E4,
            _ => throw new NotSupportedException("Native Water shape is outside its three authenticated selected resources.")
        };
        int directory = checked((int)Dword(12));
        if (Dword(directory) != 14)
        {
            throw new InvalidDataException("Native Water shape directory contradicts its actual fourteen entries.");
        }
        int offset = checked(directory + (int)Dword(checked(directory + 4 + id * 4)));
        Require(offset, length);
        NativeBattleWaterMeshShape result = new(id, offset, readCurrent(offset, length).ToImmutableArray());
        result.Require(0, 0x1C);
        int vertexOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(result.Bytes.AsSpan().Slice(0x14)));
        result.Require(vertexOffset, checked(result.VertexCount * 8));
        result.Groups();
        return result;

        uint Dword(int offset)
        {
            Require(offset, 4);
            return BinaryPrimitives.ReadUInt32LittleEndian(readCurrent(offset, 4));
        }

        void Require(int offset, int count)
        {
            if (offset < 0 || count < 0 || offset > fileLength - count)
            {
                throw new InvalidDataException("Native Water shape directory leaves its current file0 owner.");
            }
        }
    }
}
