using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWall189Finish
{
    public static NativeBattleEffectMemory CopyCommonTexture(NativeBattleEffectMemory memory, ImmutableArray<byte> source)
    {
        NativeBattleWall189State state = memory.Wall189 ?? throw new NotSupportedException("Native Wall common texture restore requires its retained executable banks.");
        if (source.IsDefaultOrEmpty || source.Length > 0x1C000)
        {
            throw new NotSupportedException("Native Wall common texture exceeds its two original executable packet banks.");
        }
        return memory with { Wall189 = state with { Packets = state.Packets.Write(0, source.AsSpan()) } };
    }

    public static NativeBattleOriginalEffectState WriteCompletion(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint offset = unchecked(address - 0x2293F60u);
        if (offset >= 32 || offset % 16 != 14 || effect.Binding.Effect != 189 || effect.Pools.Length != 2)
        {
            throw new InvalidDataException("Native Wall lost its exact outro completion BYTE.");
        }
        int index = checked((int)(offset / 16));
        NativeBattleEffectTaskPool roots = effect.Pools[0];
        NativeBattleModulePoolTask task = roots.Slots[index];
        if ((task.Flags & 1) == 0 || task.Callback != 0x5BE2F0)
        {
            throw new InvalidDataException("Native Wall texture completion has no active original outro task.");
        }
        roots = roots.WriteSlot(index, task.WriteByte(14, value));
        return effect with { Pools = effect.Pools.SetItem(0, roots) };
    }
}
