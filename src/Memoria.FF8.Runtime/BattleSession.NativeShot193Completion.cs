namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeShot193Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x2276788);
        if (delta >= 4 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5B7190 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleShot193.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5B7190 } current ? current : throw new NotSupportedException("Native Dark Shot completion lost its physical root task owner.");
            updated = graphics with { OriginalEffect = NativeBattleShot193.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
