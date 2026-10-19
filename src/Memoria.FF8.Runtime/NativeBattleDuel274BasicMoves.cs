using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274MoveInputs(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<int, short, NativeBattleVector> ReadBone502170, Func<int, int, NativeBattleActionMatrix> ReadBoneMatrix68A790, Func<NativeBattleDuel274Frame, uint, NativeBattleDuel274Frame> DrawEcho68A7B0, Func<NativeBattleDuel274Frame, uint, NativeBattleDuel274Frame> CaptureEcho68ADF0, Func<NativeBattleDuel274Frame, NativeBattleGfGeometryBatch, NativeBattleDuel274Frame> Submit, Func<NativeBattleDuel274Frame, NativeBattleModulePoolTask, int, short, short, short, NativeBattleDuel274Frame> Draw571C80, Func<NativeBattleDuel274Frame, NativeBattleModulePoolTask, NativeBattleDuel274Frame> Draw68B270, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274Frame> Contact506690, Func<NativeBattleDuel274Frame, bool, NativeBattleDuel274Frame> Flash5712C0, Func<NativeBattleDuel274Frame, uint, NativeBattleDuel274Frame> Sound501330);

public static class NativeBattleDuel274BasicMoves
{
    public static NativeBattleDuel274Frame Advance68A050(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleModulePoolTask task = Target(frame, slot, 2);
        int actor = Actor(frame);
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (task.ReadWord(12) == 0)
        {
            PositionActor(frame, task, target, actor, inputs, 0);
            uint cursor = Cursor(frame);
            frame = NativeBattleDuel274Banks.Initialize508300(frame, 0x2508498, cursor, 36, 32);
            cursor = checked(cursor + 1152);
            frame = NativeBattleDuel274Banks.Initialize508300(frame, 0x2508488, cursor, 20, 2);
            cursor = checked(cursor + 40);
            task = task.WriteDword(72, cursor).WriteDword(76, checked(cursor + 332)).WriteDword(80, checked(cursor + 432));
            frame = Store(frame, slot, task);
            frame = SetCursor(frame, checked(cursor + 1592));
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, task.ReadDword(72), 0x10EA030, 332);
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, task.ReadDword(76), 0x10EC7D4, 100);
            frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, task.ReadDword(80), [13, 17, 21, 14, 18, 22]);
        }
        NativeBattleGraphicsModel targetModel = inputs.ReadModel(target);
        short yaw = unchecked((short)((inputs.ReadModel(actor).Rotation14 ?? throw new NotSupportedException("Native Punch Rush requires actor WORD14.")) + 2048));
        inputs.WriteModel(target, targetModel with { Rotation14 = yaw });
        frame = AdvanceParticles(frame, resources, inputs);
        frame = NativeBattleDuel274Banks.Advance508420(frame, 0x2508488, (current, secondarySlot) => Advance68B270(current, secondarySlot, inputs.Draw68B270));
        short counter = Signed(task, 12);
        if (unchecked((uint)(counter - 27)) < 15)
        {
            NativeBattleVector bone = inputs.ReadBone502170(actor, 22);
            NativeBattleActionMatrix world = inputs.ReadModel(0).GeometryWorldMatrix ?? throw new NotSupportedException("Native Punch Rush requires physical model zero's world matrix.");
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(world, inputs.ReadBoneMatrix68A790(actor, 18)) with { X = bone.X, Y = bone.Y, Z = bone.Z };
            frame = Draw(frame, task.ReadDword(76), local, -128, resources, view, projection, colors, inputs);
        }
        if (unchecked((uint)(counter - 33)) < 14)
        {
            NativeBattleVector bone = inputs.ReadBone502170(actor, 22);
            frame = Draw(frame, task.ReadDword(72), Yaw(-1024, bone.X, bone.Y, bone.Z - 200), -256, resources, view, projection, colors, inputs);
        }
        frame = inputs.DrawEcho68A7B0(frame, task.ReadDword(80));
        if (counter <= 26)
        {
            frame = inputs.CaptureEcho68ADF0(frame, task.ReadDword(80));
        }
        switch (unchecked((ushort)counter))
        {
            case 8:
                frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 21, 16, 0, 0, bone => inputs.ReadBone502170(actor, bone));
                frame = Emit68B210(frame, 21, 0, inputs);
                OffsetTarget(target, -50, inputs);
                break;
            case 13:
                frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 22, 16, 0, 0, bone => inputs.ReadBone502170(actor, bone));
                frame = Emit68B210(frame, 22, 0, inputs);
                OffsetTarget(target, -50, inputs);
                break;
            case 22:
                frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 21, 16, 1, 0, bone => inputs.ReadBone502170(actor, bone));
                frame = Emit68B210(frame, 21, 1, inputs);
                OffsetTarget(target, -50, inputs);
                break;
            case 9:
            case 14:
            case 23:
                OffsetTarget(target, 50, inputs);
                break;
            default:
                if (counter is >= 32 and <= 33)
                {
                    if (counter == 32)
                    {
                        frame = inputs.Contact506690(frame, task.ReadByte(14));
                    }
                    if (counter == 33)
                    {
                        frame = inputs.Flash5712C0(frame, true);
                        frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 22, 32, 0, 0, bone => inputs.ReadBone502170(actor, bone));
                    }
                    OffsetTarget(target, -200, inputs);
                }
                break;
        }
        return counter == 8 ? inputs.Sound501330(frame, 0x110BE58) : frame;
    }

    public static NativeBattleDuel274Frame Advance68B2F0(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleModulePoolTask task = Target(frame, slot, 3);
        int actor = Actor(frame);
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (task.ReadWord(12) == 0)
        {
            PositionActor(frame, task, target, actor, inputs, 1);
            uint cursor = Cursor(frame);
            frame = NativeBattleDuel274Banks.Initialize508300(frame, 0x2508498, cursor, 36, 64);
            cursor = checked(cursor + 2304);
            task = task.WriteDword(72, cursor).WriteDword(80, checked(cursor + 1480));
            frame = SetCursor(Store(frame, slot, task), checked(cursor + 2640));
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, cursor, 0x10ED700, 1480);
            frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, task.ReadDword(80), [9]);
        }
        frame = inputs.DrawEcho68A7B0(frame, task.ReadDword(80));
        frame = AdvanceParticles(frame, resources, inputs);
        NativeBattleGraphicsModel targetModel = inputs.ReadModel(target);
        inputs.WriteModel(target, targetModel with { Rotation14 = unchecked((short)((inputs.ReadModel(actor).Rotation14 ?? throw new NotSupportedException("Native Booya requires actor WORD14.")) + 2048)) });
        short counter = Signed(task, 12);
        if (counter is >= 31 and < 45)
        {
            if (counter == 31)
            {
                task = Point(task, 84, inputs.ReadBone502170(actor, 9));
                frame = Store(frame, slot, task);
            }
            frame = Draw(frame, task.ReadDword(72), Yaw(-1024, Signed(task, 84), Signed(task, 86), Signed(task, 88)), 0, resources, view, projection, colors, inputs);
        }
        if (counter == 9)
        {
            task = Point(task, 96, inputs.ReadBone502170(actor, 22));
            frame = Store(frame, slot, task);
        }
        if (counter is >= 10 and < 32)
        {
            NativeBattleVector bone = inputs.ReadBone502170(actor, 22);
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Booya requires actual target position fields.");
            NativeBattleVector previous = new(Signed(task, 96), Signed(task, 98), Signed(task, 100));
            inputs.WriteModel(target, model with { Transform = transform with { Position28 = new(unchecked((short)(transform.Position28.X + bone.X - previous.X)), unchecked((short)(transform.Position28.Y + bone.Y - previous.Y)), unchecked((short)(transform.Position28.Z + bone.Z - previous.Z))) } });
            task = Point(task, 96, bone).MarkPayloadUnknown(102, 2);
            frame = Store(frame, slot, task);
        }
        if (counter is >= 25 and <= 32)
        {
            frame = inputs.CaptureEcho68ADF0(frame, task.ReadDword(80));
        }
        if (counter == 31)
        {
            frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 22, 32, 2, 1, bone => inputs.ReadBone502170(actor, bone));
        }
        if (counter == 32)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(target);
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Booya restoration requires actual target position fields.");
            inputs.WriteModel(target, model with { Transform = transform with { Position28 = new(Signed(task, 48), Signed(task, 50), Signed(task, 52)), Vertical34 = Signed(task, 54) } });
            frame = inputs.Contact506690(frame, task.ReadByte(14));
        }
        if (counter == 33)
        {
            frame = inputs.Flash5712C0(frame, true);
        }
        if (counter is >= 35 and <= 38)
        {
            frame = Emit68B5E0(frame, actor, 9, inputs);
            frame = Emit68B5E0(frame, target, 240, inputs);
        }
        return counter == 13 ? inputs.Sound501330(frame, 0x110BE5C) : frame;
    }

    public static NativeBattleDuel274Frame Advance68B700(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleModulePoolTask task = Target(frame, slot, 4);
        frame = PublishBillboard(frame, view);
        int actor = Actor(frame);
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (task.ReadWord(12) == 0)
        {
            PositionActor(frame, task, target, actor, inputs, 2);
            uint cursor = Cursor(frame);
            task = task.WriteDword(72, cursor).WriteDword(76, checked(cursor + 864)).WriteDword(80, checked(cursor + 964));
            frame = SetCursor(Store(frame, slot, task), checked(cursor + 2124));
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, cursor, 0x10F3808, 864);
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, checked(cursor + 864), 0x10F6BE0, 100);
            frame = NativeBattleDuel274ArenaEcho.Initialize6A1230(frame, task.ReadDword(80), [12, 16, 20]);
        }
        frame = inputs.DrawEcho68A7B0(frame, task.ReadDword(80));
        short counter = Signed(task, 12);
        if (counter is >= 32 and < 46)
        {
            if (counter == 32)
            {
                task = Point(task, 84, inputs.ReadBone502170(actor, 20));
                frame = Store(frame, slot, task);
            }
            frame = Draw(frame, task.ReadDword(72), Yaw(0, Signed(task, 84), Signed(task, 86), Signed(task, 88)), -256, resources, view, projection, colors, inputs);
            frame = Draw(frame, task.ReadDword(76), Yaw(0, Signed(task, 84), 0, Signed(task, 88)), -256, resources, view, projection, colors, inputs);
        }
        if (counter is >= 25 and <= 32)
        {
            frame = inputs.CaptureEcho68ADF0(frame, task.ReadDword(80));
        }
        if (counter == 32)
        {
            frame = inputs.Contact506690(frame, task.ReadByte(14));
        }
        if (counter == 33)
        {
            frame = inputs.Flash5712C0(frame, true);
        }
        return counter == 30 ? inputs.Sound501330(frame, 0x110BE60) : frame;
    }

    public static NativeBattleDuel274TaskStep Advance68B270(NativeBattleDuel274Frame frame, int slot, Func<NativeBattleDuel274Frame, NativeBattleModulePoolTask, NativeBattleDuel274Frame> draw)
    {
        int index = NativeBattleDuel274Banks.Index(frame, 0x2508488);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.Stride != 20 || task.Callback != 0x68B270)
        {
            throw new InvalidDataException("Native Duel secondary sprite requires its original task dimensions and callback.");
        }
        frame = draw(frame, task);
        pool = frame.Effect.Pools[index];
        task = pool.Slots[slot];
        ushort counter = unchecked((ushort)(task.ReadWord(18) + 1));
        pool = pool with { Slots = pool.Slots.SetItem(slot, task.WriteWord(18, counter)) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
        return new(frame, pool, counter < 8 ? 0 : 2);
    }

    private static NativeBattleDuel274Frame Emit68B210(NativeBattleDuel274Frame frame, short bone, int mode, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleVector position = inputs.ReadBone502170(Actor(frame), bone);
        int index = NativeBattleDuel274Banks.Index(frame, 0x2508488);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int slot) = pool.Append(0x68B270);
        NativeBattleModulePoolTask task = Point(pool.Slots[slot], 12, position).WriteWord(18, 0);
        if (mode == 0)
        {
            task = task.WriteWord(16, unchecked((ushort)(position.Z - 128)));
        }
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool with { Slots = pool.Slots.SetItem(slot, task) }) } };
    }

    private static NativeBattleDuel274Frame Emit68B5E0(NativeBattleDuel274Frame frame, int model, short bone, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleVector position = inputs.ReadBone502170(model, bone);
        int index = NativeBattleDuel274Banks.Index(frame, 0x2508498);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        NativeCrtRandomState random = frame.Random;
        for (int count = 0; count < 2 && pool.Slots.Any(task => (task.Flags & 1) == 0); count++)
        {
            (pool, int slot) = pool.Append(0x68B140);
            int radius = ((inputs.ReadModel(model).Transform ?? throw new NotSupportedException("Native Booya sparkle requires actual model scale.")).Scale38 + 4096) >> 4;
            random = random.Draw(out ushort first);
            random = random.Draw(out ushort second);
            int radial = radius * NativeBattleCameraMath.Cosine(first) >> 12;
            NativeBattleVector point = new(unchecked((short)(position.X - (radial * NativeBattleCameraMath.Sine(second) >> 12))), unchecked((short)(position.Y - (radius * NativeBattleCameraMath.Sine(first) >> 12))), unchecked((short)(position.Z + (radial * NativeBattleCameraMath.Cosine(second) >> 12))));
            NativeBattleModulePoolTask task = Point(pool.Slots[slot], 12, point).WriteWord(18, 0).WriteWord(26, 2);
            for (int offset = 20; offset <= 32; offset += 2)
            {
                if (offset != 26)
                {
                    task = task.WriteWord(offset, 0);
                }
            }
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        }
        return frame with { Random = random, Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    }

    private static NativeBattleDuel274Frame AdvanceParticles(NativeBattleDuel274Frame frame, NativeBattleDuel274Resources resources, NativeBattleDuel274MoveInputs inputs) => NativeBattleDuel274Banks.Advance508420(frame, 0x2508498, (current, slot) => NativeBattleDuel274Particles.Advance68B140(current, slot, resources, inputs.Draw571C80));

    private static NativeBattleDuel274Frame Draw(NativeBattleDuel274Frame frame, uint workspace, NativeBattleActionMatrix local, int bias, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleActionMatrix? billboard = null;
        if (frame.Effect.Pools[1].Slots[0].ReadByte(15) == 4)
        {
            billboard = NativeBattleGfMatrices.Normalize67AAE0(view);
        }
        uint morph = checked(BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2508430, 4)) + 116);
        NativeBattleDuel274HierarchyContext context = new(NativeBattleGfMatrices.Compose56C2F0(view, local), billboard, bias, 0, null, morph);
        return NativeBattleDuel274Workspaces.Advance701970(frame, resources, workspace, context, projection, colors, inputs.Submit);
    }

    private static NativeBattleDuel274Frame PublishBillboard(NativeBattleDuel274Frame frame, NativeBattleActionMatrix view)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Normalize67AAE0(view);
        for (int index = 0; index < 9; index++)
        {
            frame = frame with { Staging = frame.Staging.Write(checked(0x2508440u + (uint)index * 2), BitConverter.GetBytes(matrix.Rotation[index])) };
        }
        return frame with { Staging = frame.Staging.Write(0x2508454, BitConverter.GetBytes(matrix.X)).Write(0x2508458, BitConverter.GetBytes(matrix.Y)).Write(0x250845C, BitConverter.GetBytes(matrix.Z)) };
    }

    private static void PositionActor(NativeBattleDuel274Frame frame, NativeBattleModulePoolTask task, int target, int actor, NativeBattleDuel274MoveInputs inputs, int mode)
    {
        NativeBattleGraphicsModel model = inputs.ReadModel(actor);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel move requires actual actor position and scale.");
        NativeBattleModelTransform targetTransform = inputs.ReadModel(target).Transform ?? throw new NotSupportedException("Native Duel move requires actual target scale.");
        int distance = (targetTransform.Scale38 >> 1) + (mode == 1 ? (transform.Scale38 >> 1) + 128 : mode == 2 ? transform.Scale38 + 256 : transform.Scale38 + (transform.Scale38 >> 1));
        int angle = (model.Rotation14 ?? throw new NotSupportedException("Native Duel move requires actual actor yaw.")) + 2048;
        NativeBattleVector position = transform.Position28 with { X = unchecked((short)(Signed(task, 40) - (distance * NativeBattleCameraMath.Sine(angle) >> 12))), Z = unchecked((short)(Signed(task, 44) - (distance * NativeBattleCameraMath.Cosine(angle) >> 12))) };
        inputs.WriteModel(actor, model with { Transform = transform with { Position28 = position } });
    }

    private static void OffsetTarget(int target, int delta, NativeBattleDuel274MoveInputs inputs)
    {
        NativeBattleGraphicsModel model = inputs.ReadModel(target);
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel impact requires actual target WORD32.");
        inputs.WriteModel(target, model with { Transform = transform with { Position28 = transform.Position28 with { Z = unchecked((short)(transform.Position28.Z + delta)) } } });
    }

    private static NativeBattleModulePoolTask Target(NativeBattleDuel274Frame frame, int slot, byte selector)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x25084A8 || task.Callback != 0x689AC0 || task.ReadByte(15) != selector)
        {
            throw new InvalidDataException("Native Duel move requires its selected original target task.");
        }
        return task;
    }

    private static NativeBattleActionMatrix Yaw(short angle, int x, int y, int z) => NativeBattleGfMatrices.Euler701310(new(0, angle, 0)) with { X = x, Y = y, Z = z };
    private static int Actor(NativeBattleDuel274Frame frame) => (frame.Effect.Parameters ?? throw new NotSupportedException("Native Duel move requires actual actor parameters.")).Actor78;
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Cursor(NativeBattleDuel274Frame frame) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2508434, 4));
    private static NativeBattleDuel274Frame SetCursor(NativeBattleDuel274Frame frame, uint cursor) => frame with { Staging = frame.Staging.Write(0x2508434, BitConverter.GetBytes(cursor)) };
    private static NativeBattleModulePoolTask Point(NativeBattleModulePoolTask task, int offset, NativeBattleVector point) => task.WriteWord(offset, unchecked((ushort)point.X)).WriteWord(offset + 2, unchecked((ushort)point.Y)).WriteWord(offset + 4, unchecked((ushort)point.Z));

    private static NativeBattleDuel274Frame Store(NativeBattleDuel274Frame frame, int slot, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(slot, task) }) } };
    }
}
