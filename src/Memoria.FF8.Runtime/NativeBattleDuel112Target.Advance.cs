using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112TargetInputs(NativeBattleDuel112LateInputs Drawing, Action<uint> Sound501330, Action<int> Stage5713E0, Func<NativeBattleDuel112Frame, int, NativeBattleDuel112Frame> Contact506690);
public sealed record NativeBattleDuel112TargetStep(NativeBattleDuel112Frame Frame, NativeBattleEffectTaskPool Pool, int Result, NativeBattleLogicalOrdering Ordering);

public static partial class NativeBattleDuel112Target
{
    public static NativeBattleDuel112TargetStep Advance6CBF90(NativeBattleDuel112Frame frame, int slot, NativeBattleDuel112TargetInputs inputs, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        if (frame.Effect.Pools[1].DescriptorAddress != 0x2521950 || task.Callback != 0x6CBF90)
        {
            throw new InvalidDataException("Native Duel target requires its actual callback and task bank.");
        }
        short phase = unchecked((short)task.ReadWord(12));
        if (phase is 2 or 73)
        {
            NativeBattleDuel112Resources resources = frame.Memory.Duel112?.Resources ?? throw new NotSupportedException("Native Duel sound requires its original static descriptor.");
            inputs.Sound501330(BinaryPrimitives.ReadUInt32LittleEndian(resources.Read(phase == 2 ? 0x12DB654u : 0x12DB658u, 4).AsSpan()));
        }
        inputs.Stage5713E0(StageIntensity6CBF90(phase));
        int actor = frame.Memory.Duel112?.Parameters?.Actor78 ?? throw new NotSupportedException("Native Duel target requires its actual actor.");
        NativeBattleDuel112DrawingInputs drawing = inputs.Drawing.Drawing;
        if (phase == 7)
        {
            Backstep6CBF90(actor, drawing.Actor);
        }
        NativeBattleDuel112DrawingStep early = DrawEarly6CBF90(frame, slot, drawing, ordering);
        NativeBattleDuel112DrawingStep middle = DrawMiddle6CBF90(early.Frame, slot, drawing, early.Ordering);
        NativeBattleDuel112DrawingStep late = DrawLate6CBF90(middle.Frame, slot, inputs.Drawing, middle.Ordering);
        frame = late.Frame;
        ordering = late.Ordering;
        int elapsed = phase - 69;
        if ((uint)elapsed < 65)
        {
            short yaw = drawing.Actor.ReadModel(actor).Rotation14 ?? throw new NotSupportedException("Native Duel final particles require the actual actor yaw.");
            frame = NativeBattleDuel112Births.Late6CBF90(frame, slot, yaw);
            task = frame.Effect.Pools[1].Slots[slot];
            int target = ModelIndex(task.ReadDword(176));
            NativeBattleGraphicsModel model = drawing.Actor.ReadModel(target);
            short scale = model.Transform?.Scale38 ?? throw new NotSupportedException("Native Duel contact requires its actual target scale.");
            if (elapsed == (scale < 3072 ? 50 : 45))
            {
                drawing.Actor.WriteModel(target, model with { Flags0 = (ushort)(model.Flags0 & 0xFFF7) });
                frame = inputs.Contact506690(frame, unchecked((short)task.ReadWord(14)));
            }
        }
        frame = Trajectory6CBF90(frame, slot, drawing.Actor, inputs.Drawing.ReadAnchor571400);
        NativeBattleDuel112TaskStep finished = Finish6CBF90(frame, slot, drawing.Actor);
        return new(finished.Frame, finished.Pool, finished.Result, ordering);
    }
}
