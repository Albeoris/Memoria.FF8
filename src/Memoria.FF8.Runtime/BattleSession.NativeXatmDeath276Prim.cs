using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleXatmDeath276Frame Frame) InitializeNativeXatmDeath276Prim(NativeBattleGraphicsState graphics, NativeBattleXatmDeath276Frame frame, int slot)
    {
        NativeBattleXatmDeath276Resources resources = frame.Effect.Binding.XatmDeath276Resources ?? throw new NotSupportedException("Native X-ATM092 requires its original primitive resources.");
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[4];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x18 - 12, 0x124));
        workspace = workspace.Initialize7016B0(resources.Prim, 0x124);
        task = PublishWorkspace(task, 0x18, workspace);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return (graphics, frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, pool) } });
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleXatmDeath276Frame Frame) AdvanceNativeXatmDeath276Prim(NativeBattleGraphicsState graphics, NativeBattleXatmDeath276Frame frame, int slot)
    {
        NativeBattleXatmDeath276Resources resources = frame.Effect.Binding.XatmDeath276Resources ?? throw new NotSupportedException("Native X-ATM092 requires its original primitive resources.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native X-ATM092 requires its published camera projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native X-ATM092 requires its published camera matrix.");
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native X-ATM092 requires its original interpolation constants.");
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[4];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (unchecked((short)task.ReadWord(0x14)) != 4096)
        {
            throw new NotSupportedException("Native X-ATM092 primitive argument requires its original identity event scale.");
        }
        NativeBattleVector origin = new(unchecked((short)task.ReadWord(0xE)), unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)));
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.Slice(0x18 - 12, 0x124));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Prim, 0, (current, node) =>
        {
            if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
            {
                return current;
            }
            int geometry = BinaryPrimitives.ReadInt32LittleEndian(resources.Prim.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
            int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Prim.Data.AsSpan().Slice(geometry + 4));
            ImmutableArray<byte> vertices;
            if (node.Word40 == node.Word42 || node.Scalar38 is 0 or 4096)
            {
                int shape = node.Word40 == node.Word42 || node.Scalar38 == 0 ? node.Word40 : node.Word42;
                vertices = resources.Prim.Data.Slice(checked(geometry + 12 + shape * count * 8), checked(count * 8));
            }
            else
            {
                if (count is < 0 or > 80)
                {
                    throw new InvalidDataException("Native X-ATM092 primitive morph exceeds its original eighty-vertex allocation.");
                }
                int scratch = checked(frame.State.BufferOffset + 0x4084);
                NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native X-ATM092 requires its loaded morph allocation.");
                ImmutableArray<byte> destination = arena.Read(scratch, 0x280).ToImmutableArray();
                ImmutableArray<byte> morphed = NativeBattleGfMorphGeometry.Morph701390(resources.Prim.Data, geometry, node.Word40, node.Word42, node.Scalar38, destination);
                frame = frame with { Memory = frame.Memory with { Arena = arena.Write(scratch, morphed.AsSpan()) } };
                vertices = morphed.Slice(0, count * 8);
            }
            NativeBattleActionMatrix matrix = NativeBattleXatmDeath276PrimGeometry.Compose(node, origin, view, resources.EffectCamera);
            bool fade = node.Scalar36 != 0;
            NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(resources.Prim.Data, geometry, vertices, matrix, projection, new(fade ? 0x20F0u : 0x2030u, fade ? node.Color32 : 0, node.Scalar36, null, null, -128, 17, 2), colors);
            (graphics, frame) = AppendNativeXatmDeath276Packets(graphics, frame, batch);
            return current;
        });
        pool = frame.Effect.Pools[4];
        task = PublishWorkspace(pool.Slots[slot], 0x18, advanced.Workspace);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return (graphics, frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(4, pool) } });
    }
}

public static class NativeBattleXatmDeath276PrimGeometry
{
    public static NativeBattleActionMatrix Compose(NativeBattleGfAnimationNode node, NativeBattleVector origin, NativeBattleActionMatrix view, NativeBattleActionMatrix effectCamera)
    {
        uint flags = node.Header;
        NativeBattleActionMatrix rotation = (flags & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x;
        int y;
        int z;
        if ((flags & 0x1000) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, origin);
            (int ox, int oy, int oz) = NativeBattleGfMatrices.Rotate56C3D0(effectCamera, node.Vector8);
            x = unchecked(x + ox);
            y = unchecked(y + oy);
            z = unchecked(z + oz);
            rotation = NativeBattleGfMatrices.Multiply56C090(effectCamera, rotation);
        }
        else if ((flags & 0x200) != 0)
        {
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, origin);
            x = unchecked(x + node.Vector8.X);
            y = unchecked(y + node.Vector8.Y);
            z = unchecked(z + node.Vector8.Z);
        }
        else
        {
            NativeBattleVector translated = new(unchecked((short)(origin.X + node.Vector8.X)), unchecked((short)(origin.Y + node.Vector8.Y)), unchecked((short)(origin.Z + node.Vector8.Z)));
            (x, y, z) = NativeBattleGfMatrices.Rotate56C3D0(view, translated);
            rotation = NativeBattleGfMatrices.Multiply56C090(view, rotation);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (flags & 0x100) != 0
                ? NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096))
                : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        return rotation with { X = unchecked(view.X + x), Y = unchecked(view.Y + y), Z = unchecked(view.Z + z) };
    }
}
