using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88NodeDraw(ImmutableArray<byte> MorphScratch, NativeBattleGfGeometryBatch Packets);

/// <summary>76A380's complete original node shape, transform, depth bias and scrolling-window operands.</summary>
public static class NativeBattleRinoa88ProgramDrawing
{
    public static NativeBattleRinoa88NodeDraw Draw76A380(NativeBattleGfAnimationSource source, NativeBattleRinoa88ProgramNode callback, ImmutableArray<byte> morphScratch, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleGfAnimationNode node = callback.Node;
        if (node.Data.IsDefault || node.Data.Length != 44 || morphScratch.IsDefault || morphScratch.Length != 1440 || callback.MorphScratch != 0x25AC750)
        {
            throw new InvalidDataException("Native76A380 requires its complete node and retained1440-byte shape workspace.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(morphScratch, new([], 0));
        }
        int geometryIndex = unchecked((short)node.GeometryIndex);
        if (geometryIndex < 0 || geometryIndex >= ReadInt(0))
        {
            throw new InvalidDataException("Native76A380 node geometry index leaves its original directory.");
        }
        int geometry = ReadInt(checked(8 + geometryIndex * 4));
        int vertices = ReadInt(checked(geometry + 4));
        int shapes = ReadInt(checked(geometry + 8));
        int first = node.Word40;
        int second = node.Word42;
        if (vertices < 1 || first < 0 || first >= shapes || second < 0 || second >= shapes)
        {
            throw new InvalidDataException("Native76A380 node shape selectors leave their original vertex source.");
        }
        ImmutableArray<byte> selected;
        if (first != second && node.Scalar38 is not (0 or 4096))
        {
            morphScratch = NativeBattleGfMorphGeometry.Morph701390(source.Data, geometry, first, second, node.Scalar38, morphScratch);
            selected = morphScratch;
        }
        else
        {
            int shape = first != second && node.Scalar38 == 4096 ? second : first;
            int start = checked(geometry + 12 + shape * vertices * 8);
            selected = Read(start, checked(vertices * 8)).ToArray().ToImmutableArray();
        }
        NativeBattleActionMatrix context = callback.Context;
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16);
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
        local = NativeBattleGfMatrices.Multiply56C090(context, local);
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([unchecked((short)node.Vector24.X), 0, 0, 0, unchecked((short)node.Vector24.Y), 0, 0, 0, unchecked((short)node.Vector24.Z)], 0, 0, 0, 4096);
                local = NativeBattleGfMatrices.Multiply56C090(local, diagonal);
            }
            else
            {
                local = NativeBattlePoseMatrices.Scale56BEF0(local, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
            }
        }
        NativeBattleActionMatrix matrix = local with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        bool scroll = callback.Task.ReadWord(128) == 1 && geometryIndex == 0;
        NativeBattleRinoa88MeshContext operands = new(flags, node.Color32, node.Scalar36, unchecked((short)callback.Task.ReadWord(136)), 0, scroll ? callback.Task.ReadWord(142) : (ushort)0, full, scroll ? new(0, 0, 128, 128) : full, 17, 2);
        ImmutableArray<byte> mesh = source.Data.Slice(geometry, source.Data.Length - geometry);
        NativeBattleGfGeometryBatch packets = NativeBattleRinoa88MeshPackets.EmitBatch76A650(mesh, matrix, projection, operands, colors, offset =>
        {
            if (offset < 0 || offset > selected.Length - 6)
            {
                throw new InvalidDataException("Native76A380 face leaves its selected current vertex source.");
            }
            ReadOnlySpan<byte> point = selected.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
        });
        return new(morphScratch, packets);

        ReadOnlySpan<byte> Read(int offset, int count)
        {
            if (source.Data.IsDefault || offset < 0 || count < 0 || offset > source.Data.Length - count)
            {
                throw new InvalidDataException("Native76A380 read leaves its actual programme bytes.");
            }
            return source.Data.AsSpan().Slice(offset, count);
        }
        int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
    }
}
