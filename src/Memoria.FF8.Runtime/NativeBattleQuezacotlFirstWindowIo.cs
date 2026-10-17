using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlFirstWindowIoStep(NativeBattleEffectMemory Memory, NativeBattleEffectFileRequest? Request, bool AdvancePhase);

/// <summary>6C4AA0/6C4AFE/6C4B43 poll and append their original first-window batches after geometry submission.</summary>
public static class NativeBattleQuezacotlFirstWindowIo
{
    public static NativeBattleQuezacotlFirstWindowIoStep Advance(NativeBattleEffectMemory memory, ushort phase)
    {
        if (phase is < 1 or > 354)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (phase is 30 or 60 or 76 or 85 or 161 or 211 or 256 or 276 or 302 or 318)
        {
            NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(memory);
            memory = poll.Memory;
            if (poll.Busy)
            {
                return new(memory, poll.Request, false);
            }
            switch (phase)
            {
                case 302:
                    ImmutableArray<uint> secondaryHeads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C4D3C requires its retained secondary descriptor heads.");
                    memory = memory with { QuezacotlSecondaryHeads = secondaryHeads.SetItem(0, 0).SetItem(1, 0).SetItem(2, 0) };
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 527, 0x2108954, 0x10);
                    break;
                case 276:
                    NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native6C4CFA requires its current impact-enable DWORD.");
                    ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C4CE9 requires its retained secondary descriptor head owners.");
                    memory = memory with { Arena = arena.Write(0x28E50, [0, 0, 0, 0]), QuezacotlSecondaryHeads = heads.SetItem(3, 0) };
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 525, 0x12A9664, 5);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 526, 0x2108954, 2);
                    break;
                case 256:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 523, 0x2108954, 1);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 524, 0x2108954, 3);
                    break;
                case 211:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 521, 0x1298C68, 5);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 522, 0x2108954, 1);
                    break;
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
                case 161:
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 516, 0x2108954, 0);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 517, 0x12A9664, 5);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 518, 0x2108954, 1);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 519, 0x2108954, 1);
                    memory = NativeBattleEffectIo.Queue5341D0(memory, 520, 0x2108954, 3);
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

    public static NativeBattleOriginalEffectState ResetPools6C4D3C(NativeBattleOriginalEffectState effect)
    {
        uint[] descriptors = [0x2521728, 0x2521718, 0x2521708];
        if (descriptors.Any(descriptor => !effect.Pools.Any(pool => pool.DescriptorAddress == descriptor)))
        {
            throw new NotSupportedException("Native6C4D3C requires its current ribbon, particle and path pool owners.");
        }
        return effect with { Pools = effect.Pools.Select(pool => descriptors.Contains(pool.DescriptorAddress) ? pool with { Head = null, Tail = null } : pool).ToImmutableArray() };
    }
}
