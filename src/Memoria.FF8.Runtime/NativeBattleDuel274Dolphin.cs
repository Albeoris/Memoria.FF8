using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274DolphinInputs(Func<int, NativeBattleGraphicsModel> ReadModel, Action<int, NativeBattleGraphicsModel> WriteModel, Func<short, NativeBattleVector> ReadBone502170, Func<NativeBattleDuel274Frame, NativeBattleModulePoolTask, int, short, short, short, NativeBattleDuel274Frame> Draw571C80, Func<NativeBattleDuel274Frame, NativeBattleGfGeometryBatch, NativeBattleDuel274Frame> Submit, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274Frame> Contact506690, Func<NativeBattleDuel274Frame, NativeBattleDuel274Frame> Flash5712C0, Func<NativeBattleDuel274Frame, uint, NativeBattleDuel274Frame> Sound501330);

public static class NativeBattleDuel274Dolphin
{
    public static NativeBattleDuel274Frame Advance68C7B0(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274Resources resources, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleDuel274DolphinInputs inputs)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        if (frame.Effect.Pools[1].DescriptorAddress != 0x25084A8 || task.Callback != 0x689AC0 || task.ReadByte(15) != 6)
        {
            throw new InvalidDataException("Native Dolphin Blow requires its selected Duel target task.");
        }
        NativeBattleActionMatrix billboard = NativeBattleGfMatrices.Normalize67AAE0(view);
        for (int index = 0; index < 9; index++)
        {
            frame = frame with { Staging = frame.Staging.Write(checked(0x2508440u + (uint)index * 2), BitConverter.GetBytes(billboard.Rotation[index])) };
        }
        frame = frame with { Staging = frame.Staging.Write(0x2508454, BitConverter.GetBytes(billboard.X)).Write(0x2508458, BitConverter.GetBytes(billboard.Y)).Write(0x250845C, BitConverter.GetBytes(billboard.Z)) };
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Dolphin Blow requires its actor parameter owner.");
        int actor = parameters.Actor78;
        int target = NativeBattleDuel274Controller.Index(task.ReadDword(20));
        if (task.ReadWord(12) == 0)
        {
            NativeBattleGraphicsModel actorModel = inputs.ReadModel(actor);
            NativeBattleModelTransform actorTransform = actorModel.Transform ?? throw new NotSupportedException("Native Dolphin Blow requires its actual actor transform.");
            NativeBattleModelTransform targetTransform = inputs.ReadModel(target).Transform ?? throw new NotSupportedException("Native Dolphin Blow requires its actual target scale.");
            int yaw = (actorModel.Rotation14 ?? throw new NotSupportedException("Native Dolphin Blow requires actor WORD14.")) + 2048;
            int distance = (targetTransform.Scale38 >> 1) + actorTransform.Scale38 + (actorTransform.Scale38 >> 1);
            NativeBattleVector position = actorTransform.Position28 with { X = unchecked((short)(Signed(task, 40) - (distance * NativeBattleCameraMath.Sine(yaw) >> 12))), Z = unchecked((short)(Signed(task, 44) - (distance * NativeBattleCameraMath.Cosine(yaw) >> 12))) };
            inputs.WriteModel(actor, actorModel with { Transform = actorTransform with { Position28 = position } });
            task = task.WriteDword(84, Pack(position.X, position.Y)).WriteDword(88, Pack(position.Z, actorTransform.Vertical34));
            uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2508434, 4));
            task = task.WriteDword(72, cursor).WriteDword(76, checked(cursor + 224));
            Store();
            frame = NativeBattleDuel274Banks.Initialize508300(frame, 0x2508498, checked(cursor + 1128), 36, 64);
            frame = frame with { Staging = frame.Staging.Write(0x2508434, BitConverter.GetBytes(checked(cursor + 1128 + 2304))) };
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, cursor, 0x10FE310, 224);
            frame = NativeBattleDuel274Workspaces.Initialize7016B0(frame, resources, checked(cursor + 224), 0x110198C, 904);
        }
        task = frame.Effect.Pools[1].Slots[slot];
        short counter = Signed(task, 12);
        uint morph = checked(BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2508430, 4)) + 116);
        if (counter < 16)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(new(0, 2048, 0)) with { X = Signed(task, 84), Y = Signed(task, 86), Z = Signed(task, 88) - 500 };
            NativeBattleDuel274HierarchyContext context = new(NativeBattleGfMatrices.Compose56C2F0(view, local), billboard, -256, 1, null, morph);
            frame = NativeBattleDuel274Workspaces.Advance701970(frame, resources, task.ReadDword(72), context, projection, colors, inputs.Submit);
        }
        uint elapsed = unchecked((uint)(counter - 20));
        if (elapsed < 50)
        {
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(new(0, 2048, 0)) with { X = Signed(task, 84), Y = Signed(task, 86), Z = Signed(task, 88) - 1000 };
            NativeBattleDuel274HierarchyContext context = new(NativeBattleGfMatrices.Compose56C2F0(view, local), billboard, 0, 2, checked((ushort)(8 * (elapsed & 15))), morph);
            frame = NativeBattleDuel274Workspaces.Advance701970(frame, resources, task.ReadDword(76), context, projection, colors, inputs.Submit);
        }
        frame = NativeBattleDuel274Banks.Advance508420(frame, 0x2508498, (current, particleSlot) => NativeBattleDuel274Particles.Advance68B140(current, particleSlot, resources, inputs.Draw571C80));
        if (counter == 30)
        {
            frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 241, 16, 3, 3, inputs.ReadBone502170);
        }
        if (counter is >= 32 and < 40)
        {
            frame = NativeBattleDuel274Particles.Emit68AEE0(frame, 241, 8, 4, 4, inputs.ReadBone502170);
        }
        if (counter == 31)
        {
            frame = inputs.Flash5712C0(frame);
        }
        if (counter is >= 32 and <= 33)
        {
            if (counter == 33)
            {
                frame = inputs.Contact506690(frame, task.ReadByte(14));
            }
            NativeBattleGraphicsModel targetModel = inputs.ReadModel(target);
            NativeBattleModelTransform targetTransform = targetModel.Transform ?? throw new NotSupportedException("Native Dolphin Blow impact requires the actual target position.");
            inputs.WriteModel(target, targetModel with { Transform = targetTransform with { Position28 = targetTransform.Position28 with { Z = unchecked((short)(targetTransform.Position28.Z - 200)) } } });
        }
        if (counter == 24)
        {
            frame = inputs.Sound501330(frame, 0x110BE68);
        }
        return frame;

        void Store()
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool.WriteSlot(slot, task)) } };
        }
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static uint Pack(short low, short high) => (uint)unchecked((ushort)low) | (uint)unchecked((ushort)high) << 16;
}
