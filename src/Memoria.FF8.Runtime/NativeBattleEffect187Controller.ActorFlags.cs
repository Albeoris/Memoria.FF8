namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect187Controller
{
    public static NativeBattleEffect187Frame RestoreActorFlags64DC70(NativeBattleEffect187Frame frame, ushort delay, NativeBattleEffect187Inputs inputs)
    {
        NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect187 actor flag restoration requires its actual parameters.");
        NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
        NativeBattleActionHeaderSlot header = packets.Headers[parameters.HeaderIndex7C];
        int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect187 actor flag restoration requires its current contacts.");
        for (int index = 0; index < header.ReadByte(0x10); index++)
        {
            int actor = packets.ReadContact(first + index).Primary.ActorIndex;
            (frame, uint address) = AppendRequired(frame, 0x64DCF0);
            ushort saved = (ushort)(inputs.ReadModel(frame, actor).Flags0 & 8);
            frame = Write(frame, address, Read(frame, address).WriteWord(12, 0).WriteWord(14, delay).WriteWord(0x18, checked((ushort)actor)).WriteWord(0x1A, saved));
            frame = inputs.Execute(frame, new NativeBattleEffect187ModelFlags(actor, ushort.MaxValue, 8));
        }
        return frame;
    }

    public static NativeBattleEffect187Step AdvanceActorFlags64DCF0(NativeBattleEffect187Frame frame, uint address, NativeBattleEffect187Inputs inputs)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) < Signed(task, 14))
        {
            return new(frame, 0);
        }
        int actor = Signed(task, 0x18);
        frame = inputs.Execute(frame, new NativeBattleEffect187ModelFlags(actor, 0xFFF7, task.ReadWord(0x1A)));
        return new(frame, 2);
    }
}
