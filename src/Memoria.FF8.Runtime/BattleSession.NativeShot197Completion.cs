namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeShot197Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - 0x2260BC0);
        if (delta >= 4 * 16 || delta % 16 != 14)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x5AED30 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleShot197.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x5AED30 } current ? current : throw new NotSupportedException("Native Armor Shot completion lost its physical root task owner.");
            updated = graphics with { OriginalEffect = NativeBattleShot197.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
