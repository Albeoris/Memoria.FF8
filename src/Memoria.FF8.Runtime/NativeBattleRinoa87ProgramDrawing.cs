using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87NodeDraw(ImmutableArray<byte> MorphScratch, NativeBattleGfGeometryBatch Packets);

/// <summary>7766B0's complete original node shape, transform, depth bias and scrolling-window operands.</summary>
public static class NativeBattleRinoa87ProgramDrawing
{
    public static NativeBattleRinoa87NodeDraw Draw7766B0(NativeBattleGfAnimationSource source, NativeBattleRinoa87ProgramNode callback, ImmutableArray<byte> morphScratch, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGfAnimationNode node = callback.Node;
        if (node.Data.IsDefault || node.Data.Length != 44 || morphScratch.IsDefault || morphScratch.Length != 1120 || callback.MorphScratch != 0x25B46A8)
        {
            throw new InvalidDataException("Native7766B0 requires its complete node and retained1120-byte shape workspace.");
        }
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(morphScratch, new([], 0));
        }
        int geometryIndex = unchecked((short)node.GeometryIndex);
        if (geometryIndex < 0 || geometryIndex >= ReadInt(0))
        {
            throw new InvalidDataException("Native7766B0 node geometry index leaves its original directory.");
        }
        int geometry = ReadInt(checked(8 + geometryIndex * 4));
        int vertices = ReadInt(checked(geometry + 4));
        int shapes = ReadInt(checked(geometry + 8));
        int first = node.Word40;
        int second = node.Word42;
        if (vertices < 1 || first < 0 || first >= shapes || second < 0 || second >= shapes)
        {
            throw new InvalidDataException("Native7766B0 node shape selectors leave their original vertex source.");
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
        (int x, int y, int z) = (node.Header & 0x200) != 0 ? (node.Vector8.X, node.Vector8.Y, node.Vector8.Z) : NativeBattleGfMatrices.Rotate56C3D0(context, node.Vector8);
        if ((node.Header & 0x200) == 0)
        {
            local = NativeBattleGfMatrices.Multiply56C090(context, local);
        }
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            local = NativeBattlePoseMatrices.Scale56BEF0(local, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
        }
        NativeBattleActionMatrix matrix = local with { X = unchecked(context.X + x), Y = unchecked(context.Y + y), Z = unchecked(context.Z + z) };
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        NativeBattleRinoa87MeshContext operands = new(flags, node.Color32, node.Scalar36, unchecked((short)callback.Task.ReadWord(136)), 17, 2);
        ImmutableArray<byte> mesh = source.Data.Slice(geometry, source.Data.Length - geometry);
        NativeBattleGfGeometryBatch packets = NativeBattleRinoa87MeshPackets.EmitBatch776920(mesh, matrix, projection, operands, colors, offset =>
        {
            if (offset < 0 || offset > selected.Length - 6)
            {
                throw new InvalidDataException("Native7766B0 face leaves its selected current vertex source.");
            }
            ReadOnlySpan<byte> point = selected.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point[2..]), BinaryPrimitives.ReadInt16LittleEndian(point[4..]));
        }, pilotIdentity);
        return new(morphScratch, packets);

        ReadOnlySpan<byte> Read(int offset, int count)
        {
            if (source.Data.IsDefault || offset < 0 || count < 0 || offset > source.Data.Length - count)
            {
                throw new InvalidDataException("Native7766B0 read leaves its actual programme bytes.");
            }
            return source.Data.AsSpan().Slice(offset, count);
        }
        int ReadInt(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Read(offset, 4));
    }
}
