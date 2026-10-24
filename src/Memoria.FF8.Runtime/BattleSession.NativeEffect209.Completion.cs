using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryWriteNativeEffect209Completion(NativeBattleGraphicsState graphics, uint address, byte value, out NativeBattleGraphicsState completed)
    {
        completed = graphics;
        if (graphics.OriginalEffect is not { Binding.Entry: Entry } effect || address < RootBank || address >= RootBank + 32 || (address - RootBank) % 16 != 14)
        {
            return false;
        }
        NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Effect209 completion requires its current process memory.");
        NativeBattleEffect209State state = memory.Effect209 ?? throw new NotSupportedException("Effect209 completion requires its retained storage.");
        NativeBattleEffect209Frame frame = new(memory, effect, nativeCrtRandom ?? throw new NotSupportedException("Effect209 completion requires its CRT stream."), state) { Staging = NativeFileStaging };
        frame = NativeBattleEffect209Controller.WriteCompletionByte(frame, address, value);
        NativeEffectMemory = frame.Memory with { Effect209 = frame.State };
        completed = graphics with { OriginalEffect = frame.Effect };
        return true;
    }
}
