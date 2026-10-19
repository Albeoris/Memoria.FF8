namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeShot194Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x2271BC8);
        if (delta >= 4 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5B4C60 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleShot194.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5B4C60 } current ? current : throw new NotSupportedException("Native Flame Shot completion lost its physical root task owner.");
            updated = graphics with { OriginalEffect = NativeBattleShot194.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
