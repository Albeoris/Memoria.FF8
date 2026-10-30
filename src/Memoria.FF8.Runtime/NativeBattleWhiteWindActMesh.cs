using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWhiteWindActMeshStep(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Temporary88, NativeBattleEffectArena Arena, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattleWhiteWindActMesh
{
    public static bool Visible(NativeBattleGfAnimationNode node) => NativeBattleActMeshRendering.Visible(node);

    public static NativeBattleWhiteWindActMeshStep Draw(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context40, NativeBattlePackedMatrix32 billboard, NativeBattleEffectArena temporary88, NativeBattleEffectArena arena, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf4, NativeBattleSummonColors constants, short? localVectorW = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (!Visible(node))
        {
            throw new InvalidOperationException("White Wind ACT mesh allocation requires a visible node.");
        }
        int geometry = I32(source.Data, checked(8 + node.GeometryIndex * 4));
        int count = I32(source.Data, checked(geometry + 4));
        if (count < 1 || count > (NativeBattleWhiteWind297Profile.BindingOffset - NativeBattleWhiteWind297Profile.MorphOffset) / 8)
        {
            throw new InvalidDataException("White Wind ACT shape leaves its original morph-to-packet boundary.");
        }
        int first = checked(geometry + 12 + node.Word40 * count * 8);
        int second = checked(geometry + 12 + node.Word42 * count * 8);
        bool interpolate = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? second : first;
        uint morph = Dword(context40, 0x24);
        if (morph != NativeBattleWhiteWind297Profile.ArenaAddress + NativeBattleWhiteWind297Profile.MorphOffset)
        {
            throw new InvalidDataException("White Wind ACT morph pointer does not identify its actual shared working arena.");
        }
        int morphOffset = NativeBattleWhiteWind297Profile.MorphOffset;
        uint selectedAddress;
        if (interpolate)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector a = Vector(source.Data, checked(first + vertex * 8));
                NativeBattleVector b = Vector(source.Data, checked(second + vertex * 8));
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - node.Scalar38)), Ir = [unchecked((ushort)a.X), unchecked((ushort)a.Y), unchecked((ushort)a.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)node.Scalar38), Ir = [unchecked((ushort)b.X), unchecked((ushort)b.Y), unchecked((ushort)b.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, constants);
                byte[] xyz = new byte[6];
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
                arena = arena.Write(checked(morphOffset + vertex * 8), xyz);
            }
            selectedAddress = morph;
        }
        else
        {
            _ = Slice(source.Data, selected, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)selected);
        }
        (byte[] rootBytes, bool[] rootKnown) = context40.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 root = new(rootBytes.ToImmutableArray(), rootKnown.ToImmutableArray());
        NativeBattleActionMatrix context = root.ReadMatrix();
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (int X, int Y, int Z) point;
        if ((node.Header & 0x1000) != 0)
        {
            registers = Transform(registers, billboard, node.Vector8, localVectorW);
            point = Mac(registers);
            if ((node.Header & 0x8000) == 0)
            {
                matrix = NativeBattleGfMatrices.Multiply56C090(billboard.ReadMatrix(), matrix);
            }
        }
        else if ((node.Header & 0x200) != 0)
        {
            point = (node.Vector8.X, node.Vector8.Y, node.Vector8.Z);
        }
        else
        {
            registers = Transform(registers, root, node.Vector8, localVectorW);
            point = Mac(registers);
            if ((node.Header & 0x8000) == 0)
            {
                matrix = NativeBattleGfMatrices.Multiply56C090(context, matrix);
            }
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        matrix = matrix with { X = unchecked(context.X + point.X), Y = unchecked(context.Y + point.Y), Z = unchecked(context.Z + point.Z) };
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        uint shapeAddress = checked(source.Address + (uint)geometry);
        temporary88 = Store(temporary88, 0, shapeAddress);
        temporary88 = Store(temporary88, 4, selectedAddress);
        temporary88 = Store(temporary88, 12, unchecked((uint)(int)node.Scalar36));
        temporary88 = Store(temporary88, 0x1C, node.Scalar36 != 0 ? 0x20F0u : 0x2030u);
        if (node.Scalar36 != 0)
        {
            temporary88 = Store(temporary88, 8, node.Color32);
        }
        temporary88 = Store(temporary88, 0x18, Dword(context40, 0x20));
        int end = checked(geometry + I32(source.Data, geometry));
        foreach (int stride in new[] { 12, 12, 20, 24, 20, 24, 28, 36 })
        {
            int faces = I32(source.Data, end);
            if (faces < 0)
            {
                throw new InvalidDataException("White Wind ACT has a negative primitive count.");
            }
            end = checked(end + 4 + faces * stride);
        }
        ImmutableArray<byte> shape = Slice(source.Data, geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleEffectArena selectedArena = arena;
        NativeBattleCuraga88MeshStep drawn = NativeBattleCuraga88Mesh.EmitBatch(shape, shapeAddress, matrix, projection, temporary88, registers, farControls, dqa, dqb, zsf4, constants, selectedVerticesAddress: selectedAddress, readSelectedVertex: ReadVertex, pilotIdentity: pilotIdentity);
        return new(drawn.Batch, drawn.Context88, arena, drawn.Registers, drawn.FarControls);

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            if (offset < 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("White Wind ACT face leaves its selected packed vertex bank.");
            }
            if (!interpolate)
            {
                int at = checked(selected + offset);
                return (Vector(source.Data, at), BinaryPrimitives.ReadInt16LittleEndian(Slice(source.Data, at + 6, 2)));
            }
            byte[] xyz = selectedArena.Read(checked(morphOffset + offset), 6);
            (byte[] w, bool[] known) = selectedArena.ReadSnapshot(checked(morphOffset + offset + 6), 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
        }
    }

    private static NativeBattleGeometryRegisterStep Transform(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix, NativeBattleVector point, short? w)
    {
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), true, false, Padding(matrix)) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        return NativeBattleGeometry.Transform(registers, false);
    }
    private static (int X, int Y, int Z) Mac(NativeBattleGeometryRegisterStep registers) => (registers.Colors.Mac[0]!.Value, registers.Colors.Mac[1]!.Value, registers.Colors.Mac[2]!.Value);
    private static short? Padding(NativeBattlePackedMatrix32 matrix) => matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18)) : null;
    private static int I32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Slice(bytes, offset, 4));
    private static NativeBattleVector Vector(ImmutableArray<byte> bytes, int offset)
    {
        ReadOnlySpan<byte> data = Slice(bytes, offset, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]), BinaryPrimitives.ReadInt16LittleEndian(data[4..]));
    }
    private static ReadOnlySpan<byte> Slice(ImmutableArray<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length - count)
        {
            throw new InvalidDataException("White Wind ACT read leaves its original source.");
        }
        return bytes.AsSpan().Slice(offset, count);
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
