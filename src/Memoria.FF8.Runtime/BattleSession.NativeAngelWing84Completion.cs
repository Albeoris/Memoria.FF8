namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeAngelWing84Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x25C24C0);
        if (delta >= 2 * 100 || delta % 100 != 98)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Angel Wing completion requires its current process storage.");
        NativeBattleAngelWing84State state = memory.AngelWing84 ?? throw new NotSupportedException("Native Angel Wing completion requires its entered module storage.");
        if (memory.DetachedReturnedTask is { Binding.Entry: 0x78D7B0 } detached)
        {
            NativeEffectMemory = memory with { DetachedReturnedTask = WriteAngelCompletion(new(memory, detached, state), address, value).Effect };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x78D7B0 } current ? current : throw new NotSupportedException("Native Angel Wing completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteAngelCompletion(new(memory, effect, state), address, value).Effect };
        }
        return true;
    }
}
