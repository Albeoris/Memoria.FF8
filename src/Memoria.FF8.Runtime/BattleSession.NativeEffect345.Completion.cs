namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect345Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleEffect345Profile.RootBank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleEffect345Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteEffect345Completion(detached) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleEffect345Profile.Entry } current ? current : throw new NotSupportedException("Effect345 completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteEffect345Completion(effect) };
        }
        return true;

        NativeBattleOriginalEffectState WriteEffect345Completion(NativeBattleOriginalEffectState effect)
        {
            uint taskAddress = address - 0x62;
            NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
            NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
            return effect with { Pools = hierarchy.Write(taskAddress, task.WriteByte(0x62, value)).Pools };
        }
    }
}
