using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleTornado146GroundVertex(NativeBattleVector Position, short Weight);

/// <summary>The crater's retained centroid-cell lists and signed triangle tests.</summary>
public static class NativeBattleTornado146Ground
{
    public static NativeBattleTornado146State Initialize615160(NativeBattleTornado146State state, NativeBattleStaticMeshResource mesh, Action<int, uint>? storeContext = null)
    {
        NativeBattleTornado146Work work = new(state);
        for (uint address = 0x2491BC0; address < 0x2491CE0; address += 4)
        {
            work.WriteDword(address, 1);
        }
        work.WriteDword(0x2491E4C, 0);
        for (uint address = 0x2491E50; address < 0x24936C8; address += 12)
        {
            work.WriteDword(address, 1);
        }
        int faces = ReadInt(mesh, 0);
        storeContext?.Invoke(0x34, checked(mesh.Address + 8));
        storeContext?.Invoke(0x38, checked(mesh.Address + (uint)faces));
        int count = ReadInt(mesh, faces);
        storeContext?.Invoke(0x38, checked(mesh.Address + (uint)faces + 4));
        if (count <= 0)
        {
            return work.Freeze();
        }
        if (count > (0x24936C8 - 0x2491E50) / 12)
        {
            throw new InvalidDataException("Native crater cell records exceed their original retained bank.");
        }
        work.WriteDword(0x2491E4C, (uint)count);
        for (int index = 0; index < count; index++)
        {
            int face = checked(faces + 4 + index * 12);
            NativeBattleVector a = TriangleVertex(mesh, face, 0);
            NativeBattleVector b = TriangleVertex(mesh, face, 1);
            NativeBattleVector c = TriangleVertex(mesh, face, 2);
            for (int corner = 0; corner < 3; corner++)
            {
                NativeBattleVector point = corner == 0 ? a : corner == 1 ? b : c;
                storeContext?.Invoke(0x68 + corner * 8, (ushort)point.X | ((uint)(ushort)point.Y << 16));
                storeContext?.Invoke(0x6C + corner * 8, (ushort)point.Z | ((uint)TriangleVertexW(mesh, face, corner) << 16));
            }
            int x = ((a.X + b.X + c.X) / 3 + 0x8D8) / 512;
            int z = ((a.Z + b.Z + c.Z) / 3 + 0x80E) / 512;
            uint cell = checked(0x2491BC0u + (uint)(x + z * 9) * 4);
            uint record = checked(0x2491E50u + (uint)index * 12);
            uint previous = work.Dword(cell);
            if (previous != 1)
            {
                work.WriteDword(record, previous);
            }
            work.WriteDword(cell, record);
            work.WriteDword(record + 4, checked(mesh.Address + (uint)face));
            work.WriteDword(record + 8, 0);
        }
        storeContext?.Invoke(0x38, checked(mesh.Address + (uint)faces + 4 + (uint)count * 12));
        return work.Freeze();
    }

    public static NativeBattleTornado146GroundVertex Deform6167E0(NativeBattleTornado146Work work, NativeBattleStaticMeshResource mesh, NativeBattleVector vertex, NativeBattleVector center, int fade, Action<int, uint>? storeContext = null)
    {
        int x = vertex.X - center.X;
        int z = vertex.Z - center.Z;
        int column = (x + 0x8D8) / 512;
        int row = (z + 0x80E) / 512;
        storeContext?.Invoke(0x60, 0);
        for (int iz = Math.Clamp(row - 1, 0, 7); iz < Math.Clamp(row + 2, 0, 7); iz++)
        {
            for (int ix = Math.Clamp(column - 1, 0, 8); ix < Math.Clamp(column + 2, 0, 8); ix++)
            {
                uint record = work.Dword(checked(0x2491BC0u + (uint)(ix + iz * 9) * 4));
                int traversed = 0;
                while (record != 1)
                {
                    if (record < 0x2491E50 || record >= 0x24936C8 || (record - 0x2491E50) % 12 != 0 || ++traversed > (0x24936C8 - 0x2491E50) / 12)
                    {
                        throw new InvalidDataException("Native crater requires its actual bounded linked triangle cells.");
                    }
                    int face = checked((int)(work.Dword(record + 4) - mesh.Address));
                    NativeBattleVector a = TriangleVertex(mesh, face, 2);
                    NativeBattleVector b = TriangleVertex(mesh, face, 1);
                    NativeBattleVector c = TriangleVertex(mesh, face, 0);
                    StoreVertex(0x68, a, face, 2);
                    StoreVertex(0x70, b, face, 1);
                    StoreVertex(0x78, c, face, 0);
                    int first = unchecked((z - a.Z) * (b.X - a.X) - (x - a.X) * (b.Z - a.Z));
                    int second = unchecked((z - b.Z) * (c.X - b.X) - (x - b.X) * (c.Z - b.Z));
                    int third = unchecked((z - c.Z) * (a.X - c.X) - (x - c.X) * (a.Z - c.Z));
                    storeContext?.Invoke(0x94, unchecked((uint)first));
                    storeContext?.Invoke(0xA4, unchecked((uint)second));
                    storeContext?.Invoke(0xB4, unchecked((uint)third));
                    if (first == 0)
                    {
                        first = -1;
                        storeContext?.Invoke(0x94, uint.MaxValue);
                    }
                    if (second == 0)
                    {
                        second = -1;
                        storeContext?.Invoke(0xA4, uint.MaxValue);
                    }
                    if (third == 0)
                    {
                        third = -1;
                        storeContext?.Invoke(0xB4, uint.MaxValue);
                    }
                    if ((first & second & third) < 0)
                    {
                        short height = unchecked((short)(unchecked((a.Y + b.Y + c.Y) / 3 * fade) >> 12));
                        storeContext?.Invoke(0x60, 1);
                        return new(vertex with { Y = height }, unchecked((short)((height << 12) / 1465)));
                    }
                    record = work.Dword(record);
                }
            }
        }
        return new(vertex, 0);

        void StoreVertex(int offset, NativeBattleVector point, int face, int corner)
        {
            storeContext?.Invoke(offset, (ushort)point.X | ((uint)(ushort)point.Y << 16));
            storeContext?.Invoke(offset + 4, (ushort)point.Z | ((uint)TriangleVertexW(mesh, face, corner) << 16));
        }
    }

    public static NativeBattleCameraProjection CaptureProjection(NativeBattleVector center, ushort focal)
    {
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(rotation, 0x1000000 / (0x10C0000 / 256), 0, 0x1000000 / (0xFED000 / 256));
        return new(matrix with { X = 7 - center.X / 17, Y = center.Z / 16 - 1, Z = unchecked((short)focal) }, focal, 128, 128);
    }

    private static NativeBattleVector TriangleVertex(NativeBattleStaticMeshResource mesh, int face, int corner)
    {
        int vertex = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(checked(face + 4 + corner * 2), 2)) * 4);
        return new(BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(vertex)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(vertex + 2)), BinaryPrimitives.ReadInt16LittleEndian(mesh.Bytes.AsSpan().Slice(vertex + 4)));
    }

    private static ushort TriangleVertexW(NativeBattleStaticMeshResource mesh, int face, int corner)
    {
        int vertex = checked(8 + BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(checked(face + 4 + corner * 2), 2)) * 4);
        return BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(vertex + 6));
    }

    private static int ReadInt(NativeBattleStaticMeshResource mesh, int offset) => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(offset, 4));
}
