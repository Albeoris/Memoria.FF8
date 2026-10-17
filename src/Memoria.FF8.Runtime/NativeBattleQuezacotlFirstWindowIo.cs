namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlFirstWindowIoStep(NativeBattleEffectMemory Memory, NativeBattleEffectFileRequest? Request, bool AdvancePhase);

/// <summary>6C4AA0/6C4AFE/6C4B43 poll and append their original first-window batches after geometry submission.</summary>
public static class NativeBattleQuezacotlFirstWindowIo
{
    public static NativeBattleQuezacotlFirstWindowIoStep Advance(NativeBattleEffectMemory memory, ushort phase)
    {
        if (phase is < 1 or > 87)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (phase is 30 or 60 or 76 or 85)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(memory);
            memory = poll.Memory;
            if (poll.Busy)
            {
                return new(memory, poll.Request, false);
            }
            switch (phase)
            {
                case 30:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 508, 0x2108954, 2);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 509, 0x2108954, 1);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 510, 0x2108954, 3);
                    break;
                case 60:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 511, 0x2108954, 1);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 512, 0x2111154, 5);
                    break;
                case 76:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 513, 0x1298C68, 5);
                    break;
                case 85:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 514, 0x2108954, 0);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 515, 0x12A1164, 5);
                    break;
            }
        }
        NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(memory);
        return new(advance.Memory, advance.Request, true);
    }
}
