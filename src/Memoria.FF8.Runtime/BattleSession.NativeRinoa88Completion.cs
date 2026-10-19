namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeRinoa88Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x25A4EB0);
        if (delta >= 2 * 100 || delta % 100 != 98)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa88 completion requires its retained effect process.");
        NativeBattleRinoa88State state = memory.Rinoa88 ?? throw new NotSupportedException("Native Rinoa88 completion requires its entered process storage.");
        if (memory.DetachedReturnedTask is { Binding.Entry: 0x7684F0 } detached)
        {
            NativeEffectMemory = memory with { DetachedReturnedTask = WriteRinoa88Completion(new(memory, detached, state), address, value).Effect };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x7684F0 } current ? current : throw new NotSupportedException("Native Rinoa88 completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteRinoa88Completion(new(memory, effect, state), address, value).Effect };
        }
        return true;
    }
}
