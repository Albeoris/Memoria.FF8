using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect218Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: Entry } effect || address < 0x24F2528 || address >= 0x24F2528 + 36 * 100 || (address - 0x24F2528) % 36 != 14)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect218 completion requires its current process memory.");
        NativeBattleEffect218Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect218 completion requires its retained CRT stream."), memory.Effect218 ?? throw new NotSupportedException("Effect218 completion requires its retained storage.")) { Staging = NativeFileStaging };
        frame = NativeBattleEffect218Controller.WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, [value]), 1);
        NativeEffectMemory = frame.Memory with { Effect218 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
