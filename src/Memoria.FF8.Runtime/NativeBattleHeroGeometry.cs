using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHeroVertexSelection(int GeometryOffset, int Count, int? SourceOffset, int FirstShape, int SecondShape, int Ratio)
{
    public bool RequiresMorph => SourceOffset is null;
}

public static class NativeBattleHeroGeometry
{
    public static bool IsHidden(NativeBattleGfAnimationNode node)
    {
        return node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0;
    }

    public static NativeBattleHeroVertexSelection Select(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node)
    {
        int index = unchecked((short)node.GeometryIndex);
        int geometry = Read(source.Data, checked(8 + index * 4));
        int count = Read(source.Data, checked(geometry + 4));
        if (count < 0 || count > source.Data.Length / 8)
        {
            throw new InvalidDataException("The Hero geometry vertex count leaves its actual source.");
        }
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            int offset = checked(geometry + 12 + shape * count * 8);
            RequireRange(source.Data, offset, checked(count * 8));
            return new(geometry, count, offset, node.Word40, node.Word42, node.Scalar38);
        }
        return new(geometry, count, null, node.Word40, node.Word42, node.Scalar38);
    }

    public static ImmutableArray<byte> SelectVertices(NativeBattleGfAnimationSource source, NativeBattleHeroVertexSelection selection, ImmutableArray<byte> actualScratch)
    {
        if (selection.SourceOffset is int offset)
        {
            RequireRange(source.Data, offset, checked(selection.Count * 8));
            return source.Data.Slice(offset, selection.Count * 8);
        }
        return NativeBattleGfMorphGeometry.Morph701390(source.Data, selection.GeometryOffset, selection.FirstShape, selection.SecondShape, selection.Ratio, actualScratch);
    }

    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleHeroDrawContext context, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = ScaledOffset(node, context);
        bool scaleEnabled = RequireScale(context);
        if (scaleEnabled)
        {
            local = NativeBattlePoseMatrices.Scale56BEF0(local, context.ScaleX, context.ScaleY, context.ScaleZ);
        }
        (int x, int y, int z) translation;
        if ((node.Header & 0x200) != 0)
        {
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, context.Anchor);
            translation = (unchecked(x + offset.X), unchecked(y + offset.Y), unchecked(z + offset.Z));
        }
        else
        {
            NativeBattleVector point = new(unchecked((short)(context.Anchor.X + offset.X)), unchecked((short)(context.Anchor.Y + offset.Y)), unchecked((short)(context.Anchor.Z + offset.Z)));
            translation = NativeBattleGfMatrices.Rotate56C3D0(view, point);
            local = local with { Rotation = NativeBattleGfMatrices.Multiply56C090(view, local).Rotation };
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                local = local with { Rotation = NativeBattleGfMatrices.Multiply56C090(local, diagonal).Rotation };
            }
            else
            {
                local = NativeBattlePoseMatrices.Scale56BEF0(local, scale.X, scale.Y, scale.Z);
            }
        }
        return local with { X = unchecked(view.X + translation.x), Y = unchecked(view.Y + translation.y), Z = unchecked(view.Z + translation.z) };
    }

    public static NativeBattleActionMatrix FanMatrix(NativeBattleHeroFan fan, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(fan.Angles), fan.Radius, fan.Radius, fan.Radius);
        local = local with { X = fan.Anchor.X, Y = fan.Anchor.Y, Z = fan.Anchor.Z };
        return NativeBattlePoseMatrices.Compose56C2F0(view, local);
    }

    public static NativeBattleVector ViewInput(NativeBattleGfAnimationNode node, NativeBattleHeroDrawContext context)
    {
        if ((node.Header & 0x200) != 0)
        {
            return context.Anchor;
        }
        NativeBattleVector offset = ScaledOffset(node, context);
        return new(unchecked((short)(context.Anchor.X + offset.X)), unchecked((short)(context.Anchor.Y + offset.Y)), unchecked((short)(context.Anchor.Z + offset.Z)));
    }

    private static NativeBattleVector ScaledOffset(NativeBattleGfAnimationNode node, NativeBattleHeroDrawContext context)
    {
        NativeBattleVector offset = node.Vector8;
        return RequireScale(context) ? new(unchecked((short)(unchecked(offset.X * context.ScaleX) >> 12)), unchecked((short)(unchecked(offset.Y * context.ScaleY) >> 12)), unchecked((short)(unchecked(offset.Z * context.ScaleZ) >> 12))) : offset;
    }

    private static bool RequireScale(NativeBattleHeroDrawContext context) => context.ScaleEnabled ?? throw new NotSupportedException("The visible Hero node requires its current physical scale-enable operand.");

    private static int Read(ImmutableArray<byte> bytes, int offset)
    {
        RequireRange(bytes, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }

    private static void RequireRange(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (bytes.IsDefault || offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The Hero geometry read leaves its actual bounded resource.");
        }
    }
}
