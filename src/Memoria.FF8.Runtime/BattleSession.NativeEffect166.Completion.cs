namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect166Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleEffect166Profile.Entry } effect || address is not (NativeBattleEffect166Profile.RootBank + 14 or NativeBattleEffect166Profile.RootBank + 30))
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect166 completion requires its current process memory.");
        NativeBattleEffect166State state = memory.Effect166 ?? throw new NotSupportedException("Effect166 completion requires its retained storage.");
        NativeBattleEffect166Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect166 completion requires its retained CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect166Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect166 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
