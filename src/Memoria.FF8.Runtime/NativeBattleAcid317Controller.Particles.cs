namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid317Controller
{
    private static (NativeBattleAcid317Frame Frame, int Result) Particle(NativeBattleAcid317Frame frame, uint address, NativeBattleAcid317Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        if (callback is not (0x660830 or 0x6608D0 or 0x660D40))
        {
            throw new NotSupportedException("Acid reached an unsupported particle callback.");
        }
        uint source = callback == 0x660830 ? 0x10016A8u : callback == 0x6608D0 ? 0x100139Cu : 0x10014FCu;
        short scale = Signed(task, 0x16);
        NativeBattleVector position = new(Signed(task, 0x10), Signed(task, 0x12), Signed(task, 0x14));
        int depth = callback == 0x660830 ? 0 : -(scale >> 2);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), position, scale, scale, unchecked((short)depth)) };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Acid particles require their physical temporary memory."), 180);
        ushort phase = task.ReadWord(12);
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(allocation.Staging, allocation.Address, source, 0).Write(allocation.Address + 4, BitConverter.GetBytes(phase));
        frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleAcid317DrawSprite180(allocation.Address, source, phase));
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging ?? throw new NotSupportedException("Acid lost its physical particle temporary."), 180) };
        task = Read(frame, address);
        if (callback != 0x660D40)
        {
            task = task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x18)))).WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + task.ReadWord(0x1C))));
            if (callback == 0x660830)
            {
                task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + task.ReadWord(0x1A)))).WriteWord(0x1A, unchecked((ushort)(task.ReadWord(0x1A) + 16)));
            }
            else
            {
                task = task.WriteWord(0x1A, unchecked((ushort)(task.ReadWord(0x1A) + 1)));
                task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + task.ReadWord(0x1A))));
            }
        }
        uint next = unchecked(task.ReadDword(12) + 1);
        int duration = callback == 0x660830 ? 9 : callback == 0x6608D0 ? 13 : 16;
        return (Write(frame, address, task.WriteDword(12, next)), unchecked((int)next) < duration ? 0 : 2);
    }
}
