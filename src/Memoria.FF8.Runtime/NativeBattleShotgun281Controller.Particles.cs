namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShotgun281Controller
{
    public static (NativeBattleShotgun281Frame Frame, int Result) AdvanceSprite67E5F0(NativeBattleShotgun281Frame frame, uint address, NativeBattleShotgun281Resources resources, NativeBattleShotgun281Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleVector position = new(Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, Signed(task, 0x16), 800, -200) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(task.ReadDword(12))).Write(4, BitConverter.GetBytes(task.ReadWord(0x16))).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleShotgun281DrawSprite180(temporary, task.ReadDword(12)));
        frame = Release(frame, 180);
        task = Read(frame, address);
        ushort x = unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x18)));
        ushort y = unchecked((ushort)(task.ReadWord(0x12) + task.ReadWord(0x1A)));
        ushort z = unchecked((ushort)(task.ReadWord(0x14) + task.ReadWord(0x1C)));
        ushort vx = unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x20)));
        ushort vy = unchecked((ushort)(task.ReadWord(0x1A) + task.ReadWord(0x22)));
        ushort vz = unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x24)));
        short phase = unchecked((short)(task.ReadWord(0x16) + 1));
        task = task.WriteWord(0x10, x).WriteWord(0x12, y).WriteWord(0x14, z).WriteWord(0x18, vx).WriteWord(0x1A, vy).WriteWord(0x1C, vz).WriteWord(0x16, unchecked((ushort)phase));
        frame = Write(frame, address, task);
        return (frame, phase < Signed(task, 0x1E) ? 0 : 2);
    }
}
