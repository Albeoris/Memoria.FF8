using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleGravija214Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleGravija214Controller
{
    public static NativeBattleGravija214Frame PreprocessCopiedMesh69E940(NativeBattleGravija214Frame frame, NativeBattleGravija214Resources resources)
    {
        if (frame.State.ReadDword(CopiedMeshAddress + 4) != VertexCount)
        {
            throw new InvalidDataException("Gravija preprocessing requires its actual copied vertex count.");
        }
        for (int vertex = 0; vertex < VertexCount; vertex++)
        {
            uint row = CopiedMeshAddress + 8 + (uint)(vertex * 8);
            int x = unchecked((short)frame.State.ReadWord(row));
            int y = unchecked((short)frame.State.ReadWord(row + 2));
            int squared = unchecked(x * x + y * y);
            uint packed = 0;
            if (squared != 0)
            {
                uint raw = unchecked((uint)squared);
                uint leadingRaw = (uint)BitOperations.LeadingZeroCount(squared < 0 ? ~raw : raw);
                NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gravija preprocessing requires its current geometry owner.");
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = geometry with { Lzcs = raw, Lzcr = leadingRaw } } };
                int leading = (int)leadingRaw & ~1;
                int shift = leading - 24;
                int lookup = shift > 0 ? unchecked(squared << shift) : squared >> (24 - leading);
                if (lookup is < 64 or > 255)
                {
                    throw new NotSupportedException("Gravija radial source leaves its original normalized lookup domain.");
                }
                int radius = unchecked(resources.SqrtTable[lookup - 64] << ((31 - leading) >> 1)) >> 12;
                int factor = resources.SineTable[Math.Clamp(1024 - (radius >> 2), 0, 1023)] >> 2;
                int nx = unchecked(x * factor) / radius;
                int ny = unchecked(y * factor) / radius;
                packed = unchecked((uint)((radius & ~1) << 19)) | (unchecked((uint)nx) & 0x3FF) | ((unchecked((uint)ny) & 0x3FF) << 10);
            }
            frame = frame with { State = frame.State.WriteDword(row + 4, packed) };
        }
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(CopiedMeshAddress)).Write(4, BitConverter.GetBytes(CopiedMeshAddress + 8));
        uint cursor = CopiedMeshAddress + frame.State.ReadDword(CopiedMeshAddress) + 16;
        foreach (int corners in new[] { 3, 4 })
        {
            int count = checked((int)frame.State.ReadDword(cursor));
            cursor += 4;
            context = context.Write(32, BitConverter.GetBytes(cursor));
            for (int face = 0; face < count; face++)
            {
                ushort[] indices = new ushort[corners];
                uint[] colors = new uint[corners];
                short[] x = new short[corners];
                for (int corner = 0; corner < corners; corner++)
                {
                    indices[corner] = frame.State.ReadWord(cursor + 4 + (uint)(corner * 2));
                    colors[corner] = frame.State.ReadDword(cursor + (uint)(corner == 0 ? 0 : 8 + corner * 4));
                    uint row = CopiedMeshAddress + 8 + indices[corner] * 4u;
                    context = NativeBattleEffectNodeContextMemory.Copy(frame.State.ReadView(row, 8), 0, context, 52 + corner * 8, 8);
                    x[corner] = unchecked((short)frame.State.ReadWord(row));
                }
                int minimum = 0;
                for (int corner = 1; corner < corners; corner++)
                {
                    if (x[corner] < x[minimum])
                    {
                        minimum = corner;
                    }
                }
                int[] order = corners == 3 ? minimum switch { 1 => [1, 2, 0], 2 => [2, 0, 1], _ => [0, 1, 2] } : minimum switch { 1 => [1, 3, 0, 2], 2 => [2, 0, 3, 1], 3 => [3, 2, 1, 0], _ => [0, 1, 2, 3] };
                for (int corner = 0; corner < corners; corner++)
                {
                    ushort index = indices[order[corner]];
                    frame = frame with { State = frame.State.WriteWord(cursor + 4 + (uint)(corner * 2), unchecked((ushort)(index + (index >> 1)))) };
                    if (minimum != 0)
                    {
                        frame = frame with { State = frame.State.WriteDword(cursor + (uint)(corner == 0 ? 0 : 8 + corner * 4), colors[order[corner]]) };
                    }
                }
                cursor += (uint)(corners == 3 ? 20 : 24);
                context = context.Write(32, BitConverter.GetBytes(cursor));
            }
        }
        frame = PublishContext(frame, temporary, context, 88);
        return Release(frame, 88);
    }
}
