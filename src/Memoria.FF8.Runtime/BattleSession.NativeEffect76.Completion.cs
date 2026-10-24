namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect76Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState updated)
    {
        updated = graphics;
        if (address == NativeBattleEffect76Profile.MusicCompletionGlobal)
        {
            NativeBattleEffectMemory owner = NativeEffectMemory ?? throw new NotSupportedException("Effect76 completion requires its current process memory.");
            NativeBattleEffect76State state = owner.Effect76 ?? throw new NotSupportedException("Effect76 completion requires its retained storage.");
            NativeEffectMemory = owner with { Effect76 = state.Write(address, [value]) };
            return true;
        }
        uint relative = unchecked(address - NativeBattleEffect76Profile.ChildBank);
        if (relative >= NativeBattleEffect76Profile.ChildStride * NativeBattleEffect76Profile.ChildCount || relative % NativeBattleEffect76Profile.ChildStride != 14)
        {
            return false;
        }
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect is { Binding.Entry: NativeBattleEffect76Profile.Entry } current ? current : NativeEffectMemory?.DetachedReturnedTask is { Binding.Entry: NativeBattleEffect76Profile.Entry } detached ? detached : throw new NotSupportedException("Effect76 completion lost its actual task owner.");
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect76 completion requires its current process memory.");
        NativeBattleEffect76Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect76 completion requires its current CRT owner."), memory.Effect76 ?? throw new NotSupportedException("Effect76 completion requires its retained storage.")) { Staging = NativeFileStaging };
        frame = NativeBattleEffect76Controller.WriteCompletionByte(frame, address, value);
        bool active = ReferenceEquals(effect, graphics.OriginalEffect);
        NativeEffectMemory = frame.Memory with { Effect76 = frame.State, DetachedReturnedTask = active ? frame.Memory.DetachedReturnedTask : frame.Effect };
        NativeFileStaging = frame.Staging;
        nativeCrtRandom = frame.Random;
        updated = active ? graphics with { OriginalEffect = frame.Effect } : graphics;
        return true;
    }
}
