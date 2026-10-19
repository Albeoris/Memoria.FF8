namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeShot192Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x227B348);
        if (delta >= 4 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5B96F0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleShot192.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5B96F0 } current ? current : throw new NotSupportedException("Native Scatter Shot completion lost its physical root task owner.");
            updated = graphics with { OriginalEffect = NativeBattleShot192.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
