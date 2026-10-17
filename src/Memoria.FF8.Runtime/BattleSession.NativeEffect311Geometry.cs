using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect311Bounds(short MinX, short MinY, short MaxX, short MaxY, int MinimumDepth, bool Visible)
{
    public static NativeBattleEffect311Bounds Initial => new(32767, 32767, -32767, -32767, 65536, false);
}

public sealed record NativeBattleEffect311ScreenMeshSegment(NativeBattleGfGeometryBatch Batch, ushort Flags20);
public sealed record NativeBattleEffect311ScreenMeshBatch(ImmutableArray<NativeBattleEffect311ScreenMeshSegment> Segments);

/// <summary>666320's copied mesh fixups,6661D0's target bounds and665D50's pixel-coordinate Gouraud mesh.</summary>
public static class NativeBattleEffect311Geometry
{
    public static ImmutableArray<byte> InitializeMesh(NativeBattleEffect311Resources resources)
    {
        return InitializeMesh(resources.Read(0x101B0E8, 0x1DD0), resources.Read);
    }

    public static ImmutableArray<byte> InitializeMesh(ImmutableArray<byte> original, Func<uint, int, ImmutableArray<byte>> readResource)
    {
        byte[] mesh = original.IsDefault ? throw new InvalidDataException("Native666320 requires its actual copied mesh bytes.") : original.ToArray();
        int count = I32(mesh, 4);
        if (count <= 0 || count > (mesh.Length - 8) / 8)
        {
            throw new InvalidDataException("Native666380 vertex loop exceeds its actual copied mesh.");
        }
        for (int vertex = 0; vertex < count; vertex++)
        {
            int offset = checked(8 + vertex * 8);
            int x = I16(mesh, offset);
            int y = I16(mesh, offset + 2);
            int squared = unchecked(x * x + y * y);
            uint packed;
            if (squared == 0)
            {
                packed = 0;
            }
            else
            {
                int leading = BitOperations.LeadingZeroCount(squared < 0 ? ~unchecked((uint)squared) : (uint)squared) & ~1;
                int shift = leading - 24;
                int lookup = shift > 0 ? unchecked(squared << shift) : squared >> (24 - leading);
                ushort seed = BinaryPrimitives.ReadUInt16LittleEndian(Resource(readResource, unchecked((uint)(0x1020FC8 + lookup * 2)), 2));
                int radius = unchecked(seed << ((31 - leading) >> 1)) >> 12;
                packed = unchecked((uint)((radius & ~1) << 19)) | U32(mesh, offset + 4) & 0xFFFFF;
                int index = Math.Clamp(1024 - (radius >> 2), 0, 1023);
                int factor = BinaryPrimitives.ReadInt16LittleEndian(Resource(readResource, checked((uint)(0x10211C8 + index * 2)), 2)) >> 2;
                int normalX = unchecked(x * factor) / radius;
                int normalY = unchecked(y * factor) / radius;
                packed = packed & 0xFFF00000 | (uint)normalX & 0x3FF | ((uint)normalY & 0x3FF) << 10;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(offset + 4), packed);
        }
        int cursor = checked(I32(mesh, 0) + 16);
        for (int group = 0; group < 2; group++)
        {
            int polygons = I32(mesh, cursor);
            cursor = checked(cursor + 4);
            int corners = group == 0 ? 3 : 4;
            int length = group == 0 ? 20 : 24;
            for (int polygon = 0; polygon < polygons; polygon++)
            {
                Span<byte> face = Slice(mesh, cursor, length);
                ushort[] indices = Enumerable.Range(0, corners).Select(corner => U16(mesh, cursor + 4 + corner * 2)).ToArray();
                uint[] colors = Enumerable.Range(0, corners).Select(corner => U32(mesh, cursor + ColorOffset(corner))).ToArray();
                int minimum = 0;
                for (int corner = 1; corner < corners; corner++)
                {
                    if (I16(mesh, checked(8 + indices[corner] * 4)) < I16(mesh, checked(8 + indices[minimum] * 4)))
                    {
                        minimum = corner;
                    }
                }
                int[] order = corners == 3 ? minimum switch { 1 => [1, 2, 0], 2 => [2, 0, 1], _ => [0, 1, 2] } : minimum switch { 1 => [1, 3, 0, 2], 2 => [2, 0, 3, 1], 3 => [3, 2, 1, 0], _ => [0, 1, 2, 3] };
                for (int corner = 0; corner < corners; corner++)
                {
                    ushort index = indices[order[corner]];
                    BinaryPrimitives.WriteUInt16LittleEndian(face[(4 + corner * 2)..], unchecked((ushort)(index + (index >> 1))));
                    BinaryPrimitives.WriteUInt32LittleEndian(face[ColorOffset(corner)..], colors[order[corner]]);
                }
                cursor = checked(cursor + length);
            }
        }
        return mesh.ToImmutableArray();
    }

    public static NativeBattleEffect311Bounds IncludeBounds6661D0(NativeBattleEffect311Bounds bounds, NativeBattleGraphicsModel model, NativeBattleActionMatrix view, NativeBattleCameraProjection projection)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native6661D0 requires its actual model position and scale WORDs.");
        NativeBattleVector bottom = transform.Position28;
        short height = unchecked((short)(transform.Scale38 + 256));
        NativeBattleVector top = bottom with { Y = unchecked((short)(bottom.Y - height)) };
        int radius = 0;
        for (int point = 0; point < 2; point++)
        {
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(view, point == 0 ? top : bottom, projection);
            short x = (short)(unchecked((short)projected.Screen) >> 3);
            short y = (short)(unchecked((short)(projected.Screen >> 16)) >> 3);
            short depth = unchecked((short)projected.Depth);
            if (point == 0)
            {
                radius = 54 * height / (depth == 0 ? 1 : depth);
            }
            if (depth > 0)
            {
                short left = unchecked((short)(x - radius));
                short right = unchecked((short)(left + radius * 2));
                bounds = bounds with
                {
                    MinX = Math.Min(bounds.MinX, Math.Min(left, right)),
                    MaxX = Math.Max(bounds.MaxX, Math.Max(left, right)),
                    MinY = Math.Min(bounds.MinY, y),
                    MaxY = Math.Max(bounds.MaxY, y),
                    MinimumDepth = Math.Min(bounds.MinimumDepth, depth),
                    Visible = true
                };
            }
        }
        return bounds;
    }

    public static NativeBattleEffect311ScreenMeshBatch Emit665D50(ImmutableArray<byte> mesh, NativeBattleActionMatrix screenMatrix, int bucket, int colorFactor, NativeBattleSummonColors colors)
    {
        screenMatrix.Validate();
        if (mesh.IsDefault || screenMatrix.Rotation[2] != 0 || screenMatrix.Rotation[5] != 0 || screenMatrix.Rotation[8] != 0)
        {
            throw new NotSupportedException("Native665D50 requires its actual two-dimensional screen matrix and initialized copied mesh.");
        }
        byte[] source = mesh.ToArray();
        int count = I32(source, 4);
        if (count <= 0 || count > (source.Length - 8) / 8)
        {
            throw new InvalidDataException("Native665D50 vertex loop exceeds its actual mesh.");
        }
        uint[] screens = new uint[count];
        for (int vertex = 0; vertex < count; vertex++)
        {
            NativeBattleVector position = new(I16(source, 8 + vertex * 8), I16(source, 10 + vertex * 8), 0);
            NativeBattleVector result = NativeBattleGfMatrices.Transform460840(screenMatrix, position);
            short x = (short)(result.X >> 3);
            short y = (short)(result.Y >> 3);
            //The original X clamp branches bypass Y clamping for that vertex.
            if (x < 0)
            {
                x = 0;
            }
            else if (x >= 320)
            {
                x = 319;
            }
            else if (y < 0)
            {
                y = 0;
            }
            else if (y >= 216)
            {
                y = 215;
            }
            screens[vertex] = unchecked((ushort)x) | unchecked((uint)y << 16);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int cursor = 0;
        int positionOffset = checked(I32(source, 0) + 16);
        for (int group = 0; group < 2; group++)
        {
            int polygons = I32(source, positionOffset);
            positionOffset = checked(positionOffset + 4);
            int corners = group == 0 ? 3 : 4;
            int faceLength = group == 0 ? 20 : 24;
            for (int polygon = 0; polygon < polygons; polygon++)
            {
                Slice(source, positionOffset, faceLength);
                uint[] words = new uint[corners * 2 + 1];
                words[0] = (uint)(words.Length - 1) << 24;
                for (int corner = 0; corner < corners; corner++)
                {
                    int index = U16(source, positionOffset + 4 + corner * 2);
                    if (index % 3 != 0 || index / 3 >= count)
                    {
                        throw new InvalidDataException("Native665F50/666070 polygon exceeds its initialized twelve-byte vertex scratch.");
                    }
                    words[corner * 2 + 1] = colors.Interpolate(U32(source, positionOffset + ColorOffset(corner)), colorFactor, 0);
                    words[corner * 2 + 2] = screens[index / 3];
                }
                words[1] = words[1] & 0xFFFFFF | (group == 0 ? 0x32000000u : 0x3A000000u);
                attempts.Add(Attempt(cursor, words.ToImmutableArray(), bucket));
                cursor = checked(cursor + words.Length * 4);
                positionOffset = checked(positionOffset + faceLength);
            }
        }
        NativeBattleGfGeometryBatch body = new(attempts.ToImmutable(), cursor);
        NativeBattleGfGeometryBatch before = new([Attempt(0, [0x1000000, 0xE1000220], bucket)], 8);
        NativeBattleGfGeometryBatch after = new([Attempt(0, [0x1000000, 0xE1000240], bucket)], 8);
        return new([new(before, 0), new(body, 1), new(after, 0)]);
    }

    private static NativeBattleGfGeometryAttempt Attempt(int offset, ImmutableArray<uint> words, int bucket)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, [0, 0, 0, 0]) { PhysicalWriteMask = mask };
        return new(offset, bytes.ToImmutableArray(), mask, primitive);
    }

    private static int ColorOffset(int corner) => corner == 0 ? 0 : 8 + corner * 4;
    private static short I16(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(Slice(bytes, offset, 2));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Slice(bytes, offset, 2));
    private static int I32(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Slice(bytes, offset, 4));
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Slice(bytes, offset, 4));

    private static Span<byte> Slice(byte[] bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Native effect311 geometry access leaves its actual copied mesh.");
        }
        return bytes.AsSpan(offset, length);
    }

    private static ReadOnlySpan<byte> Resource(Func<uint, int, ImmutableArray<byte>> read, uint address, int length)
    {
        ImmutableArray<byte> bytes = read(address, length);
        if (bytes.IsDefault || bytes.Length != length)
        {
            throw new InvalidDataException("Native666380 lookup did not preserve its exact WORD load.");
        }
        return bytes.AsSpan();
    }
}
