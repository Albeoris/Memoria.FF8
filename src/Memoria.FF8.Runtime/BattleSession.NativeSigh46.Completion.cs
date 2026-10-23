namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeSigh46Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleSigh46Profile.RootBank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleSigh46Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteSigh46CompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleSigh46Profile.Entry } current ? current : throw new NotSupportedException("Sigh46 completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteSigh46CompletionByte(effect, address, value) };
        }
        return true;
    }

    private static NativeBattleOriginalEffectState WriteSigh46CompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - NativeBattleSigh46Profile.RootBank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            throw new InvalidDataException("Sigh46 completion does not address its physical root completion byte.");
        }
        uint taskAddress = address - 0x62;
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        return effect with { Pools = hierarchy.Write(taskAddress, task.WriteByte(0x62, value)).Pools };
    }
}
