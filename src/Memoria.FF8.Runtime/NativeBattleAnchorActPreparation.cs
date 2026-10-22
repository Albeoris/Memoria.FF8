using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAnchorActContextPolicy(int MorphPointerOffset, int? DepthBiasOffset, bool RequireDirectMorphCapacity, bool Header100Scaling = false)
{
    public static NativeBattleAnchorActContextPolicy Electrocute275 { get; } = new(8, null, true);
    public static NativeBattleAnchorActContextPolicy AnchorDepth16 { get; } = new(12, 8, false);

    public static NativeBattleAnchorActContextPolicy ReflectBeam135 { get; } = new(12, 8, false, true);

    public void Validate()
    {
        if (this != Electrocute275 && this != AnchorDepth16 && this != ReflectBeam135)
        {
            throw new NotSupportedException("Anchor ACT requires a supported context layout and vertex-bound policy.");
        }
    }
}

public sealed record NativeBattleAnchorActPreparationStep(NativeBattleEffectArena Temporary88, NativeBattleEffectArena Arena, NativeBattleGeometryRegisterStep Registers, NativeBattleActionMatrix Matrix, int GeometryOffset, uint SelectedVerticesAddress, Func<int, (NativeBattleVector Vertex, short? W)> ReadVertex);

public static class NativeBattleAnchorActPreparation
{
    public static NativeBattleAnchorActPreparationStep Prepare(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena temporary88, NativeBattleEffectArena arena, uint morphAddress, int morphLength, NativeBattleGeometryRegisterStep registers, NativeBattleSummonColors constants, NativeBattleAnchorActContextPolicy policy, short? localVectorW = null)
    {
        policy.Validate();
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            throw new InvalidOperationException("Anchor ACT mesh allocation requires a visible node.");
        }
        int geometry = I32(source.Data, checked(8 + node.GeometryIndex * 4));
        int count = I32(source.Data, checked(geometry + 4));
        bool interpolate = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        if (count < 1 || (policy.RequireDirectMorphCapacity || interpolate) && count > morphLength / 8)
        {
            throw new InvalidDataException("Anchor ACT shape leaves its original morph-to-packet boundary.");
        }
        int first = checked(geometry + 12 + node.Word40 * count * 8);
        int second = checked(geometry + 12 + node.Word42 * count * 8);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? second : first;
        int morphOffset = checked((int)(morphAddress - NativeBattleEffectNodeMemory.ArenaAddress));
        if (morphLength < 0 || morphOffset < 0 || morphOffset > NativeBattleEffectArena.Length - morphLength)
        {
            throw new InvalidDataException("Anchor ACT requires its actual bounded morph owner.");
        }
        uint selectedAddress;
        if (interpolate)
        {
            uint morph = Dword(context, policy.MorphPointerOffset);
            if (morph != morphAddress)
            {
                throw new InvalidDataException("Anchor ACT morph pointer does not identify its actual shared working arena.");
            }
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
        NativeBattleActionMatrix matrix = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector anchor = new(Signed(context, 0), Signed(context, 2), Signed(context, 4));
        NativeBattleVector point = node.Vector8;
        NativeBattleActionMatrix view = camera.ReadMatrix();
        (int X, int Y, int Z) transformed;
        if ((node.Header & 0x200) != 0)
        {
            registers = Transform(registers, camera, anchor, ContextW(context));
            (int X, int Y, int Z) baseTranslation = Mac(registers);
            transformed = (unchecked(baseTranslation.X + point.X), unchecked(baseTranslation.Y + point.Y), unchecked(baseTranslation.Z + point.Z));
        }
        else
        {
            point = new(unchecked((short)(point.X + anchor.X)), unchecked((short)(point.Y + anchor.Y)), unchecked((short)(point.Z + anchor.Z)));
            registers = Transform(registers, camera, point, localVectorW);
            transformed = Mac(registers);
            matrix = NativeBattleGfMatrices.Multiply56C090(view, matrix);
        }
        matrix = matrix with { X = unchecked(transformed.X + view.X), Y = unchecked(transformed.Y + view.Y), Z = unchecked(transformed.Z + view.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if (policy.Header100Scaling && (node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                matrix = matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, diagonal).Rotation };
            }
            else
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        uint shapeAddress = checked(source.Address + (uint)geometry);
        temporary88 = Store(temporary88, 0, shapeAddress);
        temporary88 = Store(temporary88, 4, selectedAddress);
        temporary88 = Store(temporary88, 12, unchecked((uint)(int)node.Scalar36));
        uint flags = 0x2030;
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
        }
        temporary88 = Store(temporary88, 0x1C, flags);
        if (node.Scalar36 != 0)
        {
            temporary88 = Store(temporary88, 8, node.Color32);
        }
        if (policy.DepthBiasOffset is int depthOffset)
        {
            temporary88 = Store(temporary88, 0x18, Dword(context, depthOffset));
        }
        NativeBattleEffectArena selectedArena = arena;
        return new(temporary88, arena, registers, matrix, geometry, selectedAddress, ReadVertex);

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            if (offset < 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Anchor ACT face leaves its selected packed vertex bank.");
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
            throw new InvalidDataException("Anchor ACT read leaves its original source.");
        }
        return bytes.AsSpan().Slice(offset, count);
    }
    private static short? ContextW(NativeBattleEffectArena context)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }
    private static short Signed(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
