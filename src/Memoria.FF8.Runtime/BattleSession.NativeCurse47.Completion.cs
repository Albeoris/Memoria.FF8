namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void CompleteNativeCurse47File(uint callback)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Curse file completion requires its process memory.");
        NativeBattleCurse47State state = memory.Curse47 ?? throw new NotSupportedException("Curse file completion requires its live IO owner.");
        (NativeBattleCurse47State completed, NativeBattleEffectMemory updated) = NativeBattleCurse47Io.Complete(state, memory, callback);
        NativeEffectMemory = updated with { Curse47 = completed };
    }

    private bool TryWriteNativeCurse47Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleCurse47State.Pools[0].Bank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleCurse47Controller.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteCurse47CompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleCurse47Controller.Entry } current ? current : throw new NotSupportedException("Curse completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteCurse47CompletionByte(effect, address, value) };
        }
        return true;
    }

    private static NativeBattleCurse47Frame WriteCurse47Completion(NativeBattleCurse47Frame frame, uint address, byte value)
    {
        return frame with { Effect = WriteCurse47CompletionByte(frame.Effect, address, value) };
    }

    private static NativeBattleOriginalEffectState WriteCurse47CompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - NativeBattleCurse47State.Pools[0].Bank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            throw new InvalidDataException("Curse completion does not address its physical root completion byte.");
        }
        uint taskAddress = address - 0x62;
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        return effect with { Pools = hierarchy.Write(taskAddress, task.WriteByte(0x62, value)).Pools };
    }
}
