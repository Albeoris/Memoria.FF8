namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect313Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleEffect313Profile.Entry } effect || address is not (NativeBattleEffect313Profile.RootBank + 14 or NativeBattleEffect313Profile.RootBank + 30))
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect313 completion requires its current process memory.");
        NativeBattleEffect313State state = memory.Effect313 ?? throw new NotSupportedException("Effect313 completion requires its retained storage.");
        NativeBattleEffect313Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect313 completion requires its retained CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect313Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect313 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
