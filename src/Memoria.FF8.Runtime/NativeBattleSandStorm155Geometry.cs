using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSandStorm155Geometry
{
    public const uint SourceVertices = 0xDB8158;
    public const uint SelectedVertices = 0x23F2A70;
    public const uint SortedReferences = 0x23F33F0;
    public const int VertexCount = 304;

    public static NativeBattleSandStorm155Frame Sort607090(NativeBattleSandStorm155Frame frame, NativeBattleSandStorm155Resources resources)
    {
        byte[] rows = new byte[VertexCount * 12];
        for (int index = 0; index < VertexCount; index++)
        {
            int offset = index * 12;
            BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan(offset), checked(SourceVertices + (uint)(index * 8)));
            BinaryPrimitives.WriteUInt32LittleEndian(rows.AsSpan(offset + 4), checked(SelectedVertices + (uint)(index * 8)));
            BinaryPrimitives.WriteInt32LittleEndian(rows.AsSpan(offset + 8), SourceWord(resources, SourceVertices + (uint)(index * 8) + 2));
        }
        for (int first = 0; first < VertexCount - 1; first++)
        {
            for (int second = first + 1; second < VertexCount; second++)
            {
                int a = first * 12;
                int b = second * 12;
                if (BinaryPrimitives.ReadInt32LittleEndian(rows.AsSpan(a + 8)) < BinaryPrimitives.ReadInt32LittleEndian(rows.AsSpan(b + 8)))
                {
                    byte[] swap = rows.AsSpan(a, 12).ToArray();
                    rows.AsSpan(b, 12).CopyTo(rows.AsSpan(a, 12));
                    swap.CopyTo(rows.AsSpan(b, 12));
                }
            }
        }
        return frame with { State = frame.State.Write(SortedReferences, rows) };
    }

    public static (NativeBattleSandStorm155Frame Frame, int Result) Displace607130(NativeBattleSandStorm155Frame frame, uint address, NativeBattleSandStorm155Resources resources, uint graphicsFlags)
    {
        NativeBattleModulePoolTask task = NativeBattleSandStorm155Controller.Read(frame, address);
        int angle = unchecked((short)task.ReadWord(0x18));
        int amplitude = unchecked((short)task.ReadWord(0x1C));
        for (int group = 0; group < 19; group++)
        {
            int dx = unchecked(NativeBattleCameraMath.Cosine(angle) * amplitude) >> 12;
            int dz = unchecked(NativeBattleCameraMath.Sine(angle) * amplitude) >> 12;
            for (int vertex = 0; vertex < 16; vertex++)
            {
                uint rowAddress = checked(SortedReferences + (uint)((group * 16 + vertex) * 12));
                byte[] row = frame.State.ReadView(rowAddress, 8).Read(0, 8);
                uint source = BinaryPrimitives.ReadUInt32LittleEndian(row);
                uint selected = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4));
                if (source < SourceVertices || source - SourceVertices >= VertexCount * 8 || (source - SourceVertices) % 8 != 0)
                {
                    throw new InvalidDataException("Sand Storm sorted source leaves its actual packed vertex rows.");
                }
                if (selected < SelectedVertices || selected - SelectedVertices >= VertexCount * 8 || (selected - SelectedVertices) % 8 != 0)
                {
                    throw new InvalidDataException("Sand Storm selected vertex leaves its original addressed output bank.");
                }
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, unchecked((short)(SourceWord(resources, source) + dx)));
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), SourceWord(resources, source + 2));
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), unchecked((short)(SourceWord(resources, source + 4) + dz)));
                frame = frame with { State = frame.State.Write(selected, xyz) };
            }
            angle = unchecked(angle + 300);
            amplitude = unchecked(amplitude + 15);
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (NativeBattleSandStorm155Controller.Write(frame, address, task), unchecked((short)task.ReadWord(12)) >= 60 ? 2 : 0);
    }

    private static short SourceWord(NativeBattleSandStorm155Resources resources, uint address)
    {
        NativeBattleStaticMeshResource source = resources.Mesh;
        long offset = (long)address - source.Address;
        if (source.Address != 0xDB8150 || source.Bytes.IsDefault || offset < 8 || offset > source.Bytes.Length - 2 || address < SourceVertices || address - SourceVertices >= VertexCount * 8)
        {
            throw new InvalidDataException("Sand Storm vertex requires its original packed source owner.");
        }
        return BinaryPrimitives.ReadInt16LittleEndian(source.Bytes.AsSpan().Slice(checked((int)offset), 2));
    }
}
