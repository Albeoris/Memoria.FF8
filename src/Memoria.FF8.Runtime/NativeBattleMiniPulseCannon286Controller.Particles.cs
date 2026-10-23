namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMiniPulseCannon286Controller
{
    public static (NativeBattleMiniPulseCannon286Frame Frame, int Result) AdvanceSprite67A7C0(NativeBattleMiniPulseCannon286Frame frame, uint address, NativeBattleMiniPulseCannon286Resources resources, NativeBattleMiniPulseCannon286Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int depth = unchecked(-(unchecked((int)task.ReadDword(16)) >> 2));
        NativeBattleVector point = new(Signed(task, 20), Signed(task, 22), Signed(task, 24));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, Signed(task, 26), Signed(task, 16), unchecked((short)depth)) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(task.ReadDword(12))).Write(4, BitConverter.GetBytes(task.ReadWord(26))).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleMiniPulseCannon286DrawSprite180(temporary, task.ReadDword(12)));
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            task = task.WriteWord(20 + axis * 2, unchecked((ushort)(task.ReadWord(20 + axis * 2) + task.ReadWord(28 + axis * 2))));
        }
        short phase = unchecked((short)(task.ReadWord(26) + 1));
        task = task.WriteWord(26, unchecked((ushort)phase));
        frame = Write(frame, address, task);
        return (frame, phase < Signed(task, 34) ? 0 : 2);
    }
}
