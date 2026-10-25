namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect312Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleEffect312Profile.Entry } effect || address is not (NativeBattleEffect312Profile.RootBank + 14 or NativeBattleEffect312Profile.RootBank + 30))
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect312 completion requires its current process memory.");
        NativeBattleEffect312State state = memory.Effect312 ?? throw new NotSupportedException("Effect312 completion requires its retained storage.");
        NativeBattleEffect312Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect312 completion requires its retained CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect312Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect312 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
