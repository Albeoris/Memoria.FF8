namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeResonance49Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleResonance49Profile.RootBank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleResonance49Profile.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteResonance49CompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleResonance49Profile.Entry } current ? current : throw new NotSupportedException("Resonance49 completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteResonance49CompletionByte(effect, address, value) };
        }
        return true;
    }

    private static NativeBattleOriginalEffectState WriteResonance49CompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - NativeBattleResonance49Profile.RootBank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            throw new InvalidDataException("Resonance49 completion does not address its physical root completion byte.");
        }
        uint taskAddress = address - 0x62;
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        return effect with { Pools = hierarchy.Write(taskAddress, task.WriteByte(0x62, value)).Pools };
    }
}
