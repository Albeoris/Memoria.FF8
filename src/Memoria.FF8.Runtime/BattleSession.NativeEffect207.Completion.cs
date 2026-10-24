namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect207Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: NativeBattleEffect207Profile.Entry } effect || address != (NativeEffectMemory?.Effect207 ?? throw new NotSupportedException("Effect207 completion requires its current storage.")).ReadDword(NativeBattleEffect207Profile.ArenaGlobal) + 0x810u)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect207 completion requires its current process memory.");
        NativeBattleEffect207State state = memory.Effect207 ?? throw new NotSupportedException("Effect207 completion requires its retained storage.");
        NativeBattleEffect207Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect207 completion requires its retained CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect207Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect207 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
