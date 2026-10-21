using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusCureItemGeometryStep(NativeBattleStatusCureItemFrame Frame, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleStatusCureItemGeometry
{
    public static NativeBattleVector Anchor(ushort effect, NativeBattleVector defaultPosition, short modelY, short height, NativeBattleVector? bone240 = null)
    {
        return effect switch
        {
            292 or 294 => defaultPosition with { Y = unchecked((short)(modelY + height / 2)) },
            293 => bone240 ?? throw new NotSupportedException("Native Eye Drops requires its original502170 bone240 anchor."),
            295 => defaultPosition,
            296 => defaultPosition with { Y = unchecked((short)(modelY + (height * 2 / 3 + height / 2) / 2)) },
            _ => throw new NotSupportedException("Native status-cure item anchor requires its original profile.")
        };
    }

    public static NativeBattleActionMatrix Compose(ushort effect, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleActionMatrix view, NativeBattleActionMatrix? billboard = null)
    {
        _ = NativeBattleStatusCureItemProfile.ForEffect(effect);
        NativeBattleActionMatrix rotation = effect == 295
            ? (node.Header & 0x400) != 0 ? NativeBattleGfMatrices.Euler701430(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16)
            : (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector offset = node.Vector8;
        int x;
        int y;
        int z;
        if (effect == 295 && (node.Header & 0x1000) != 0)
        {
            NativeBattleActionMatrix basis = billboard ?? throw new NotSupportedException("Native Echo Screen requires its current root's67AAE0 billboard publication.");
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            (int dx, int dy, int dz) = NativeBattleGfMatrices.Rotate56C3D0(basis, offset);
            x = unchecked(x + dx);
            y = unchecked(y + dy);
            z = unchecked(z + dz);
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
            }
        }
        else if (effect != 295 && (node.Header & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, anchor);
            x = unchecked(x + offset.X);
            y = unchecked(y + offset.Y);
            z = unchecked(z + offset.Z);
        }
        else
        {
            NativeBattleVector translated = new(unchecked((short)(anchor.X + offset.X)), unchecked((short)(anchor.Y + offset.Y)), unchecked((short)(anchor.Z + offset.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, translated);
            if (effect is 293 or 296 || (node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(view, rotation);
            }
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (effect is 293 or 295) && (node.Header & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        return rotation with { X = unchecked(x + view.X), Y = unchecked(y + view.Y), Z = unchecked(z + view.Z) };
    }

    public static NativeBattleStatusCureItemGeometryStep Emit(NativeBattleStatusCureItemFrame frame, NativeBattleStatusCureItemResources resources, NativeBattleGfAnimationNode node, NativeBattleVector anchor, NativeBattleCameraProjection projection, NativeBattleSummonColors? colors = null)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return new(frame, new([], 0));
        }
        NativeBattleStatusCureItemProfile profile = NativeBattleStatusCureItemProfile.ForEffect(resources.Effect);
        ImmutableArray<byte> data = resources.Layout.Data;
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(geometry + 4));
        ImmutableArray<byte> vertices;
        if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
        {
            int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
            vertices = data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
        }
        else
        {
            int scratch = checked(frame.Execution.ArenaOffset + profile.ChildStride);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native status-cure item morph requires its current arena scratch.");
            vertices = NativeBattleGfMorphGeometry.Morph701390(data, geometry, node.Word40, node.Word42, node.Scalar38, arena.Read(scratch, checked(count * 8)).ToImmutableArray());
            frame = frame with { Memory = frame.Memory with { Arena = arena.Write(scratch, vertices.AsSpan()) } };
            vertices = vertices.Slice(0, checked(count * 8));
        }
        NativeBattleActionMatrix matrix = Compose(resources.Effect, node, anchor, projection.View ?? throw new NotSupportedException("Native status-cure item requires its current camera matrix."), frame.Execution.Billboard);
        uint flags = node.Scalar36 == 0 ? 0x2030u : 0x20F0u;
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(data, geometry, vertices, matrix, projection, new(flags, node.Color32, node.Scalar36, null, null, -128, 17, 2), colors);
        return new(frame, batch);
    }
}
