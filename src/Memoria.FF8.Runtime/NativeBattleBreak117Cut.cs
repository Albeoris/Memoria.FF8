using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreak117CutResult(NativeBattleGfGeometryBatch Packets, ImmutableArray<byte> Projected);

public static class NativeBattleBreak117Cut
{
    public sealed record Vertex(NativeBattleVector Position, uint Screen, ushort Depth, byte Flags, ushort Uv, uint Color);

    public static Vertex Edge(Vertex a, Vertex b, int cut, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection)
    {
        int dy = b.Position.Y - a.Position.Y;
        if (dy == 0)
        {
            return a;
        }
        int factor = unchecked((cut - a.Position.Y) << 16) / dy;
        int Blend(int first, int second) => unchecked(unchecked((second - first) * factor) >> 16) + first;
        NativeBattleVector point = new(unchecked((short)Blend(a.Position.X, b.Position.X)), unchecked((short)cut), unchecked((short)Blend(a.Position.Z, b.Position.Z)));
        NativeBattleModuleProjectedVertex p = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
        ushort uv = (ushort)(unchecked((byte)Blend(a.Uv & 255, b.Uv & 255)) | unchecked((byte)Blend(a.Uv >> 8, b.Uv >> 8)) << 8);
        uint color = 0;
        for (int channel = 0; channel < 3; channel++)
        {
            color |= (uint)unchecked((byte)Blend((int)(a.Color >> (channel * 8)) & 255, (int)(b.Color >> (channel * 8)) & 255)) << (channel * 8);
        }
        // The original edge writer replaces screenXY but retains its destination depth/flags bytes.
        return new(point, p.Screen, 0, 0, uv, color);
    }

    public static NativeBattleBreak117CutResult Draw(ImmutableArray<byte> source, int geometry, ImmutableArray<byte> selected, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int cut, ImmutableArray<byte> retainedProjected)
    {
        int count = I32(geometry + 4);
        if (selected.IsDefault || count < 0 || count > 512 || selected.Length < count * 8)
        {
            throw new InvalidDataException("Native Break shell cut requires its complete selected vertex bank.");
        }
        if (retainedProjected.IsDefault || retainedProjected.Length < count * 8)
        {
            throw new InvalidDataException("Native Break requires its actual retained projected scratch bytes.");
        }
        byte[] projectedBytes = retainedProjected.AsSpan().Slice(0, count * 8).ToArray();
        Vertex[] vertices = new Vertex[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> raw = selected.AsSpan().Slice(index * 8);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw[2..]), BinaryPrimitives.ReadInt16LittleEndian(raw[4..]));
            NativeBattleModuleProjectedVertex p = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
            ushort depth = p.ReadDepth45E250();
            byte flags = Clip(p with { Depth = depth });
            vertices[index] = new(point, p.Screen, depth, flags, 0, 0);
            Span<byte> output = projectedBytes.AsSpan(index * 8, 8);
            BinaryPrimitives.WriteUInt32LittleEndian(output, p.Screen);
            BinaryPrimitives.WriteUInt16LittleEndian(output[4..], depth);
            output[6] = flags;
            if (flags != 16)
            {
                output[7] = point.Y <= cut ? (byte)1 : (byte)2;
            }
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int cursor = geometry + I32(geometry);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int kind = 0; kind < 8; kind++)
        {
            int faces = I32(cursor);
            cursor += 4;
            if (faces < 0 || faces > 4096 || faces != 0 && kind is not (2 or 6))
            {
                throw new InvalidDataException("Native Break cut model requires its original FT3/GT3 groups.");
            }
            for (int face = 0; face < faces; face++)
            {
                ReadOnlySpan<byte> record = source.AsSpan().Slice(cursor, strides[kind]);
                bool gouraud = kind == 6;
                Vertex[] corners = new Vertex[3];
                for (int corner = 0; corner < 3; corner++)
                {
                    ushort offset = BinaryPrimitives.ReadUInt16LittleEndian(record[(4 + corner * 2)..]);
                    if ((offset & 1) != 0 || offset / 2 >= vertices.Length)
                    {
                        throw new InvalidDataException("Native Break cut triangle leaves its actual selected vertex bank.");
                    }
                    int uvOffset = corner == 0 ? 12 : corner == 1 ? 16 : 10;
                    uint color = gouraud ? BinaryPrimitives.ReadUInt32LittleEndian(record[(corner == 0 ? 0 : corner == 1 ? 20 : 24)..]) : 0;
                    corners[corner] = vertices[offset / 2] with { Uv = BinaryPrimitives.ReadUInt16LittleEndian(record[uvOffset..]), Color = gouraud ? color : 0 };
                }
                int any = corners[0].Flags | corners[1].Flags | corners[2].Flags;
                int shared = corners[0].Flags & corners[1].Flags & corners[2].Flags;
                if (any < 16 && shared == 0 && NativeBattleModulePackets.Nclip(corners[0].Screen, corners[1].Screen, corners[2].Screen) >= 0)
                {
                    Vertex[] kept = corners.Where(vertex => vertex.Position.Y <= cut).ToArray();
                    if (kept.Length > 0)
                    {
                        Vertex[] emitted;
                        if (kept.Length == 3)
                        {
                            emitted = corners;
                        }
                        else
                        {
                            Vertex[] removed = corners.Where(vertex => vertex.Position.Y > cut).ToArray();
                            emitted = kept.Length == 1 ? [kept[0], Edge(kept[0], removed[0], cut, matrix, projection), Edge(kept[0], removed[1], cut, matrix, projection)] : [kept[0], kept[1], Edge(kept[0], removed[0], cut, matrix, projection), Edge(kept[1], removed[0], cut, matrix, projection)];
                        }
                        bool quad = emitted.Length == 4;
                        SkinWrites writes = new(gouraud ? quad ? 52 : 40 : quad ? 40 : 32, allocated);
                        writes.Word(0, gouraud ? quad ? 0xC000000u : 0x9000000u : 0x7000000u);
                        uint command = BinaryPrimitives.ReadUInt32LittleEndian(record);
                        for (int corner = 0; corner < emitted.Length; corner++)
                        {
                            int screen = gouraud ? 8 + corner * 12 : 8 + corner * 8;
                            writes.Word(screen, emitted[corner].Screen);
                            if (kept.Length == 3 && corner == 2)
                            {
                                writes.Word(screen + 4, emitted[corner].Uv);
                            }
                            else
                            {
                                writes.Word16(screen + 4, emitted[corner].Uv);
                            }
                            if (gouraud)
                            {
                                uint rgb = emitted[corner].Color;
                                if (corner == 0)
                                {
                                    rgb = (rgb & 0xFFFFFF) | (quad ? 0x3C000000u : 0x34000000u);
                                }
                                writes.Word(screen - 4, kept.Length == 3 ? corner == 0 ? command : rgb : rgb);
                            }
                        }
                        if (!gouraud)
                        {
                            writes.Word(4, command);
                        }
                        writes.Word16(14, BinaryPrimitives.ReadUInt16LittleEndian(record[14..]));
                        writes.Word16(gouraud ? 26 : 22, BinaryPrimitives.ReadUInt16LittleEndian(record[18..]));
                        uint average = unchecked((uint)corners.Sum(vertex => (int)vertex.Depth) * 341u) >> 12;
                        ImmutableArray<int> depth = gouraud ? [corners[0].Depth, corners[1].Depth, corners[2].Depth, quad ? corners[0].Depth : 0] : [corners[0].Depth * 4, corners[1].Depth * 4, corners[2].Depth * 4, quad ? corners[0].Depth * 4 : 0];
                        NativeBattleGeometryPrimitive primitive = new(checked(17 + (int)average), writes.Words(), depth);
                        NativeBattleGfGeometryAttempt attempt = writes.Attempt(primitive);
                        attempts.Add(attempt);
                        allocated += attempt.PacketLength;
                    }
                }
                cursor += strides[kind];
            }
        }
        return new(new(attempts.ToImmutable(), allocated), projectedBytes.ToImmutableArray());

        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(offset));
    }

    private static byte Clip(NativeBattleModuleProjectedVertex vertex)
    {
        if (unchecked((short)vertex.Depth) <= 0)
        {
            return 16;
        }
        short x = unchecked((short)vertex.Screen);
        short y = unchecked((short)(vertex.Screen >> 16));
        return (byte)((x < 0 ? 1 : x >= 2560 ? 2 : 0) | (y < 0 ? 4 : y >= 1728 ? 8 : 0));
    }
}
