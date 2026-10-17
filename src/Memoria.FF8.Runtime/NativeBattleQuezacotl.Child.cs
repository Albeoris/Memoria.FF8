using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuezacotlChildStep(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffectFileRequest? FileRequest, bool Entered);

public static class NativeBattleQuezacotlChild
{
    public static NativeBattleQuezacotlChildStep AdvanceFirst6C3940(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect)
    {
        NativeBattleEffectTaskPool child = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521738);
        short phase = unchecked((short)child.Slots[0].ReadWord(12));
        NativeBattleQuezacotlState state = (effect.Quezacotl ?? throw new InvalidDataException("Native6C3940 lost its initialized Quezacotl globals.")) with { Intensity17A4 = 2400 };
        if (phase != 0)
        {
            if (phase == 88)
            {
                throw new NotSupportedException("Native Quezacotl6C3940 phase88 requires its next skinned/ribbon controller frame and original6C7D10 particle producer; this callback is not implemented.");
            }
            throw new NotSupportedException($"Native Quezacotl6C3940 phase{phase} requires original7016B0 model workspace and701970/6C6060 geometry callbacks; full GF animation is not implemented.");
        }
        NativeBattleEffectIoPoll poll = NativeBattleEffectIo.Poll534270(memory);
        memory = poll.Memory;
        NativeBattleEffectFileRequest? request = poll.Request;
        if (!poll.Busy)
        {
            memory = NativeBattleEffectIo.Queue5341D0(memory, 0x1F9, 0x2108954, 1);
            memory = NativeBattleEffectIo.Queue5341D0(memory, 0x1FA, 0x2108954, 1);
            memory = NativeBattleEffectIo.Queue5341D0(memory, 0x1FB, 0x2108954, 2);
            NativeBattleEffectIoAdvance advance = NativeBattleEffectIo.Advance534210(memory);
            memory = advance.Memory;
            request = advance.Request;
            child = child with { Slots = child.Slots.SetItem(0, child.Slots[0].WriteWord(12, 1)) };
            memory = memory with { Arena = memory.Arena!.Write(12, [1, 0]) };
        }
        effect = effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == child.DescriptorAddress ? child : pool).ToImmutableArray(), Quezacotl = state };
        return new(memory, FinishMain6C3760(memory, effect), request, !poll.Busy);
    }

    public static NativeBattleOriginalEffectState FinishMain6C3760(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, bool ribbonExecuted = false)
    {
        //6C3760 runs the remaining five508420 lists after its primary child. Their real cold head pointers are retained separately.
        ImmutableArray<uint> heads = memory.QuezacotlSecondaryHeads ?? throw new NotSupportedException("Native6C3760 requires the retained heads of its five secondary private task descriptors.");
        if (heads.IsDefault || heads.Length != 5)
        {
            throw new InvalidDataException("Native Quezacotl requires its five physical secondary descriptor head DWORDs.");
        }
        if (heads.Skip(1).Any(head => head != 0) || heads[0] != 0 && !ribbonExecuted)
        {
            throw new NotSupportedException("Native6C3760 secondary Quezacotl task callback requires its actual retained pool owner.");
        }
        NativeBattleEffectTaskPool main = effect.Pools.Single(pool => pool.DescriptorAddress == 0x2521748);
        if ((memory.QuezacotlPause ?? throw new NotSupportedException("Native6C3760 requires its retained pause DWORD.")) == 0)
        {
            main = main with { Slots = main.Slots.SetItem(0, main.Slots[0].WriteWord(12, unchecked((ushort)(main.Slots[0].ReadWord(12) + 1)))) };
        }
        return effect with { Pools = effect.Pools.Select(pool => pool.DescriptorAddress == main.DescriptorAddress ? main : pool).ToImmutableArray() };
    }
}
