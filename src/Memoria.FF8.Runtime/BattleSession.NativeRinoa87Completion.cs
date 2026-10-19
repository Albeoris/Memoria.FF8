namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeRinoa87Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x25ACFC8);
        if (delta >= 2 * 100 || delta % 100 != 98)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Rinoa87 completion requires its retained effect process.");
        NativeBattleRinoa87State state = memory.Rinoa87 ?? throw new NotSupportedException("Native Rinoa87 completion requires its entered process storage.");
        if (memory.DetachedReturnedTask is { Binding.Entry: 0x773870 } detached)
        {
            NativeEffectMemory = memory with { DetachedReturnedTask = WriteRinoa87Completion(new(memory, detached, state), address, value).Effect };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x773870 } current ? current : throw new NotSupportedException("Native Rinoa87 completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteRinoa87Completion(new(memory, effect, state), address, value).Effect };
        }
        return true;
    }
}
