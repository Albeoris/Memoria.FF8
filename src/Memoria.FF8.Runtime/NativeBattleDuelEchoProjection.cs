using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuelEchoProjectionResult(NativeBattleModulePoolTask Context, int SourceBytes, int VertexBytes);

public static class NativeBattleDuelEchoProjection
{
    public static NativeBattleActionMatrix ComposeHistory460820(NativeBattleActionMatrix basis, NativeBattleActionMatrix bone)
    {
        basis.Validate();
        bone.Validate();
        NativeBattleActionMatrix rotationOnly = basis with { X = 0, Y = 0, Z = 0 };
        short[] rotation = new short[9];
        for (int column = 0; column < 3; column++)
        {
            NativeBattleVector result = NativeBattleGfMatrices.Transform460840(rotationOnly, new(bone.Rotation[column], bone.Rotation[column + 3], bone.Rotation[column + 6]));
            rotation[column] = result.X;
            rotation[column + 3] = result.Y;
            rotation[column + 6] = result.Z;
        }
        NativeBattleFullMacVertex translation = NativeBattleGfMatrices.TransformMac460840(basis, new(unchecked((short)bone.X), unchecked((short)bone.Y), unchecked((short)bone.Z)));
        return new(rotation.ToImmutableArray(), translation.X, translation.Y, translation.Z, 4096);
    }

    public static NativeBattleDuelEchoProjectionResult Project6A2A90(NativeBattleModulePoolTask context, int offset, ImmutableArray<byte> vertices, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int vertexOffset, Action<int, ImmutableArray<byte>> writeCpu)
    {
        if (vertices.IsDefault || vertices.Length < 2 || vertexOffset < 0)
        {
            throw new InvalidDataException("Native Duel vertex projection requires its bounded source and canonical CPU destination.");
        }
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(vertices.AsSpan());
        if (count > short.MaxValue || vertices.Length < checked(2 + count * 6))
        {
            throw new InvalidDataException("Native Duel vertex projection leaves its original signed count and source extent.");
        }
        for (int index = 0; index < count; index++)
        {
            int source = 2 + index * 6;
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(source)), BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(source + 2)), BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan().Slice(source + 4)));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FE10(matrix, point, projection);
            ushort depth = (ushort)(projected.Depth >> 2);
            context = context.WriteDword(offset + 28, projected.Screen).WriteDword(offset + 32, depth);
            int destination = checked(vertexOffset + index * 8);
            if (depth == 0)
            {
                writeCpu(checked(destination + 6), BitConverter.GetBytes((ushort)16).ToImmutableArray());
                continue;
            }
            short x = unchecked((short)projected.Screen);
            short y = unchecked((short)(projected.Screen >> 16));
            ushort clip = x < 8 * Signed(context, offset + 12) ? (ushort)1 : x >= 8 * Signed(context, offset + 16) ? (ushort)2 : (ushort)0;
            clip |= y < 8 * Signed(context, offset + 14) ? (ushort)4 : y >= 8 * Signed(context, offset + 18) ? (ushort)8 : (ushort)0;
            context = context.WriteWord(offset + 34, clip);
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, projected.Screen);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), depth);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), clip);
            writeCpu(destination, bytes.ToImmutableArray());
        }
        return new(context, checked(2 + count * 6), checked(count * 8));
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
