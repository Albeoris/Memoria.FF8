using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112LateInputs(NativeBattleDuel112DrawingInputs Drawing, Func<int, NativeBattleModuleAnchor> ReadAnchor571400);

public static partial class NativeBattleDuel112Target
{
    public static NativeBattleDuel112DrawingStep DrawLate6CBF90(NativeBattleDuel112Frame frame, int slot, NativeBattleDuel112LateInputs inputs, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        if (frame.Effect.Pools[1].DescriptorAddress != 0x2521950 || task.Callback != 0x6CBF90)
        {
            throw new InvalidDataException("Native Duel final hierarchy requires its actual target task owner.");
        }
        int elapsed = unchecked((short)task.ReadWord(12)) - 69;
        if ((uint)elapsed >= 65)
        {
            return new(frame, ordering);
        }
        NativeBattleDuel112DrawingInputs drawing = inputs.Drawing;
        int actor = frame.Memory.Duel112?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel final hierarchy requires its actual actor.");
        if (elapsed == 38)
        {
            Append(NativeBattleDuelSharedPackets.Flash6CBF90());
        }
        else if (elapsed == 39)
        {
            drawing.Flash5712C0(0, 1, 1, 192);
        }
        if (elapsed >= 16)
        {
            Append(NativeBattleDuelSharedPackets.Flash6A83B0(40, 55, 50, elapsed - 16, 3));
        }
        if (elapsed >= 32)
        {
            Append(NativeBattleDuelSharedPackets.Flash6A83B0(40, 55, 50, elapsed - 32, 3));
        }
        if (elapsed == 0)
        {
            frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 2316);
            frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 2584);
            frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 3480);
            frame = NativeBattleDuel112Workspaces.Initialize7016B0(frame, slot, 3704);
            NativeBattleGraphicsModel model = drawing.Actor.ReadModel(actor);
            int yaw = model.Rotation14 ?? throw new NotSupportedException("Native Duel final hierarchy requires its actual actor yaw.");
            if (yaw > 2048)
            {
                yaw -= 4096;
            }
            short halfYaw = unchecked((short)(yaw >> 1));
            task = frame.Effect.Pools[1].Slots[slot];
            ImmutableArray<short> rotation = NativeBattleDuelSharedPackets.Rotation6D9510(halfYaw);
            for (int word = 0; word < 9; word++)
            {
                task = task.WriteWord(140 + word * 2, unchecked((ushort)rotation[word]));
            }
            int target = ModelIndex(task.ReadDword(176));
            NativeBattleModuleAnchor anchor = inputs.ReadAnchor571400(target);
            task = task.WriteWord(92, unchecked((ushort)anchor.X)).WriteWord(94, unchecked((ushort)anchor.Y)).WriteWord(96, unchecked((ushort)anchor.Z)).WriteWord(98, unchecked((ushort)anchor.W));
            short scale = drawing.Actor.ReadModel(target).Transform?.Scale38 ?? throw new NotSupportedException("Native Duel final hierarchy requires its actual target scale.");
            NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native Duel final hierarchy requires its actual actor position fields.");
            int sine = NativeBattleCameraMath.Sine(halfYaw);
            int cosine = NativeBattleCameraMath.Cosine(halfYaw);
            short x = unchecked((short)(anchor.X - (unchecked((scale + 5000) * sine) >> 14)));
            short z = unchecked((short)(anchor.Z - (unchecked((scale + 5000) * cosine) >> 14)));
            model = model with { Rotation14 = halfYaw, Transform = transform with { Position28 = new(x, 0, z), Vertical34 = anchor.W } };
            drawing.Actor.WriteModel(actor, model);
            task = task.WriteWord(100, unchecked((ushort)(x - (sine >> 2)))).WriteWord(102, 0).WriteWord(104, unchecked((ushort)(z - (cosine >> 2))));
            Store();
        }
        task = frame.Effect.Pools[1].Slots[slot];
        SetTranslation(100);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(drawing.View, Matrix140());
        Draw(3480, new(0x12DB6D8, MorphAddress(frame), null, null, basis));
        if (elapsed >= 41)
        {
            Draw(3704, new(0x12DB6D8, MorphAddress(frame), null, null, basis));
        }
        task = frame.Effect.Pools[1].Slots[slot];
        (int xView, int yView, int zView) = NativeBattleGfMatrices.RotateLong56C600(drawing.View, Signed(92), Signed(94), Signed(96));
        NativeBattleActionMatrix cameraFacing = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], unchecked(xView + drawing.View.X), unchecked(yView + drawing.View.Y), unchecked(zView + drawing.View.Z), 4096);
        short depth = unchecked((short)(task.ReadWord(172) + 200));
        Draw(2584, new(0x12DB6E0, MorphAddress(frame), null, depth, cameraFacing));
        if (elapsed != 2 && elapsed >= 38)
        {
            task = frame.Effect.Pools[1].Slots[slot];
            SetTranslation(92);
            basis = NativeBattleGfMatrices.Compose56C2F0(drawing.View, Matrix140());
            Draw(2316, new(0x12DB720, MorphAddress(frame), null, depth, basis));
        }
        return new(frame, ordering);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void SetTranslation(int offset)
        {
            task = task.WriteDword(160, unchecked((uint)Signed(offset))).WriteDword(164, unchecked((uint)Signed(offset + 2))).WriteDword(168, unchecked((uint)Signed(offset + 4)));
            Store();
        }
        NativeBattleActionMatrix Matrix140() => new(Enumerable.Range(0, 9).Select(index => Signed(140 + 2 * index)).ToImmutableArray(), unchecked((int)task.ReadDword(160)), unchecked((int)task.ReadDword(164)), unchecked((int)task.ReadDword(168)), 4096);
        void Draw(int offset, NativeBattleDuel112HierarchyContext context)
        {
            NativeBattleDuel112WorkspaceStep step = NativeBattleDuel112Workspaces.Advance701970(frame, slot, offset, context, drawing.View, drawing.Projection, drawing.Colors, ordering, drawing.CapturePresentation);
            frame = step.Frame;
            ordering = step.Ordering;
        }
        void Append(NativeBattleModulePacketBatch batch)
        {
            NativeBattleDuel112DrawingStep step = Append6CBF90(frame, ordering, CaptureFixedFlash(batch, drawing.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(112, frame.Effect.Pools[1].Slots[slot], 0x6CBF90) : null));
            frame = step.Frame;
            ordering = step.Ordering;
        }
        void Store()
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool.WriteSlot(slot, task)) } };
        }
    }
}
