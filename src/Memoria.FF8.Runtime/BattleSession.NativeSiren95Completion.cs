namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeSiren95Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleSiren95Controller.MainBank);
        if (delta >= 0xC8 || delta % 0x64 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: 0x739DA0 } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = NativeBattleSiren95Controller.WriteCompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: 0x739DA0 } current ? current : throw new NotSupportedException("Native Siren completion lost its physical root task.");
            updated = graphics with { OriginalEffect = NativeBattleSiren95Controller.WriteCompletionByte(effect, address, value) };
        }
        return true;
    }
}
