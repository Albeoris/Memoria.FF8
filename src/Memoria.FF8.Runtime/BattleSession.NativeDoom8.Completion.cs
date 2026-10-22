namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void CompleteNativeDoom8File(uint callback)
    {
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Doom file completion requires its process memory.");
        NativeBattleDoom8State state = memory.Doom8 ?? throw new NotSupportedException("Doom file completion requires its live IO owner.");
        (NativeBattleProfiledIoState completed, NativeBattleEffectMemory updated) = NativeBattleDoom8Io.Complete(state.ReadIo(), memory, callback);
        NativeEffectMemory = updated with { Doom8 = state.WithIo(completed) };
    }

    private bool TryWriteNativeDoom8Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        uint delta = unchecked(address - NativeBattleDoom8State.Pools[0].Bank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            return false;
        }
        if (NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleDoom8Controller.Entry } detached)
        {
            NativeEffectMemory = NativeEffectMemory with { DetachedReturnedTask = WriteDoom8CompletionByte(detached, address, value) };
        }
        else
        {
            NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleDoom8Controller.Entry } current ? current : throw new NotSupportedException("Doom completion lost its physical root task.");
            updated = graphics with { OriginalEffect = WriteDoom8CompletionByte(effect, address, value) };
        }
        return true;
    }

    private static NativeBattleDoom8Frame WriteDoom8Completion(NativeBattleDoom8Frame frame, uint address, byte value)
    {
        return frame with { Effect = WriteDoom8CompletionByte(frame.Effect, address, value) };
    }

    private static NativeBattleOriginalEffectState WriteDoom8CompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - NativeBattleDoom8State.Pools[0].Bank);
        if (delta >= 200 || delta % 100 != 0x62)
        {
            throw new InvalidDataException("Doom completion does not address its physical root completion byte.");
        }
        uint taskAddress = address - 0x62;
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(taskAddress);
        return effect with { Pools = hierarchy.Write(taskAddress, task.WriteByte(0x62, value)).Pools };
    }
}
