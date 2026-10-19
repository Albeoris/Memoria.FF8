using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The distinct6BDB90/6BED30 part contracts over the shared701970 record and7043B0 packet kernel.</summary>
public static class NativeBattleStatusMagicDrawing
{
    public static NativeBattleStatusMagicFrame Draw(NativeBattleStatusMagicFrame frame, NativeBattleStatusMagicResources resources, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleVector anchor, int depth, NativeBattleStatusMagicInputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        ImmutableArray<byte> data = source.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(8 + unchecked((short)node.GeometryIndex) * 4)));
        int vertices = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(checked(geometry + 4)));
        if (vertices is < 0 or > 4096)
        {
            throw new InvalidDataException("Native status layout geometry exceeds its original bounded vertex bank.");
        }
        ImmutableArray<byte> selected;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = data.AsSpan().Slice(checked(geometry + 12 + shape * vertices * 8), checked(vertices * 8)).ToArray().ToImmutableArray();
        }
        else
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native status morph requires its current arena bytes.");
            ImmutableArray<NativeBattleVector> points = NativeBattleGfMorphGeometry.MorphVertices701390(data, geometry, node.Word40, node.Word42, node.Scalar38);
            for (int index = 0; index < points.Length; index++)
            {
                byte[] xyz = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(xyz, points[index].X);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), points[index].Y);
                BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), points[index].Z);
                arena = arena.Write(resources.MorphOffset + index * 8, xyz);
            }
            selected = arena.Read(resources.MorphOffset, vertices * 8).ToImmutableArray();
            frame = frame with { Memory = frame.Memory with { Arena = arena } };
        }
        NativeBattleActionMatrix matrix = Matrix(resources.Effect, node, anchor, inputs.Projection.View ?? throw new NotSupportedException("Native status layout requires its published battle camera matrix."));
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        int bias = resources.Effect == 122 && node.GeometryIndex == 4 ? unchecked(depth - 0x20) : depth;
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(data, geometry, selected, matrix, inputs.Projection, new(flags, node.Color32, node.Scalar36, null, null, bias, 17, 2), inputs.Colors);
        return inputs.Execute(frame, new NativeBattleStatusMagicPackets(batch));
    }

    public static NativeBattleActionMatrix Matrix(ushort effect, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleActionMatrix camera)
    {
        NativeBattleActionMatrix matrix = effect == 120 && (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, anchor);
            x = unchecked(x + offset.X);
            y = unchecked(y + offset.Y);
            z = unchecked(z + offset.Z);
        }
        else
        {
            NativeBattleVector point = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(camera, point);
            matrix = NativeBattleGfMatrices.Multiply56C090(camera, matrix);
        }
        matrix = matrix with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if (effect == 122 && (node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                matrix = matrix with { Rotation = NativeBattleGfMatrices.Multiply56C090(matrix, diagonal).Rotation };
            }
            else
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
            }
        }
        return matrix;
    }

    public static int SizeDepth(NativeBattleGraphicsModel model)
    {
        short height = (model.Transform ?? throw new NotSupportedException("Native status depth requires modelWORD26.")).Scale38;
        return unchecked(-0x800 - height) >> 5;
    }
}
