namespace Memoria.FF8.Runtime;

public static class NativeBattleProcessFileBanks
{
    public static NativeBattleChoco97IoBanks Read(NativeBattleEffectMemory memory)
    {
        NativeBattleChoco97State owner = memory.Choco97 ?? throw new NotSupportedException("Native process file banks require their retained canonical owner.");
        return owner.Io?.Banks ?? owner.PendingIoBanks ?? throw new NotSupportedException("Native process file banks are unknown.");
    }

    public static NativeBattleEffectMemory Write(NativeBattleEffectMemory memory, NativeBattleChoco97IoBanks banks)
    {
        banks.Validate();
        NativeBattleChoco97State owner = memory.Choco97 ?? throw new NotSupportedException("Native process file banks require their retained canonical owner.");
        if (owner.Io is { } io)
        {
            return memory with { Choco97 = owner with { Io = io with { Banks = banks } } };
        }
        if (owner.PendingIoBanks is null)
        {
            throw new NotSupportedException("Native process file banks are unknown.");
        }
        return memory with { Choco97 = owner with { PendingIoBanks = banks } };
    }
}
