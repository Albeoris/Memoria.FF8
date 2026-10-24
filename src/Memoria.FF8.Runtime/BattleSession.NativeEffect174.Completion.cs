namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect174Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleEffect174Profile.Entry } effect || address is not (NativeBattleEffect174Profile.RootBank + 14 or NativeBattleEffect174Profile.RootBank + 30))
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect174 completion requires its current process memory.");
        NativeBattleEffect174State state = memory.Effect174 ?? throw new NotSupportedException("Effect174 completion requires its retained storage.");
        NativeBattleEffect174Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect174 completion requires its retained CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect174Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect174 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
