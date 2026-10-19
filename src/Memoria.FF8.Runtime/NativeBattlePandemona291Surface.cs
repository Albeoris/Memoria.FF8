using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291PreparedSurface(bool Active, int GeometryOffset, int SelectedOffset, ImmutableArray<NativeBattleVector> Morph, NativeBattleActionMatrix Matrix, NativeBattleEsunaMeshContext Context);

public static class NativeBattlePandemona291Surface
{
    public static NativeBattlePandemona291PreparedSurface Prepare6F06F0(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleActionMatrix parent, byte textureKind, byte phase, short depthBias)
    {
        NativeBattleTextureWindow full = new(0, 0, 256, 256);
        NativeBattleEsunaMeshContext context = new(0x2030, null, 0, depthBias, 0, 0, full, full, 17, 2);
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(false, 0, 0, [], parent, context);
        }
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(8 + 4 * unchecked((short)node.GeometryIndex)), 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked(geometry + 4), 4));
        int selected = 0;
        ImmutableArray<NativeBattleVector> morph = [];
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            selected = checked(geometry + 12 + shape * count * 8);
        }
        else
        {
            morph = NativeBattleGfMorphGeometry.MorphVertices701390(source.Data, geometry, node.Word40, node.Word42, node.Scalar38);
        }
        NativeBattleActionMatrix local = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector position = node.Vector8;
        if ((node.Header & 0x800) != 0)
        {
            position = NativeBattleGfMatrices.RotateShort56C4F0(local, position);
        }
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(parent, position);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(parent, local);
        if (node.Vector24 is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleVector scale = node.Vector24;
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                matrix = NativeBattleGfMatrices.Multiply56C090(matrix, diagonal);
            }
            else
            {
                matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, node.Vector24.X, node.Vector24.Y, node.Vector24.Z);
            }
        }
        matrix = matrix with { X = unchecked(parent.X + x), Y = unchecked(parent.Y + y), Z = unchecked(parent.Z + z) };
        if (node.Scalar36 != 0)
        {
            context = context with { Flags = 0x20F0, FarColor = node.Color32, Fade = node.Scalar36 };
        }
        context = textureKind switch
        {
            0 => context with { VOffset = (ushort)(phase & 127), WindowAfter = new(0, 128, 256, 128) },
            1 => context with { UOffset = (ushort)(phase & 127), WindowAfter = new(0, 0, 128, 256) },
            2 => context with { VOffset = (ushort)(phase & 127), WindowAfter = new(0, 0, 256, 128) },
            3 => context with { VOffset = (ushort)(unchecked((byte)-phase) & 127), WindowAfter = new(0, 128, 256, 128) },
            4 => context,
            _ => throw new NotSupportedException("Native Pandemona texture selector requires its retained rectangle operands.")
        };
        return new(true, geometry, selected, morph, matrix, context);
    }

    public static NativeBattleGfGeometryBatch Emit701DD0(NativeBattleGfAnimationSource source, NativeBattlePandemona291PreparedSurface prepared, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short zsf4, Func<int, NativeBattleVector> readSelected, Action<int>? observeInput = null, Action<uint>? observeFlags = null, Func<NativeBattleVector, NativeBattleModuleProjectedVertex>? projectFourth = null)
    {
        if (!prepared.Active)
        {
            return new([], 0);
        }
        return EmitGeometry701DD0(source.Data.AsSpan().Slice(prepared.GeometryOffset).ToArray().ToImmutableArray(), prepared.Matrix, prepared.Context, projection, colors, zsf4, readSelected, observeInput, observeFlags, projectFourth);
    }

    public static NativeBattleGfGeometryBatch EmitGeometry701DD0(ImmutableArray<byte> geometry, NativeBattleActionMatrix matrix, NativeBattleEsunaMeshContext context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, short zsf4, Func<int, NativeBattleVector> readSelected, Action<int>? observeInput = null, Action<uint>? observeFlags = null, Func<NativeBattleVector, NativeBattleModuleProjectedVertex>? projectFourth = null)
    {
        NativeBattleEffectMeshWindow window = new(context.UOffset, context.VOffset, context.WindowBefore, context.WindowAfter);
        NativeBattleEffectMeshPacketContext operands = new(context.Flags, context.FarColor, context.Fade, null, null, 0, false, context.DepthBias, context.TableBaseBucket, context.DepthShift, window);
        int corner = -1;
        return NativeBattleEffectMeshPackets.EmitBatch(geometry, matrix, projection, context.Flags, offset =>
        {
            corner++;
            if (corner is 0 or 3)
            {
                observeInput?.Invoke(offset);
            }
            return readSelected(offset);
        }, (group, face, project, offset) =>
        {
            corner = -1;
            NativeBattleGfGeometryBatch batch = group switch
            {
                3 => NativeBattleEffectMeshPackets.EmitTextured(face, project, true, false, operands, colors, offset),
                7 => NativeBattleEffectMeshPackets.EmitTextured(face, project, true, true, operands, colors, offset),
                _ => throw new NotSupportedException($"Native Pandemona surface geometry requires its reached FT4/GT4 groups, found{group}.")
            };
            NativeBattleGeometryPrimitive? polygon = batch.Attempts.Select(attempt => attempt.Primitive).FirstOrDefault(primitive => primitive is not null && primitive.VertexPayload4.Length == 4 && primitive.Words.Length > 3);
            if (polygon is null)
            {
                return batch;
            }
            uint sum = unchecked((uint)polygon.VertexPayload4.Sum());
            uint average = unchecked(sum * (uint)(int)zsf4) >> 12;
            int depth = Math.Max(unchecked((int)average + context.DepthBias), 0);
            int bucket = checked(context.TableBaseBucket + (depth >> context.DepthShift));
            return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = primitive with { Bucket = bucket } } : attempt).ToImmutableArray() };
        }, observeFlags, projectFourth);
    }
}
