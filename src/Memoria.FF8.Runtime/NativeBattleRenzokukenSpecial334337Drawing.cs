using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialController
{
    private static NativeBattleRenzokuken160ChildStep DrawGroupMesh(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleStaticMeshResource mesh, NativeBattleActionMatrix world, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, int fadeStart, int fadeStep, int lifetime, int divisor, bool stored = false, byte depthShift = 2)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        int scale = unchecked((short)task.ReadWord(28));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, scale, scale, scale);
        NativeBattleModuleMeshContext context = new(counter >= fadeStart ? 243u : 51u, 0, counter >= fadeStart ? fadeStep * (counter - fadeStart) : null, null, null, null, 17, depthShift);
        NativeBattleGfGeometryBatch packets = NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, NativeBattlePoseMatrices.Compose56C2F0(view, world), projection, context, colors);
        frame = inputs.Execute(frame, new NativeBattleRenzokuken160MeshDraw(packets));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        short speed = unchecked((short)task.ReadWord(30));
        task = task.WriteWord(28, unchecked((ushort)(scale + speed))).WriteWord(30, unchecked((ushort)(speed - speed / divisor))).WriteWord(12, unchecked((ushort)(counter + 1)));
        bool ended = unchecked((short)task.ReadWord(12)) >= lifetime;
        if (ended && stored)
        {
            byte[] bank = ReadState(frame).ExtraStorage.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(task.ReadWord(14) * 32), 0);
            frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        }
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, ended ? 2 : 0);
    }

    private static NativeBattleRenzokuken160ChildStep GroupLateMesh(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleRenzokuken160Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint callback = task.Callback;
        NativeBattleActionMatrix world;
        if (callback is 0x5872C0 or 0x5826C0)
        {
            byte[] bank = ReadState(frame).ExtraStorage.ToArray();
            int row = task.ReadWord(14) * 32;
            NativeBattleModuleAxisAngle axis = NativeBattleModuleAlignment.Between571480(resources.SpecialAlignmentAxis[0], resources.SpecialAlignmentAxis[1], resources.SpecialAlignmentAxis[2], GroupWord(bank, row + 24), GroupWord(bank, row + 26), GroupWord(bank, row + 28));
            world = new(NativeBattleModuleAlignment.Rotation5714F0(axis.Angle, axis.Axis), GroupWord(bank, row + 8), GroupWord(bank, row + 10), GroupWord(bank, row + 12), 4096);
            return DrawGroupMesh(frame, pool, slot, inputs, resources.Meshes[callback == 0x5872C0 ? 2 : 4], world, view, projection, colors, 6, 341, 18, 4, true, 3);
        }
        if (callback == 0x5892B0)
        {
            world = NativeBattleGfMatrices.Euler56CE30(new(0, unchecked((short)task.ReadWord(24)), 0));
            world = world with { X = unchecked((short)task.ReadWord(16)), Y = unchecked((short)task.ReadWord(18)), Z = unchecked((short)task.ReadWord(20)) };
            return DrawGroupMesh(frame, pool, slot, inputs, resources.Meshes[2], world, view, projection, colors, 6, 512, 14, 4, true);
        }
        if (callback is 0x585220 or 0x582B50)
        {
            world = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = unchecked((short)task.ReadWord(16)), Y = 0, Z = unchecked((short)task.ReadWord(20)) };
            return DrawGroupMesh(frame, pool, slot, inputs, resources.Meshes[5], world, view, projection, colors, 6, 409, 16, 5);
        }
        world = inputs.ReadModel(ReadState(frame).Core.Actor).GeometryWorldMatrix ?? throw new NotSupportedException("Native special Renzokuken mesh requires its current actor matrix64.");
        world = world with { X = unchecked((short)task.ReadWord(16)), Y = unchecked((short)task.ReadWord(18)), Z = unchecked((short)task.ReadWord(20)) };
        return callback switch
        {
            0x589710 => DrawGroupMesh(frame, pool, slot, inputs, resources.Meshes[5], world, view, projection, colors, 4, 341, 16, 4),
            0x5848F0 or 0x582270 => DrawGroupMesh(frame, pool, slot, inputs, resources.Meshes[2], world, view, projection, colors, 6, 512, 14, 5),
            0x584DE0 => DrawGroupMesh(frame, pool, slot, inputs, resources.Meshes[4], world, view, projection, colors, 6, 341, 18, 4),
            _ => throw new NotSupportedException("Native special Renzokuken mesh callback has no original owner.")
        };
    }

    private static NativeBattleRenzokuken160ChildStep AdvanceGroupCamera(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        int value = 100 * NativeBattleCameraMath.Sine((counter << 10) / 8 + 1024) >> 12;
        if ((counter & 1) == 0)
        {
            value = -value;
        }
        frame = inputs.Execute(frame, new NativeBattleRenzokukenSpecialCameraSet(unchecked((short)value)));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, unchecked((short)task.ReadWord(12)) >= 8 ? 2 : 0);
    }

    private static NativeBattleRenzokuken160ChildStep AdvanceGroupRadialSprites(NativeBattleRenzokuken160Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleRenzokuken160Inputs inputs, NativeBattleSearchSpriteResource sprite, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, short cameraRoll)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        short counter = unchecked((short)task.ReadWord(12));
        bool second = task.Callback == 0x582850;
        bool advance = (inputs.GraphicsFlags & 0x201) == 0;
        byte[] bank = ReadState(frame).ExtraStorage.ToArray();
        if (advance && counter <= 2)
        {
            for (int emission = 0; emission < 14; emission++)
            {
                int row = TargetFree(bank);
                if (row == bank.Length)
                {
                    break;
                }
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(row), 4);
                GroupPut(bank, row + 4, 0);
                GroupPut(bank, row + 6, Rand() % (second ? 2048 : 3584) + (second ? 4608 : 6144));
                int angle = Rand() % 4096;
                int sine = NativeBattleCameraMath.Sine(angle);
                int cosine = NativeBattleCameraMath.Cosine(angle);
                int radius = Rand() % (second ? 250 : 200) + (second ? 400 : 500);
                GroupPut(bank, row + 8, unchecked(radius * sine) >> 12);
                GroupPut(bank, row + 10, -(Rand() % 50));
                GroupPut(bank, row + 12, unchecked(radius * cosine) >> 12);
                int speed = Rand() % (second ? 160 : 150) + (second ? 190 : 220);
                GroupPut(bank, row + 16, unchecked(speed * sine) >> 12);
                GroupPut(bank, row + 20, unchecked(speed * cosine) >> 12);
            }
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = unchecked((short)task.ReadWord(16)), Y = 0, Z = unchecked((short)task.ReadWord(20)) };
        NativeBattleActionMatrix transform = NativeBattlePoseMatrices.Compose56C2F0(view, world);
        int alive = 0;
        for (int row = 0; row < bank.Length; row += 32)
        {
            if (BinaryPrimitives.ReadInt32LittleEndian(bank.AsSpan(row)) != 4)
            {
                continue;
            }
            NativeBattleRenzokuken160Sprite current = new(sprite, unchecked((ushort)GroupWord(bank, row + 4)), new(GroupWord(bank, row + 8), GroupWord(bank, row + 10), GroupWord(bank, row + 12)), GroupWord(bank, row + 6), null, 8);
            NativeBattleRenzokuken160SpritePackets drawn = NativeBattleRenzokukenSpecialSpriteDrawing.Draw64DD70(current, transform, projection, cameraRoll);
            frame = inputs.Execute(frame, new NativeBattleRenzokukenSpecialSpriteDraw(drawn.Packets));
            if (!advance)
            {
                continue;
            }
            if (drawn.AnimationEnded)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bank.AsSpan(row), 0);
                continue;
            }
            GroupPut(bank, row + 4, GroupWord(bank, row + 4) + 1);
            for (int axis = 0; axis <= 2; axis += 2)
            {
                short speed = GroupWord(bank, row + 16 + axis * 2);
                GroupPut(bank, row + 8 + axis * 2, GroupWord(bank, row + 8 + axis * 2) + speed);
                GroupPut(bank, row + 16 + axis * 2, speed - (speed >> 3));
            }
            alive++;
        }
        frame = Store(frame, ReadState(frame) with { ExtraStorage = bank.ToImmutableArray() });
        if (!advance)
        {
            return new(frame, pool, 0);
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(frame, pool with { Slots = pool.Slots.SetItem(slot, task) }, alive == 0 ? 2 : 0);

        int Rand()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            return value;
        }
    }
}
