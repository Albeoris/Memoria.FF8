using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleFatedCircle162Controller
{
    public static NativeBattleFatedCircle162ChildStep Advance5F9F70(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, short actorHeight3C, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CD50(new(0, Word(task, 24), 0));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, Word(task, 28), (actorHeight3C << 12) / -2000, Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        frame = DrawMesh(frame, inputs, 0xDA6700, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(counter >= 3 ? 0xF0u : 0x30u, counter >= 3 ? 0u : null, counter >= 3 ? 409 * (counter - 3) : null, null, null, null, 17, 2));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 26);
        task = task.WriteWord(24, unchecked((ushort)(Word(task, 24) + speed))).WriteWord(26, unchecked((ushort)(speed - (speed >> 4)))).WriteWord(12, unchecked((ushort)(counter + 1)));
        return Finish(frame, pool, slot, task, 13);
    }

    public static NativeBattleFatedCircle162ChildStep Advance5FA340(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), Word(task, 28), 4096, Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        frame = DrawMesh(frame, inputs, 0xDA4488, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(counter >= 4 ? 0xC3u : 3u, counter >= 4 ? 0u : null, counter >= 4 ? (counter - 4) << 9 : null, null, null, null, 17, 2));
        return DampedScale(frame, pool, slot, inputs.GraphicsFlags, task, counter, 12);
    }

    public static NativeBattleFatedCircle162ChildStep Advance5FA480(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleActionMatrix matrix = Billboard571BC0(Position(task), Word(task, 28), -256, view);
        frame = DrawMesh(frame, inputs, 0xDA3A58, matrix, projection, colors, new(counter >= 28 ? 0xC0u : 0u, counter >= 28 ? 0u : null, counter >= 28 ? (counter - 28) << 9 : null, null, null, null, 17, 2));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 30);
        task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + speed))).WriteWord(30, unchecked((ushort)(speed + (speed >> 6)))).WriteWord(12, unchecked((ushort)(counter + 1)));
        return Finish(frame, pool, slot, task, 36);
    }

    public static NativeBattleFatedCircle162ChildStep Advance5FB3F0(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = Word(task, 12);
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(500, Word(task, 24), 0)), Word(task, 28), Word(task, 28), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        uint flags = 0x33;
        int? fade = null;
        if (counter < 8)
        {
            flags = 0xF3;
            fade = (8 - counter) << 9;
        }
        else if (counter >= 12)
        {
            flags = 0xF3;
            fade = 682 * (counter - 12);
        }
        ImmutableArray<byte> vertices = default;
        if (counter >= 8)
        {
            flags |= 0x2000;
            int sine = NativeBattleCameraMath.Sine(56 * (counter - 8));
            for (int vertex = 0; vertex < 168; vertex++)
            {
                for (int component = 0; component < 3; component++)
                {
                    uint offset = checked((uint)(vertex * 8 + component * 2));
                    int source = ReadWord(frame.Staging, 0xDA71A0 + offset);
                    int target = ReadWord(frame.Staging, 0xDA8728 + offset);
                    int value = unchecked(source + ((sine * (target - source)) >> 12));
                    frame = frame with { Staging = WriteWord(frame.Staging, 0x23D07A0 + offset, unchecked((ushort)value)) };
                }
            }
            vertices = frame.Staging.Read(0x23D07A0, 168 * 8).ToImmutableArray();
        }
        frame = DrawMesh(frame, inputs, 0xDA7198, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(flags, 0, fade, null, null, null, 17, 2), vertices);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        if (counter < 8)
        {
            task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + (Word(task, 30) >> 3))));
        }
        return Finish(frame, pool, slot, task.WriteWord(12, unchecked((ushort)(counter + 1))), 18);
    }

    public static NativeBattleFatedCircle162ChildStep Advance5FB630(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (Word(task, 14) > 0)
        {
            return Delay(frame, pool, slot, inputs.GraphicsFlags, task, 14);
        }
        short counter = Word(task, 12);
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(41, Word(task, 24), 0)), Word(task, 28), Word(task, 28), Word(task, 28)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
        frame = DrawMesh(frame, inputs, 0xDA7198, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(counter >= 14 ? 0x20F3u : 0x2033u, 0, counter >= 14 ? (counter - 14) << 9 : null, null, null, null, 17, 2), frame.Staging.Read(0xDA8728, 168 * 8).ToImmutableArray());
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 30);
        task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + speed))).WriteWord(30, unchecked((ushort)(speed + (speed >> 3)))).WriteWord(12, unchecked((ushort)(counter + 1)));
        return Finish(frame, pool, slot, task, 22);
    }

    public static NativeBattleFatedCircle162ChildStep Advance5FC480(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleFatedCircle162Inputs inputs, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (Word(task, 26) > 0)
        {
            return Delay(frame, pool, slot, inputs.GraphicsFlags, task, 26);
        }
        short counter = Word(task, 12);
        int fragment = Word(task, 14);
        if (fragment is < 0 or >= 8)
        {
            throw new InvalidDataException("Native5FC480 requires an original fragment directory index.");
        }
        uint address = unchecked((uint)ReadDword(frame.Staging, 0xDA8C74u + (uint)fragment * 4));
        NativeBattleActionMatrix world = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(0, 0, 0)), 4096, 0, 4096) with { X = Word(task, 16), Y = 41, Z = Word(task, 20) };
        frame = DrawMesh(frame, inputs, address, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, colors, new(0xC0, 0, counter >= 12 ? 384 * (20 - counter) : 3072, null, null, null, 4122, 13));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        return Finish(frame, pool, slot, task.WriteWord(12, unchecked((ushort)(counter + 1))), 21);
    }

    private static NativeBattleFatedCircle162Frame DrawMesh(NativeBattleFatedCircle162Frame frame, NativeBattleFatedCircle162Inputs inputs, uint address, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleModuleMeshContext context, ImmutableArray<byte> selectedVertices = default, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleFatedCircle162Resources resources = frame.Memory.FatedCircle162?.RetainedResources ?? throw new NotSupportedException("Native Fated Circle drawing requires resources retained by actual entry.");
        NativeBattleStaticMeshResource mesh = resources.Meshes.Single(resource => resource.Address == address);
        ImmutableArray<byte> bytes = frame.Staging.Read(address, mesh.Bytes.Length).ToImmutableArray();
        NativeBattlePilotVisualIdentity? identity = pilotIdentity ?? (inputs.PilotIdentity is { } task ? task with { Primitive = unchecked((int)address) } : null);
        NativeBattleGfGeometryBatch batch = NativeBattleModulePackets.EmitBatch572200(bytes, 0, selectedVertices, matrix, projection, context, colors, pilotIdentity: identity);
        return inputs.Execute(frame, new NativeBattleFatedCircle162Geometry(batch));
    }

    private static NativeBattleActionMatrix Billboard571BC0(NativeBattleModuleAnchor point, short scale, int depth, NativeBattleActionMatrix view)
    {
        NativeBattleModuleDraw draw = new(NativeBattleModuleDrawKind.Sprite, 0) { Anchor = point, Scale = scale, DepthOffset = depth, SpriteFlags = 0 };
        return NativeBattlePoseMatrices.ModuleSpriteCamera(draw, view, 0);
    }

    private static NativeBattleFatedCircle162ChildStep Delay(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, uint flags, NativeBattleModulePoolTask task, int offset)
    {
        if ((flags & 0x201) == 0)
        {
            task = task.WriteWord(offset, unchecked((ushort)(Word(task, offset) - 1)));
        }
        return new(frame, pool.WriteSlot(slot, task), 0);
    }

    private static NativeBattleFatedCircle162ChildStep DampedScale(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, uint flags, NativeBattleModulePoolTask task, short counter, int lifetime)
    {
        if ((flags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = Word(task, 30);
        task = task.WriteWord(28, unchecked((ushort)(Word(task, 28) + speed))).WriteWord(30, unchecked((ushort)(speed - speed / 4))).WriteWord(12, unchecked((ushort)(counter + 1)));
        return Finish(frame, pool, slot, task, lifetime);
    }

    private static NativeBattleFatedCircle162ChildStep Finish(NativeBattleFatedCircle162Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task, int lifetime) => new(frame, pool.WriteSlot(slot, task), task.ReadWord(12) >= lifetime ? 2 : 0);
}
