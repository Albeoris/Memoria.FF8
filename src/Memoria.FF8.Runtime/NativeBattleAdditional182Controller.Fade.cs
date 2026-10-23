namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional182Controller
{
    public static (NativeBattleAdditional182Frame Frame, int Result) AdvanceActorFade(NativeBattleAdditional182Frame frame, uint address, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5CB390)
        {
            throw new NotSupportedException("Additional effect actor callback is unsupported.");
        }
        short age = Signed(task, 12);
        int actor = Signed(task, 14);
        int factor = unchecked(4096 - NativeBattleCameraMath.Sine((age << 10) / Signed(task, 0x18)));
        frame = inputs.Execute(frame, new NativeBattleAdditional182ActorAlpha(actor, unchecked((byte)((uint)unchecked(factor * 255) >> 12))));
        frame = inputs.Execute(frame, new NativeBattleAdditional182ActorRecolor(actor, factor));
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleAdditional182ActorFlags(actor, 0, 0x800));
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) < Signed(task, 0x18))
        {
            return (frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleAdditional182ActorFlags(actor, 0x800, unchecked((ushort)(task.ReadWord(0x1A) | 4))));
        return (frame, 2);
    }
}
