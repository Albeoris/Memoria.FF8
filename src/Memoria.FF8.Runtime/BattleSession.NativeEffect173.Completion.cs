namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect173Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleEffect173Profile.Entry } effect || address is not (NativeBattleEffect173Profile.RootBank + 14 or NativeBattleEffect173Profile.RootBank + 30))
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect173 completion requires its current process memory.");
        NativeBattleEffect173State state = memory.Effect173 ?? throw new NotSupportedException("Effect173 completion requires its retained storage.");
        NativeBattleEffect173Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect173 completion requires its retained CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect173Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect173 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
